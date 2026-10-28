using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace StudentConnect.Helpers
{
    public static class ModerationStatuses
    {
        public const string AiApproved = "AI_APPROVED";
        public const string AiRejected = "AI_REJECTED";
        public const string AiUnavailable = "AI_UNAVAILABLE";
        public const string LocalRejected = "LOCAL_REJECTED";
        public const string PendingReview = "PENDING_REVIEW";
        public const string NotApplicable = "NOT_APPLICABLE";
        public const string DisabledLocalOnly = "DISABLED_LOCAL_ONLY";
    }

    public class FileModerationResult

    {

        public bool IsApproved { get; set; }

        public string ErrorCode { get; set; }

        public string UserFriendlyMessage { get; set; }

        public string DetectedMimeType { get; set; }

        public string DetectedExtension { get; set; }

        public bool IsImage { get; set; }

        public string AntivirusStatus { get; set; } // "CLEAN", "THREAT_DETECTED", "SCAN_UNAVAILABLE", "SCAN_FAILED"

        public string AntivirusDetails { get; set; }

        public string ImageModerationStatus { get; set; } // "SAFE", "FLAGGED", "SERVICE_UNAVAILABLE", "NOT_CONFIGURED", "NOT_APPLICABLE"

        public string ImageModerationReason { get; set; }

        public string FileSha256 { get; set; }

        public long FileSizeBytes { get; set; }

        public string OriginalFileName { get; set; }

        public string SanitizedFileName { get; set; }

        public string GeneratedFileKey { get; set; }

    }

    public class ModerationAuditEntry

    {

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public int UserID { get; set; }

        public string Username { get; set; }

        public int RoomID { get; set; }

        public string OriginalFileName { get; set; }

        public long FileSizeBytes { get; set; }

        public string Sha256 { get; set; }

        public string DeclaredMimeType { get; set; }

        public string DetectedExtension { get; set; }

        public bool IsImage { get; set; }

        public string SignatureCheckStatus { get; set; }

        public string AntivirusStatus { get; set; }

        public string ImageModerationStatus { get; set; }

        public string FinalDecision { get; set; } // "APPROVED" or "REJECTED"

        public string DecisionReason { get; set; }

    }

    /// <summary>

    /// Hệ thống kiểm duyệt toàn diện và bảo vệ an toàn cho hình ảnh, tệp đính kèm trong Trò chuyện.

    /// Kiểm tra xác thực, giới hạn dung lượng, định dạng, chữ ký file (Magic Bytes), quét mã độc và kiểm duyệt ảnh bằng AI.

    /// </summary>

    public static class ChatFileModerationHelper
    {
        private static readonly object _logLock = new object();

        // Bộ nhớ đệm (Cache) kết quả kiểm duyệt theo mã SHA-256 (lưu trữ 24h)
        private static readonly ConcurrentDictionary<string, (string status, bool isSafe, string reason, DateTime cachedUtc)> _moderationCache
            = new ConcurrentDictionary<string, (string status, bool isSafe, string reason, DateTime cachedUtc)>(StringComparer.OrdinalIgnoreCase);

        // Quản lý Circuit Breaker phòng chống cạn kiệt hạn ngạch (Quota Exhaustion) và lỗi hàng loạt
        private static readonly object _circuitLock = new object();
        private static DateTime _circuitOpenUntilUtc = DateTime.MinValue;
        private static int _consecutiveFailureCount = 0;
        private const int CircuitFailureThreshold = 3;
        private static readonly TimeSpan RateLimitCircuitDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan GeneralCircuitDuration = TimeSpan.FromMinutes(2);

        private static readonly HttpClient _aiHttpClient = CreateAiHttpClient();

        private static HttpClient CreateAiHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
        }

        public static bool IsCircuitBreakerOpen()
        {
            lock (_circuitLock)
            {
                return DateTime.UtcNow < _circuitOpenUntilUtc;
            }
        }

        public static void RecordSuccess()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = 0;
                _circuitOpenUntilUtc = DateTime.MinValue;
            }
        }

        public static void RecordRateLimitFailure()
        {
            lock (_circuitLock)
            {
                _circuitOpenUntilUtc = DateTime.UtcNow.Add(RateLimitCircuitDuration);
                _consecutiveFailureCount = CircuitFailureThreshold;
            }
        }

        public static void RecordGeneralFailure()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount++;
                if (_consecutiveFailureCount >= CircuitFailureThreshold)
                {
                    _circuitOpenUntilUtc = DateTime.UtcNow.Add(GeneralCircuitDuration);
                }
            }
        }

        public static void ResetCircuitBreaker()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = 0;
                _circuitOpenUntilUtc = DateTime.MinValue;
            }
        }

        public static void TriggerCircuitBreakerForTesting(int minutes = 5)
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = CircuitFailureThreshold;
                _circuitOpenUntilUtc = DateTime.UtcNow.AddMinutes(minutes);
            }
        }

        public static void ClearModerationCache()
        {
            _moderationCache.Clear();
        }

        public static int GetModerationCacheCount()
        {
            return _moderationCache.Count;
        }

        /// <summary>
        /// Nén và hạ tỉ lệ ảnh trong bộ nhớ trước khi gửi Gemini để tiết kiệm token và băng thông mạng
        /// </summary>
        private static byte[] PrepareOptimizedImageBytes(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var original = Image.FromStream(fs, false, false))
                {
                    int maxWidth = 1024;
                    int maxHeight = 1024;
                    bool needsResize = original.Width > maxWidth || original.Height > maxHeight;
                    bool isBigFile = fs.Length > 512 * 1024;

                    if (!needsResize && !isBigFile)
                    {
                        fs.Position = 0;
                        using (var ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }

                    int targetWidth = original.Width;
                    int targetHeight = original.Height;
                    if (needsResize)
                    {
                        double ratioX = (double)maxWidth / original.Width;
                        double ratioY = (double)maxHeight / original.Height;
                        double ratio = Math.Min(ratioX, ratioY);
                        targetWidth = Math.Max(1, (int)(original.Width * ratio));
                        targetHeight = Math.Max(1, (int)(original.Height * ratio));
                    }

                    using (var resized = new Bitmap(targetWidth, targetHeight))
                    {
                        using (var graphics = Graphics.FromImage(resized))
                        {
                            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                            graphics.DrawImage(original, 0, 0, targetWidth, targetHeight);
                        }

                        using (var outMs = new MemoryStream())
                        {
                            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                            if (jpgEncoder != null)
                            {
                                var encoderParams = new EncoderParameters(1);
                                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
                                resized.Save(outMs, jpgEncoder, encoderParams);
                            }
                            else
                            {
                                resized.Save(outMs, ImageFormat.Jpeg);
                            }
                            return outMs.ToArray();
                        }
                    }
                }
            }
            catch
            {
                return File.ReadAllBytes(filePath);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }

        // Danh sách định dạng nguy hiểm bị cấm tuyệt đối (Blacklist)

        private static readonly HashSet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)

        {

            ".exe", ".bat", ".cmd", ".sh", ".bash", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",

            ".msc", ".msi", ".msp", ".com", ".pif", ".scr", ".hta", ".cpl", ".jar", ".class", ".reg", ".inf", ".ins",

            ".isp", ".iso", ".img", ".vhd", ".vhdx", ".php", ".php3", ".php4", ".php5", ".phtml", ".asp", ".aspx",

            ".cer", ".asa", ".asax", ".ashx", ".asmx", ".axd", ".jsp", ".jspx", ".cgi", ".pl", ".py", ".pyc", ".pyo",

            ".rb", ".bin", ".app", ".deb", ".rpm", ".dmg", ".pkg", ".dll", ".sys", ".drv"

        };

        // Danh sách định dạng ảnh được phép (Whitelist)

        public static readonly HashSet<string> AllowedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)

        {

            ".jpg", ".jpeg", ".png", ".gif", ".webp"

        };

        // Danh sách định dạng tài liệu được phép (Whitelist)

        public static readonly HashSet<string> AllowedDocExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)

        {

            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar", ".7z"

        };

        // Giới hạn dung lượng tệp

        public const long MaxSingleImageSizeBytes = 10 * 1024 * 1024;    // 10MB

        public const long MaxSingleDocSizeBytes = 15 * 1024 * 1024;      // 15MB

        public const long MaxBatchUploadSizeBytes = 25 * 1024 * 1024;    // 25MB

        public const int MaxBatchFileCount = 5;

        /// <summary>

        /// Tính mã băm SHA-256 của tệp để định danh và kiểm tra toàn vẹn

        /// </summary>

        public static string ComputeFileSha256(string filePath)

        {

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";

            try

            {

                using (var sha = SHA256.Create())

                using (var stream = File.OpenRead(filePath))

                {

                    byte[] hash = sha.ComputeHash(stream);

                    return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();

                }

            }

            catch

            {

                return "";

            }

        }

        /// <summary>

        /// Làm sạch tên file chống tấn công Path Traversal, Null Byte và ký tự đặc biệt

        /// </summary>

        public static string SanitizeFileName(string rawFileName)

        {

            if (string.IsNullOrWhiteSpace(rawFileName)) return "attachment";

            string fileName = Path.GetFileName(rawFileName);

            // Loại bỏ các ký tự điều khiển và null bytes

            fileName = new string(fileName.Where(c => !char.IsControl(c) && c != '\0').ToArray());

            string baseName = Path.GetFileNameWithoutExtension(fileName);

            string ext = Path.GetExtension(fileName) ?? "";

            string cleanBase = Regex.Replace(baseName, @"[^\w\-\.]", "_");

            if (cleanBase.Length > 60) cleanBase = cleanBase.Substring(0, 60);

            if (string.IsNullOrWhiteSpace(cleanBase)) cleanBase = "file";

            return cleanBase + ext.ToLowerInvariant();

        }

        /// <summary>

        /// Kiểm tra tên file có chứa phần mở rộng kép giả mạo hoặc nguy hiểm (Double Extension Detection)

        /// Ví dụ: document.pdf.exe, image.png.vbs

        /// </summary>

        public static bool HasDangerousDoubleExtension(string fileName, out string dangerousPart)

        {

            dangerousPart = null;

            if (string.IsNullOrWhiteSpace(fileName)) return false;

            string clean = Path.GetFileName(fileName);

            var parts = clean.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 2)

            {

                // Kiểm tra từ phần tử thứ 2 đến cuối cùng

                for (int i = 1; i < parts.Length; i++)

                {

                    string ext = "." + parts[i].ToLowerInvariant();

                    if (DangerousExtensions.Contains(ext))

                    {

                        dangerousPart = ext;

                        return true;

                    }

                }

            }

            return false;

        }

        /// <summary>

        /// Đọc header bytes của file để đối chiếu chữ ký số thực tế (Magic Bytes)

        /// </summary>

        public static (bool isValid, string detectedType, string reason) VerifyFileSignature(string filePath, string expectedExt)

        {

            if (!File.Exists(filePath))

                return (false, "UNKNOWN", "Tệp không tồn tại.");

            byte[] header = new byte[512];

            int bytesRead;

            try

            {

                using (var stream = File.OpenRead(filePath))

                {

                    bytesRead = stream.Read(header, 0, header.Length);

                }

            }

            catch (IOException ioEx) when (ioEx.Message.IndexOf("virus", StringComparison.OrdinalIgnoreCase) >= 0 ||

                                          ioEx.Message.IndexOf("potentially unwanted", StringComparison.OrdinalIgnoreCase) >= 0 ||

                                          ioEx.HResult == unchecked((int)0x800700E1))

            {

                return (false, "MALWARE_DETECTED", "Hệ thống bảo vệ (Windows Defender) đã phát hiện và phong tỏa mã độc trong tệp tin!");

            }

            catch (Exception ex)

            {

                return (false, "READ_ERROR", "Không thể đọc dữ liệu tệp: " + ex.Message);

            }

            if (bytesRead < 4)

                return (false, "EMPTY_OR_TRUNCATED", "Dung lượng tệp quá nhỏ hoặc rỗng.");

            // 1. CHẶN NGAY LẬP TỨC CÁC CHỮ KÝ THỰC THI (EXECUTABLE SIGNATURES)

            // Windows PE (MZ)

            if (header[0] == 0x4D && header[1] == 0x5A)

            {

                return (false, "EXECUTABLE_PE", "Phát hiện cấu trúc tệp thực thi Windows (MZ/PE header). Nguy cơ mã độc cao!");

            }

            // Linux ELF (\x7F ELF)

            if (header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46)

            {

                return (false, "EXECUTABLE_ELF", "Phát hiện tệp thực thi nhị phân Linux ELF.");

            }

            // Mach-O (macOS executable)

            if ((header[0] == 0xCA && header[1] == 0xFE && header[2] == 0xBA && header[3] == 0xBE) ||

                (header[0] == 0xFE && header[1] == 0xED && header[2] == 0xFA && header[3] == 0xCE) ||

                (header[0] == 0xFE && header[1] == 0xED && header[2] == 0xFA && header[3] == 0xCF))

            {

                return (false, "EXECUTABLE_MACHO", "Phát hiện tệp thực thi nhị phân macOS / Java class.");

            }

            // Kiểm tra nội dung script nhúng nguy hiểm trong 512 bytes đầu

            string headerText = Encoding.ASCII.GetString(header, 0, Math.Min(bytesRead, 512)).ToLowerInvariant();

            if (headerText.Contains("<?php") || headerText.Contains("<script") || headerText.Contains("<%") ||

                headerText.Contains("#!/bin/sh") || headerText.Contains("#!/bin/bash") ||

                headerText.Contains("wscript.shell") || headerText.Contains("powershell"))

            {

                return (false, "EMBEDDED_SCRIPT", "Phát hiện mã lệnh kịch bản (Script / Shell) ngụy trang trong tệp.");

            }

            string ext = expectedExt.ToLowerInvariant();

            // 2. XÁC MINH CHỮ KÝ ẢNH (IMAGE SIGNATURES)

            if (ext == ".jpg" || ext == ".jpeg")

            {

                // JPEG: FF D8 FF

                if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)

                {

                    return VerifyImageDecodeability(filePath, "image/jpeg");

                }

                return (false, "INVALID_JPEG", "Chữ ký tệp không khớp chuẩn JPEG (Thiếu Magic Bytes FF D8 FF).");

            }

            if (ext == ".png")

            {

                // PNG: 89 50 4E 47 0D 0A 1A 0A

                if (bytesRead >= 8 &&

                    header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&

                    header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)

                {

                    return VerifyImageDecodeability(filePath, "image/png");

                }

                return (false, "INVALID_PNG", "Chữ ký tệp không khớp chuẩn PNG (Thiếu Magic Bytes 89 50 4E 47).");

            }

            if (ext == ".gif")

            {

                // GIF: GIF87a hoặc GIF89a

                if (bytesRead >= 6 &&

                    header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 &&

                    header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)

                {

                    return VerifyImageDecodeability(filePath, "image/gif");

                }

                return (false, "INVALID_GIF", "Chữ ký tệp không khớp chuẩn GIF (Thiếu Magic Bytes GIF87a/GIF89a).");

            }

            if (ext == ".webp")

            {

                // WEBP: bytes 0..3 là "RIFF", bytes 8..11 là "WEBP"

                if (bytesRead >= 12 &&

                    header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&

                    header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)

                {

                    return (true, "image/webp", "Định dạng ảnh WebP hợp lệ.");

                }

                return (false, "INVALID_WEBP", "Chữ ký tệp không khớp chuẩn WebP (Thiếu Magic Bytes RIFF....WEBP).");

            }

            // 3. XÁC MINH CHỮ KÝ TÀI LIỆU (DOCUMENTS)

            if (ext == ".pdf")

            {

                // PDF: %PDF- (25 50 44 46 2D)

                if (bytesRead >= 5 &&

                    header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D)

                {

                    return (true, "application/pdf", "Định dạng tài liệu PDF hợp lệ.");

                }

                return (false, "INVALID_PDF", "Chữ ký tệp không khớp chuẩn tài liệu PDF (Thiếu Magic Bytes %PDF-).");

            }

            if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx" || ext == ".zip")

            {

                // Zip format: PK\x03\x04 (50 4B 03 04) hoặc PK\x05\x06 (50 4B 05 06) hoặc PK\x07\x08

                if (bytesRead >= 4 && header[0] == 0x50 && header[1] == 0x4B &&

                    (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07))

                {

                    return VerifyZipAndOfficeArchive(filePath, ext);

                }

                return (false, "INVALID_OFFICE_ZIP", "Chữ ký tệp không khớp cấu trúc gói Office/ZIP (Thiếu PK header).");

            }

            if (ext == ".doc" || ext == ".xls" || ext == ".ppt")

            {

                // OLE Compound File: D0 CF 11 E0 A1 B1 1A E1

                if (bytesRead >= 8 &&

                    header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&

                    header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1)

                {

                    return (true, "application/msword-ole", "Định dạng tài liệu Microsoft OLE nhị phân hợp lệ.");

                }

                return (false, "INVALID_OLE_DOC", "Chữ ký tệp không khớp định dạng tài liệu Microsoft Office OLE.");

            }

            if (ext == ".rar")

            {

                // RAR: Rar!\x1A\x07 (52 61 72 21 1A 07)

                if (bytesRead >= 6 &&

                    header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 &&

                    header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07)

                {

                    return (true, "application/vnd.rar", "Định dạng tệp nén RAR hợp lệ.");

                }

                return (false, "INVALID_RAR", "Chữ ký tệp không khớp định dạng lưu trữ nén RAR.");

            }

            if (ext == ".7z")

            {

                // 7z: 37 7A BC AF 27 1C

                if (bytesRead >= 6 &&

                    header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC &&

                    header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)

                {

                    return (true, "application/x-7z-compressed", "Định dạng tệp nén 7-Zip hợp lệ.");

                }

                return (false, "INVALID_7Z", "Chữ ký tệp không khớp định dạng lưu trữ nén 7-Zip.");

            }

            if (ext == ".txt" || ext == ".csv")

            {

                // Tệp văn bản thuần túy không được chứa null byte và không được chứa mã nhị phân không in được

                int nullCount = 0;

                for (int i = 0; i < bytesRead; i++)

                {

                    if (header[i] == 0x00) nullCount++;

                }

                if (nullCount > 0)

                {

                    return (false, "BINARY_IN_TEXT", "Tệp văn bản thuần chứa ký tự nhị phân bất thường (Null bytes).");

                }

                return (true, "text/plain", "Định dạng văn bản thuần túy hợp lệ.");

            }

            return (false, "UNSUPPORTED_FORMAT", $"Định dạng tệp '{ext}' không nằm trong danh mục cho phép.");

        }

        /// <summary>

        /// Giải mã thử ảnh bằng GDI+ để kiểm tra tệp ảnh có bị lỗi, hỏng cấu trúc hoặc che giấu payload không

        /// </summary>

        private static (bool isValid, string detectedType, string reason) VerifyImageDecodeability(string filePath, string mimeType)

        {

            try

            {

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))

                {

                    using (var img = Image.FromStream(fs, false, true))

                    {

                        if (img.Width <= 0 || img.Height <= 0)

                        {

                            return (false, "CORRUPT_IMAGE", "Kích thước ảnh không hợp lệ (Chiều rộng/cao <= 0).");

                        }

                        if (img.Width > 15000 || img.Height > 15000)

                        {

                            return (false, "IMAGE_BOMB", "Độ phân giải ảnh vượt quá ngưỡng an toàn (Khả năng tấn công Image Bomb).");

                        }

                        return (true, mimeType, $"Ảnh hợp lệ ({img.Width}x{img.Height}).");

                    }

                }

            }

            catch (Exception ex)

            {

                return (false, "CORRUPT_OR_MALFORMED_IMAGE", "Không thể giải mã cấu trúc ảnh. Tệp có thể bị hỏng hoặc cố ý sửa đổi: " + ex.Message);

            }

        }

        private static DateTime _lastCleanupUtc = DateTime.MinValue;

        private static readonly object _cleanupLock = new object();

        /// <summary>

        /// Dọn dẹp các tệp tạm tồn đọng (quá 1 giờ) trong thư mục cách ly kiểm duyệt

        /// </summary>

        public static void CleanOrphanedTempFiles(string tempDir, TimeSpan? maxAge = null)

        {

            if (string.IsNullOrEmpty(tempDir) || !Directory.Exists(tempDir)) return;

            lock (_cleanupLock)

            {

                if ((DateTime.UtcNow - _lastCleanupUtc).TotalMinutes < 10) return;

                _lastCleanupUtc = DateTime.UtcNow;

            }

            TimeSpan ageLimit = maxAge ?? TimeSpan.FromHours(1);

            DateTime threshold = DateTime.UtcNow - ageLimit;

            Task.Run(() =>

            {

                try

                {

                    var dirInfo = new DirectoryInfo(tempDir);

                    var oldFiles = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)

                                          .Where(f => f.LastWriteTimeUtc < threshold);

                    foreach (var file in oldFiles)

                    {

                        try { file.Delete(); } catch { }

                    }

                }

                catch { }

            });

        }

        /// <summary>

        /// Kiểm tra chuyên sâu cấu trúc tệp nén ZIP và tài liệu Office OpenXML (.docx, .xlsx, .pptx).

        /// Ngăn chặn tấn công Zip Bomb (tỉ lệ nén > 100:1, quá 1000 files, > 100MB), Zip Slip,

        /// tệp thực thi ẩn trong zip, và mã Macro (vbaProject.bin) trong tài liệu Office.

        /// </summary>

        public static (bool isValid, string detectedType, string reason) VerifyZipAndOfficeArchive(string filePath, string expectedExt)

        {

            string ext = (expectedExt ?? "").ToLowerInvariant();

            try

            {

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))

                {

                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))

                    {

                        var entries = archive.Entries;

                        if (entries == null)

                        {

                            return (false, "CORRUPT_ARCHIVE", "Không thể đọc danh mục phần tử trong tệp nén.");

                        }

                        if (entries.Count > 1000)

                        {

                            return (false, "ZIP_BOMB_ENTRY_COUNT", $"Tệp nén chứa quá nhiều phần tử ({entries.Count} entries > 1,000). Nguy cơ tấn công Zip Bomb.");

                        }

                        long totalUncompressedBytes = 0;

                        bool hasOfficeContentTypes = false;

                        bool hasOfficeRels = false;

                        foreach (var entry in entries)

                        {

                            // 1. Kiểm tra Zip Slip / Path Traversal

                            string entryName = entry.FullName ?? "";

                            if (entryName.Contains("..") ||

                                entryName.StartsWith("/") ||

                                entryName.StartsWith("\\") ||

                                (entryName.Length > 2 && entryName[1] == ':'))

                            {

                                return (false, "ZIP_SLIP_ATTACK", $"Phát hiện đường dẫn duyệt thư mục nguy hại (Zip Slip): '{entryName}'.");

                            }

                            // 2. Tích lũy dung lượng và kiểm tra tỉ lệ nén (Zip Bomb Detection)

                            totalUncompressedBytes += entry.Length;

                            if (totalUncompressedBytes > 104857600) // 100MB

                            {

                                return (false, "ZIP_BOMB_SIZE", "Tổng dung lượng giải nén vượt quá 100MB. Tệp bị chặn để phòng chống Zip Bomb.");

                            }

                            if (entry.CompressedLength > 0 && entry.Length > 1024 * 1024)

                            {

                                double ratio = (double)entry.Length / entry.CompressedLength;

                                if (ratio > 100.0)

                                {

                                    return (false, "ZIP_BOMB_RATIO", $"Phần tử '{entryName}' có tỉ lệ nén bất thường ({ratio:F0}:1 > 100:1). Nguy cơ tấn công Decompression Bomb.");

                                }

                            }

                            // 3. Nếu là tệp .zip thuần túy: chặn các tệp thực thi / script ẩn bên trong

                            if (ext == ".zip")

                            {

                                string innerExt = Path.GetExtension(entryName)?.ToLowerInvariant() ?? "";

                                if (DangerousExtensions.Contains(innerExt))

                                {

                                    return (false, "DANGEROUS_FILE_IN_ZIP", $"Phát hiện tệp thực thi/kịch bản nguy hại '{entryName}' được ẩn giấu bên trong tệp nén.");

                                }

                            }

                            // 4. Nếu là tài liệu Office OpenXML (.docx, .xlsx, .pptx)

                            if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx")

                            {

                                if (entryName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))

                                {

                                    hasOfficeContentTypes = true;

                                }

                                if (entryName.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase))

                                {

                                    hasOfficeRels = true;

                                }

                                // Chặn mã Macro (vbaProject.bin)

                                if (entryName.IndexOf("vbaProject.bin", StringComparison.OrdinalIgnoreCase) >= 0)

                                {

                                    return (false, "OFFICE_MACRO_BLOCKED", "Tài liệu Office chứa mã Macro (vbaProject.bin). Chỉ cho phép tài liệu thuần túy không chứa macro.");

                                }

                                // Chặn đối tượng nhúng có thể thực thi

                                string innerExt = Path.GetExtension(entryName)?.ToLowerInvariant() ?? "";

                                if (entryName.IndexOf("embeddings", StringComparison.OrdinalIgnoreCase) >= 0 &&

                                    (DangerousExtensions.Contains(innerExt) || innerExt == ".bin" || innerExt == ".ole"))

                                {

                                    return (false, "DANGEROUS_OFFICE_EMBEDDING", $"Phát hiện đối tượng nhúng nhị phân nguy hại '{entryName}' trong tài liệu Office.");

                                }

                            }

                        }

                        // Nếu là Office mà không có [Content_Types].xml hoặc _rels: đây là zip giả danh docx/xlsx/pptx

                        if ((ext == ".docx" || ext == ".xlsx" || ext == ".pptx") && (!hasOfficeContentTypes && !hasOfficeRels))

                        {

                            return (false, "INVALID_OFFICE_STRUCTURE", $"Tệp mang đuôi '{ext}' nhưng không có cấu trúc chuẩn OpenXML của Microsoft Office.");

                        }

                        string detectedMime = ext == ".zip" ? "application/zip" :

                                              ext == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" :

                                              ext == ".xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" :

                                              "application/vnd.openxmlformats-officedocument.presentationml.presentation";

                        return (true, detectedMime, $"Cấu trúc tệp '{ext}' hợp lệ và an toàn ({entries.Count} phần tử, {totalUncompressedBytes} bytes).");

                    }

                }

            }

            catch (InvalidDataException idEx)

            {

                return (false, "CORRUPT_ZIP", "Cấu trúc tệp lưu trữ ZIP bị hỏng hoặc không đúng định dạng: " + idEx.Message);

            }

            catch (IOException ioEx) when (ioEx.Message.IndexOf("virus", StringComparison.OrdinalIgnoreCase) >= 0 ||

                                          ioEx.Message.IndexOf("potentially unwanted", StringComparison.OrdinalIgnoreCase) >= 0 ||

                                          ioEx.HResult == unchecked((int)0x800700E1))

            {

                return (false, "MALWARE_DETECTED", "Hệ thống bảo vệ (Windows Defender) đã phát hiện và phong tỏa mã độc trong tệp nén!");

            }

            catch (Exception ex)

            {

                return (false, "ARCHIVE_READ_ERROR", "Lỗi kiểm tra cấu trúc lưu trữ: " + ex.Message);

            }

        }

        /// <summary>

        /// Tìm đường dẫn thực thi của Windows Defender (MpCmdRun.exe)

        /// </summary>

        private static string ResolveMpCmdRunPath()

        {

            string[] standardPaths = new[]

            {

                @"C:\Program Files\Windows Defender\MpCmdRun.exe",

                @"C:\Program Files (x86)\Windows Defender\MpCmdRun.exe"

            };

            foreach (var path in standardPaths)

            {

                if (File.Exists(path)) return path;

            }

            string platformDir = @"C:\ProgramData\Microsoft\Windows Defender\Platform";

            if (Directory.Exists(platformDir))

            {

                try

                {

                    var files = Directory.GetFiles(platformDir, "MpCmdRun.exe", SearchOption.AllDirectories);

                    if (files.Length > 0)

                    {

                        return files.OrderByDescending(f => f).First();

                    }

                }

                catch { }

            }

            return null;

        }

        /// <summary>

        /// Quét mã độc tệp tin bằng trình quét Antivirus thực tế (Windows Defender CLI)

        /// </summary>

        public static (string status, string details) ScanFileWithAntivirus(string filePath)

        {

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))

            {

                return ("SCAN_FAILED", "Tệp kiểm tra không tồn tại.");

            }

            string mpCmdRun = ResolveMpCmdRunPath();

            if (string.IsNullOrEmpty(mpCmdRun))

            {

                return ("SCAN_UNAVAILABLE", "Trình quét Windows Defender không khả dụng trên môi trường máy chủ này.");

            }

            try

            {

                var psi = new ProcessStartInfo

                {

                    FileName = mpCmdRun,

                    Arguments = $"-Scan -ScanType 3 -File \"{filePath}\" -DisableRemediation",

                    CreateNoWindow = true,

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true

                };

                using (var proc = Process.Start(psi))

                {

                    bool finished = proc.WaitForExit(10000); // 10 giây timeout

                    if (!finished)

                    {

                        try { proc.Kill(); } catch { }

                        return ("SCAN_FAILED", "Quá thời gian quét mã độc (Timeout 10s).");

                    }

                    int exitCode = proc.ExitCode;

                    string stdout = proc.StandardOutput.ReadToEnd();

                    // Exit code 0: Không phát hiện mối đe dọa nào (Clean)

                    // Exit code 2: Phát hiện mối đe dọa (Threat detected)

                    if (exitCode == 0)

                    {

                        return ("CLEAN", "Không phát hiện mã độc (Windows Defender kiểm tra an toàn).");

                    }

                    else if (exitCode == 2)

                    {

                        return ("THREAT_DETECTED", "Phát hiện mã độc hoặc phần mềm nguy hại trong tệp tin! Tệp bị từ chối.");

                    }

                    else

                    {

                        return ("SCAN_FAILED", $"Trình quét trả về mã lỗi {exitCode}: {stdout.Trim()}");

                    }

                }

            }

            catch (Exception ex)

            {

                return ("SCAN_FAILED", "Lỗi khi thực thi trình quét mã độc: " + ex.Message);

            }

        }

        /// <summary>
        /// Kiểm duyệt nội dung hình ảnh bằng Google Gemini API (model Flash-Lite).
        /// Có cơ chế đệm kết quả SHA-256, Circuit Breaker khi 429/ResourceExhausted, và tự động fallback sang bảo vệ cục bộ.
        /// </summary>
        public static async Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(
            string imagePath,
            string mimeType,
            string fileSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return (ModerationStatuses.AiUnavailable, false, "Tệp ảnh không tồn tại để kiểm duyệt.");
            }

            string sha256 = fileSha256 ?? ComputeFileSha256(imagePath);

            // 1. Kiểm tra bộ nhớ đệm (Cache) theo SHA-256
            if (!string.IsNullOrEmpty(sha256) && _moderationCache.TryGetValue(sha256, out var cached))
            {
                if ((DateTime.UtcNow - cached.cachedUtc).TotalHours < 24)
                {
                    return (cached.status, cached.isSafe, cached.reason + " (kết quả từ bộ nhớ đệm)");
                }
            }

            // 2. Kiểm tra Circuit Breaker (ngắt mạch tạm thời nếu API đang kiệt hạn mức quota)
            if (IsCircuitBreakerOpen())
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Circuit breaker đang kích hoạt do giới hạn tần suất Gemini API. Chuyển sang bảo vệ bằng lớp kiểm duyệt kỹ thuật cục bộ.");
            }

            // 3. Đọc cấu hình API Key và Model từ AppSettings / Biến môi trường
            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                            ?? "";
            apiKey = (apiKey ?? "").Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Chưa cấu hình Gemini API Key. Đang áp dụng lớp bảo vệ an toàn kỹ thuật cục bộ.");
            }

            string model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-1.5-flash";
            model = (model ?? "").Trim();
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-1.5-flash";

            // 4. Chuẩn bị ảnh nén/tối ưu kích thước (max 1024x1024, JPEG 75%)
            byte[] imageBytes = PrepareOptimizedImageBytes(imagePath);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return (ModerationStatuses.AiUnavailable, false, "Không thể đọc dữ liệu ảnh để phân tích.");
            }

            string base64Data = Convert.ToBase64String(imageBytes);
            string payloadMime = "image/jpeg";

            string prompt = "Analyze this image for student community chat safety (pornography, sexual content, graphic violence, hate speech, illegal weapons). Return JSON strictly: {\"isSafe\": true/false, \"violationCategory\": \"NONE\" or category, \"reason\": \"Vietnamese explanation\"}";

            var payloadObj = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = prompt },
                            new JObject
                            {
                                ["inlineData"] = new JObject
                                {
                                    ["mimeType"] = payloadMime,
                                    ["data"] = base64Data
                                }
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["temperature"] = 0.1,
                    ["maxOutputTokens"] = 200
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    request.Content = new StringContent(payloadObj.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    using (var response = await _aiHttpClient.SendAsync(request))
                    {
                        string respBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            RecordSuccess();

                            var parsed = JObject.Parse(respBody);
                            string jsonText = (string)parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];

                            if (!string.IsNullOrWhiteSpace(jsonText))
                            {
                                var modObj = JObject.Parse(jsonText);
                                bool isSafe = (bool)(modObj["isSafe"] ?? true);
                                string reason = (string)modObj["reason"] ?? (isSafe ? "Nội dung an toàn." : "Hình ảnh vi phạm quy chuẩn cộng đồng.");

                                string status = isSafe ? ModerationStatuses.AiApproved : ModerationStatuses.AiRejected;
                                if (!string.IsNullOrEmpty(sha256))
                                {
                                    _moderationCache[sha256] = (status, isSafe, reason, DateTime.UtcNow);
                                }

                                return (status, isSafe, reason);
                            }

                            return (ModerationStatuses.AiApproved, true, "Nội dung hình ảnh đã được AI phân tích an toàn.");
                        }

                        int statusCode = (int)response.StatusCode;
                        string errReason = "";
                        string errMsg = respBody;
                        try
                        {
                            var errObj = JObject.Parse(respBody);
                            errMsg = (string)errObj["error"]?["message"] ?? respBody;
                            errReason = (string)errObj["error"]?["details"]?[0]?["reason"] ?? "";
                        }
                        catch { }

                        if (statusCode == 429 || errReason == "RESOURCE_EXHAUSTED" || errMsg.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            RecordRateLimitFailure();
                            return (ModerationStatuses.AiUnavailable, false,
                                "Gemini API hết quota hoặc chạm giới hạn tần suất (HTTP 429/ResourceExhausted). Hệ thống tự động chuyển sang chế độ kiểm duyệt kỹ thuật cục bộ.");
                        }

                        if (statusCode == 400 || statusCode == 401 || statusCode == 403)
                        {
                            RecordGeneralFailure();
                            return (ModerationStatuses.AiUnavailable, false,
                                $"Xác thực Gemini API không thành công (HTTP {statusCode}). Tệp được bảo vệ bởi lớp kiểm tra an toàn kỹ thuật cục bộ.");
                        }

                        RecordGeneralFailure();
                        return (ModerationStatuses.AiUnavailable, false,
                            $"Dịch vụ Gemini API tạm thời không khả dụng (HTTP {statusCode}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    "Quá thời gian phản hồi từ Gemini API (Timeout). Hệ thống chuyển sang bảo vệ bằng lớp kiểm duyệt kỹ thuật cục bộ.");
            }
            catch (HttpRequestException httpEx)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    $"Lỗi mạng kết nối đến Gemini API ({httpEx.Message}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
            }
            catch (Exception ex)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    $"Lỗi trong quá trình kiểm duyệt AI ({ex.Message}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
            }
        }

        /// <summary>
        /// Kiểm duyệt nội dung văn bản thuần túy (.txt, .csv) an toàn, trích xuất tối đa 2000 ký tự đầu.
        /// Tuyệt đối KHÔNG gửi dữ liệu nhị phân hoặc tệp nhạy cảm lên API.
        /// </summary>
        public static async Task<(string status, bool isSafe, string reason)> ModerateDocumentTextWithAiAsync(
            string filePath,
            string fileSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return (ModerationStatuses.AiUnavailable, false, "Tệp văn bản không tồn tại.");
            }

            string sha256 = fileSha256 ?? ComputeFileSha256(filePath);

            if (!string.IsNullOrEmpty(sha256) && _moderationCache.TryGetValue(sha256, out var cached))
            {
                if ((DateTime.UtcNow - cached.cachedUtc).TotalHours < 24)
                {
                    return (cached.status, cached.isSafe, cached.reason + " (kết quả từ bộ nhớ đệm)");
                }
            }

            if (IsCircuitBreakerOpen())
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Circuit breaker đang kích hoạt do giới hạn tần suất API. Chuyển sang bảo vệ cục bộ.");
            }

            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                            ?? "";
            apiKey = (apiKey ?? "").Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Chưa cấu hình Gemini API Key. Đang áp dụng bảo vệ an toàn kỹ thuật cục bộ.");
            }

            string textContent;
            try
            {
                using (var sr = new StreamReader(filePath, Encoding.UTF8))
                {
                    char[] buffer = new char[2000];
                    int read = sr.Read(buffer, 0, buffer.Length);
                    textContent = new string(buffer, 0, read);
                }
            }
            catch (Exception ex)
            {
                return (ModerationStatuses.AiUnavailable, false, "Không thể đọc nội dung văn bản: " + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(textContent))
            {
                return (ModerationStatuses.AiApproved, true, "Tệp văn bản rỗng hoặc chỉ chứa khoảng trắng.");
            }

            string model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-1.5-flash";
            model = (model ?? "").Trim();
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-1.5-flash";

            string escapedText = textContent.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
            string prompt = "Analyze this user text attachment for chat safety (hate speech, sexual harassment, violent threats, illegal). Return JSON strictly: {\"isSafe\": true/false, \"violationCategory\": \"NONE\" or category, \"reason\": \"Vietnamese explanation\"}\nText to analyze:\n\"" + escapedText + "\"";

            var payloadObj = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = prompt }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["temperature"] = 0.1,
                    ["maxOutputTokens"] = 200
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    request.Content = new StringContent(payloadObj.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    using (var response = await _aiHttpClient.SendAsync(request))
                    {
                        string respBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            RecordSuccess();

                            var parsed = JObject.Parse(respBody);
                            string jsonText = (string)parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];

                            if (!string.IsNullOrWhiteSpace(jsonText))
                            {
                                var modObj = JObject.Parse(jsonText);
                                bool isSafe = (bool)(modObj["isSafe"] ?? true);
                                string reason = (string)modObj["reason"] ?? (isSafe ? "Nội dung văn bản an toàn." : "Văn bản vi phạm chính sách nội dung.");

                                string status = isSafe ? ModerationStatuses.AiApproved : ModerationStatuses.AiRejected;
                                if (!string.IsNullOrEmpty(sha256))
                                {
                                    _moderationCache[sha256] = (status, isSafe, reason, DateTime.UtcNow);
                                }

                                return (status, isSafe, reason);
                            }

                            return (ModerationStatuses.AiApproved, true, "Nội dung văn bản đã được AI phân tích an toàn.");
                        }

                        int statusCode = (int)response.StatusCode;
                        if (statusCode == 429)
                        {
                            RecordRateLimitFailure();
                            return (ModerationStatuses.AiUnavailable, false, "Gemini API hết quota (HTTP 429). Chuyển sang bảo vệ kỹ thuật cục bộ.");
                        }

                        RecordGeneralFailure();
                        return (ModerationStatuses.AiUnavailable, false, $"Gemini API không khả dụng (HTTP {statusCode}). Áp dụng bảo vệ kỹ thuật cục bộ.");
                    }
                }
            }
            catch (Exception ex)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false, $"Lỗi kiểm duyệt văn bản bằng AI ({ex.Message}). Áp dụng bảo vệ kỹ thuật cục bộ.");
            }
        }

        /// <summary>

        /// Ghi nhật ký kiểm duyệt tệp tin vào tệp Audit Log có cấu trúc trong App_Data

        /// </summary>

        public static void LogModerationAudit(ModerationAuditEntry entry)

        {

            if (entry == null) return;

            try

            {

                string logDir = HttpContext.Current != null

                    ? HttpContext.Current.Server.MapPath("~/App_Data/Logs/ChatModeration")

                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs", "ChatModeration");

                if (!Directory.Exists(logDir))

                {

                    Directory.CreateDirectory(logDir);

                }

                string logFilePath = Path.Combine(logDir, $"ChatFileModeration_{DateTime.Now:yyyyMMdd}.log");

                string logLine = string.Format(

                    "[{0:yyyy-MM-dd HH:mm:ss}] [AUDIT] UserID: {1} (@{2}) | RoomID: {3} | File: \"{4}\" ({5} B, {6}) | SHA256: {7} | SigCheck: {8} | AV: {9} | AI Mod: {10} | Decision: {11} | Reason: {12}",

                    entry.Timestamp,

                    entry.UserID,

                    entry.Username ?? "Unknown",

                    entry.RoomID,

                    entry.OriginalFileName,

                    entry.FileSizeBytes,

                    entry.DetectedExtension,

                    entry.Sha256,

                    entry.SignatureCheckStatus,

                    entry.AntivirusStatus,

                    entry.ImageModerationStatus,

                    entry.FinalDecision,

                    entry.DecisionReason

                );

                lock (_logLock)

                {

                    File.AppendAllText(logFilePath, logLine + Environment.NewLine, Encoding.UTF8);

                }

            }

            catch (Exception ex)

            {

                Debug.WriteLine("[ChatFileModeration] Error writing audit log: " + ex.Message);

            }

        }

        /// <summary>

        /// Quy trình kiểm duyệt toàn diện cho một tệp được tải lên trong Trò chuyện

        /// </summary>

        public static async Task<FileModerationResult> ValidateAndModerateFileAsync(

            string stagedFilePath,

            string originalFileName,

            string declaredMimeType,

            bool isExplicitImage,

            int userId,

            string username,

            int roomId)

        {

            var audit = new ModerationAuditEntry

            {

                UserID = userId,

                Username = username,

                RoomID = roomId,

                OriginalFileName = originalFileName,

                DeclaredMimeType = declaredMimeType

            };

            var result = new FileModerationResult

            {

                OriginalFileName = originalFileName,

                SanitizedFileName = SanitizeFileName(originalFileName),

                IsApproved = false

            };

            // 1. Kiểm tra tồn tại và dung lượng tệp

            if (!File.Exists(stagedFilePath))

            {

                result.ErrorCode = "FILE_NOT_FOUND";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = "Không tìm thấy tệp tin được tải lên.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                return result;

            }

            var fileInfo = new FileInfo(stagedFilePath);

            result.FileSizeBytes = fileInfo.Length;

            audit.FileSizeBytes = fileInfo.Length;

            if (fileInfo.Length <= 0)

            {

                result.ErrorCode = "FILE_EMPTY";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = "Tệp đính kèm rỗng (0 bytes). Vui lòng chọn tệp hợp lệ.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            string ext = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? "";

            result.DetectedExtension = ext;

            audit.DetectedExtension = ext;

            bool isImageExt = AllowedImageExtensions.Contains(ext);

            bool isDocExt = AllowedDocExtensions.Contains(ext);

            result.IsImage = isImageExt || isExplicitImage;

            audit.IsImage = result.IsImage;

            // Kiểm tra giới hạn dung lượng

            long maxAllowedSize = result.IsImage ? MaxSingleImageSizeBytes : MaxSingleDocSizeBytes;

            if (fileInfo.Length > maxAllowedSize)

            {

                result.ErrorCode = "FILE_TOO_LARGE";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                string limitStr = result.IsImage ? "10MB" : "15MB";

                result.UserFriendlyMessage = $"Tệp '{originalFileName}' vượt quá dung lượng tối đa cho phép ({limitStr}).";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            // 2. Chặn định dạng nguy hiểm trong Blacklist

            if (DangerousExtensions.Contains(ext))

            {

                result.ErrorCode = "EXTENSION_PROHIBITED";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"Tệp '{originalFileName}' có phần mở rộng nguy hiểm ({ext}) và bị từ chối vì lý do an toàn bảo mật hệ thống.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            // 3. Chặn tấn công phần mở rộng kép (Double Extension Detection)

            if (HasDangerousDoubleExtension(originalFileName, out string dangerousPart))

            {

                result.ErrorCode = "DOUBLE_EXTENSION_DETECTED";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"Tệp '{originalFileName}' bị từ chối do chứa phần mở rộng kép nguy hiểm ({dangerousPart}).";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            // Kiểm tra danh sách hợp lệ

            if (!isImageExt && !isDocExt)

            {

                result.ErrorCode = "UNSUPPORTED_EXTENSION";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"Định dạng tệp '{ext}' không được hỗ trợ. Vui lòng chọn tệp ảnh hoặc tài liệu cho phép.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = result.UserFriendlyMessage;

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            // 4. Tính mã băm SHA-256

            result.FileSha256 = ComputeFileSha256(stagedFilePath);

            audit.Sha256 = result.FileSha256;

            // 5. Kiểm tra chữ ký tệp thực tế (Magic Bytes Verification)

            var sigCheck = VerifyFileSignature(stagedFilePath, ext);

            audit.SignatureCheckStatus = sigCheck.isValid ? $"PASS ({sigCheck.detectedType})" : $"FAIL ({sigCheck.detectedType})";

            if (!sigCheck.isValid)

            {

                if (sigCheck.detectedType == "MALWARE_DETECTED")

                {

                    result.ErrorCode = "MALWARE_DETECTED";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                    result.UserFriendlyMessage = $"CẢNH BÁO: Tệp '{originalFileName}' bị phát hiện chứa mã độc/phần mềm nguy hại và đã bị chặn bởi hệ thống phòng vệ.";

                    audit.AntivirusStatus = "THREAT_DETECTED (Blocked by Windows Defender Realtime Protection)";

                    audit.FinalDecision = "REJECTED";

                    audit.DecisionReason = sigCheck.reason;

                    LogModerationAudit(audit);

                    SafeDeleteFile(stagedFilePath);

                    return result;

                }

                result.ErrorCode = "SIGNATURE_MISMATCH";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"Tệp '{originalFileName}' bị từ chối: {sigCheck.reason}";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = $"Signature check failed: {sigCheck.reason}";

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            result.DetectedMimeType = sigCheck.detectedType;

            // 6. Quét mã độc bằng Antivirus thực tế (Windows Defender)

            var avResult = ScanFileWithAntivirus(stagedFilePath);

            result.AntivirusStatus = avResult.status;

            result.AntivirusDetails = avResult.details;

            audit.AntivirusStatus = $"{avResult.status} ({avResult.details})";

            if (avResult.status == "THREAT_DETECTED")

            {

                result.ErrorCode = "MALWARE_DETECTED";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"CẢNH BÁO: Tệp '{originalFileName}' bị phát hiện chứa mã độc/phần mềm nguy hại và đã bị chặn bởi hệ thống phòng vệ.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = "Antivirus detected threat!";

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

            else if (avResult.status == "SCAN_FAILED")

            {

                result.ErrorCode = "SCAN_SERVICE_FAILED";
                result.ImageModerationStatus = ModerationStatuses.LocalRejected;

                result.UserFriendlyMessage = $"Hệ thống không thể hoàn tất quét mã độc cho tệp '{originalFileName}' ({avResult.details}). Tệp bị từ chối để bảo đảm an toàn.";

                audit.FinalDecision = "REJECTED";

                audit.DecisionReason = $"Antivirus scan failed: {avResult.details}";

                LogModerationAudit(audit);

                SafeDeleteFile(stagedFilePath);

                return result;

            }

                        // 7. Kiểm duyệt nội dung ngữ nghĩa bằng Gemini API kết hợp các lớp bảo vệ kỹ thuật cục bộ
            if (result.IsImage)
            {
                var aiMod = await ModerateImageWithAiAsync(stagedFilePath, result.DetectedMimeType, result.FileSha256);
                result.ImageModerationStatus = aiMod.status;
                result.ImageModerationReason = aiMod.reason;
                audit.ImageModerationStatus = $"{aiMod.status} ({aiMod.reason})";

                if (aiMod.status == ModerationStatuses.AiRejected)
                {
                    result.IsApproved = false;
                    result.ErrorCode = "AI_CONTENT_VIOLATION";
                    result.UserFriendlyMessage = $"Hình ảnh bị từ chối do vi phạm quy chuẩn nội dung cộng đồng: {aiMod.reason}";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI detected content policy violation: {aiMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
            }
            else if (ext == ".txt" || ext == ".csv")
            {
                var textMod = await ModerateDocumentTextWithAiAsync(stagedFilePath, result.FileSha256);
                result.ImageModerationStatus = textMod.status;
                result.ImageModerationReason = textMod.reason;
                audit.ImageModerationStatus = $"{textMod.status} (Text Content: {textMod.reason})";

                if (textMod.status == ModerationStatuses.AiRejected)
                {
                    result.IsApproved = false;
                    result.ErrorCode = "AI_CONTENT_VIOLATION";
                    result.UserFriendlyMessage = $"Tệp văn bản bị từ chối do vi phạm quy chuẩn nội dung cộng đồng: {textMod.reason}";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI detected text content policy violation: {textMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
            }
            else
            {
                // Đối với tệp tài liệu nhị phân (.pdf, .docx, .xlsx, .pptx, .zip, .rar, .7z):
                // Tuyệt đối không gửi nhị phân tùy ý lên AI để bảo vệ dữ liệu và tiết kiệm tài nguyên.
                // Tệp đã được bảo vệ toàn diện qua chữ ký Magic Bytes, quét mã độc, và kiểm tra cấu trúc lưu trữ.
                result.ImageModerationStatus = ModerationStatuses.NotApplicable;
                result.ImageModerationReason = "Tệp tài liệu kỹ thuật được bảo vệ bởi bộ lọc chữ ký, phòng chống Zip Bomb/Slip/Macro và trình quét Antivirus cục bộ.";
                audit.ImageModerationStatus = "NOT_APPLICABLE (Protected by Local Technical Checks)";
            }

            // 8. TẤT CẢ CÁC BƯỚC ĐÃ VƯỢT QUA -> DUYỆT TỆP
            result.IsApproved = true;
            result.UserFriendlyMessage = "Tệp hợp lệ và đã qua kiểm duyệt an toàn.";
            audit.FinalDecision = "APPROVED";
            audit.DecisionReason = result.ImageModerationStatus == ModerationStatuses.AiApproved
                ? "Technical security checks passed and content verified safe by Gemini AI."
                : $"Technical security checks passed (Content moderation status: {result.ImageModerationStatus}).";

            LogModerationAudit(audit);
            return result;

        }

        /// <summary>

        /// Đọc các dòng nhật ký kiểm duyệt gần nhất phục vụ kiểm toán và chẩn đoán Admin

        /// </summary>

        public static List<string> GetRecentAuditLogLines(int maxLines = 100)

        {

            var lines = new List<string>();

            try

            {

                string logDir = HttpContext.Current != null

                    ? HttpContext.Current.Server.MapPath("~/App_Data/Logs/ChatModeration")

                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs", "ChatModeration");

                if (!Directory.Exists(logDir)) return lines;

                var logFiles = Directory.GetFiles(logDir, "ChatFileModeration_*.log")

                                        .OrderByDescending(f => f)

                                        .ToList();

                foreach (var file in logFiles)

                {

                    var fileLines = File.ReadAllLines(file, Encoding.UTF8).Reverse();

                    foreach (var line in fileLines)

                    {

                        if (!string.IsNullOrWhiteSpace(line))

                        {

                            lines.Add(line);

                            if (lines.Count >= maxLines) return lines;

                        }

                    }

                }

            }

            catch { }

            return lines;

        }

        private static void SafeDeleteFile(string path)

        {

            try

            {

                if (File.Exists(path)) File.Delete(path);

            }

            catch { }

        }

    }

}

