path = r"E:\LTWEB\StudentConnect\Helpers\ChatFileModerationHelper.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# Normalize line endings for replacement
content = content.replace("\r\n", "\n")

if "using System.IO.Compression;" not in content:
    content = content.replace("using System.IO;\n", "using System.IO;\nusing System.IO.Compression;\n")

archive_method = '''        private static DateTime _lastCleanupUtc = DateTime.MinValue;
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
                                entryName.StartsWith("\\\\") ||
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
'''

if "public static (bool isValid, string detectedType, string reason) VerifyZipAndOfficeArchive" not in content:
    target = "        /// <summary>\n        /// Tìm đường dẫn thực thi của Windows Defender"
    idx = content.find(target)
    if idx > 0:
        content = content[:idx] + archive_method + "\n" + content[idx:]
        print("Inserted archive_method")
    else:
        print("FAILED to find target for archive_method")

# Convert back to CRLF
content = content.replace("\n", "\r\n")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated Helpers/ChatFileModerationHelper.cs successfully")
