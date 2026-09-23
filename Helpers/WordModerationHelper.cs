using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StudentConnect.Helpers
{
    public class ModerationResult
    {
        public bool HasViolation { get; set; }
        public string CleanText { get; set; }
        public List<string> Violations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Bộ lọc và kiểm duyệt từ ngữ nhạy cảm / thô tục trong hệ thống trò chuyện
    /// </summary>
    public static class WordModerationHelper
    {
        // Cụm từ xúc phạm / thô tục ghép (ưu tiên lọc trước)
        private static readonly string[] InappropriatePhrases = new[]
        {
            "đụ má", "đụ mẹ", "du ma", "du me", "đụ mạ",
            "chó chết", "chó đẻ", "óc chó", "oc cho", "thằng chó", "đồ chó",
            "súc vật", "suc vat", "mẹ kiếp", "gái gọi", "bán dâm", "mua dâm",
            "thủ dâm", "ấu dâm", "khiêu dâm", "dâm đãng", "lăng loàn",
            "mother fucker", "motherfucker", "son of a bitch", "piece of shit"
        };

        // Từ đơn thô tục rõ ràng (không gây nhầm lẫn từ ngữ thông dụng)
        private static readonly string[] InappropriateWords = new[]
        {
            // Tiếng Việt viết tắt / tục tĩu
            "đm", "dm", "dmm", "đmm", "dcm", "đcm", "đkm", "dkm", "đcmm", "dcmm",
            "vcl", "vl", "vkl", "vcll", "clgt", "đệt",
            "cặc", "cak", "lồn", "lồz", "loz", "buồi",
            "đụ", "địt", "ditme", "đitmẹ", "địtme", "đéo", "duma", "sv", "lol",
            "cave",
            // Tiếng Anh
            "fuck", "fucking", "fucker", "bitch", "shit", "asshole",
            "cunt", "dick", "pussy", "bastard", "whore", "slut", "porno"
        };

        //  Mẫu lách luật có dấu chấm/khoảng trắng ở giữa (d.m, v.l, v_l, ...)
        private static readonly string[] MaskedPatterns = new[]
        {
            @"\b[đd][\.\s_-]+[m]\b",
            @"\b[đd][\.\s_-]+[c][\.\s_-]+[m]\b",
            @"\b[đd][\.\s_-]+[k][\.\s_-]+[m]\b",
            @"\b[v][\.\s_-]+[l]\b",
            @"\b[v][\.\s_-]+[c][\.\s_-]+[l]\b",
            @"\b[c][\.\s_-]+[aăâ][\.\s_-]+[ck]\b",
            @"\b[l][\.\s_-]+[oôõọ][\.\s_-]+[nz]\b",
            @"\b[f][\.\s_-]+[u][\.\s_-]+[c][\.\s_-]+[k]\b"
        };

        /// <summary>
        /// Kiểm duyệt nội dung tin nhắn: phát hiện từ vi phạm và thay thế bằng ***
        /// </summary>
        public static ModerationResult Moderate(string input)
        {
            var result = new ModerationResult
            {
                HasViolation = false,
                CleanText = input ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(input))
                return result;

            string processed = input;
            var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Quét cụm từ trước
            foreach (var phrase in InappropriatePhrases)
            {
                string pattern = @"(?i)\b" + Regex.Escape(phrase) + @"\b";
                if (Regex.IsMatch(processed, pattern))
                {
                    detected.Add(phrase);
                    processed = Regex.Replace(processed, pattern, m => MaskString(m.Value));
                }
            }

            // 2. Quét từ đơn
            foreach (var word in InappropriateWords)
            {
                string pattern = @"(?i)\b" + Regex.Escape(word) + @"\b";
                if (Regex.IsMatch(processed, pattern))
                {
                    detected.Add(word);
                    processed = Regex.Replace(processed, pattern, m => MaskString(m.Value));
                }
            }

            // 3. Quét mẫu lách luật (d.m, v.l, ...)
            foreach (var pattern in MaskedPatterns)
            {
                var matches = Regex.Matches(processed, pattern, RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    detected.Add(m.Value);
                }
                processed = Regex.Replace(processed, pattern, m => MaskString(m.Value), RegexOptions.IgnoreCase);
            }

            result.CleanText = processed;
            result.Violations = detected.ToList();
            result.HasViolation = detected.Count > 0;

            return result;
        }

        /// <summary>
        /// Kiểm tra nhanh xem nội dung có chứa từ ngữ vi phạm không
        /// </summary>
        public static bool ContainsViolation(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return Moderate(input).HasViolation;
        }

        /// <summary>
        /// Thay thế chuỗi bằng các ký tự dấu sao ***
        /// </summary>
        private static string MaskString(string word)
        {
            if (string.IsNullOrEmpty(word)) return "***";
            return new string('*', Math.Max(3, word.Length));
        }
    }
}
