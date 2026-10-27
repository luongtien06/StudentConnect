using System;

namespace StudentConnect.Helpers
{
    /// <summary>
    /// Các hằng số và tiện ích tạo placeholder ảnh mặc định với gradient Xanh Navy (#0B1026) – Xanh đậm (#172554) – Tím (#6D28D9)
    /// Sử dụng hoàn toàn CSS / SVG Data-URI inline để triệt tiêu phụ thuộc tải file qua mạng, ngăn ngừa 404 và infinite loop.
    /// </summary>
    public static class DefaultImagePlaceholders
    {
        /// <summary>
        /// Avatar vector tròn/vuông gradient Navy - Tím với icon silhouette người dùng trang nhã
        /// </summary>
        public const string Avatar = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' width='100' height='100'><defs><linearGradient id='av_np' x1='0%25' y1='0%25' x2='100%25' y2='100%25'><stop offset='0%25' stop-color='%230B1026'/><stop offset='50%25' stop-color='%23172554'/><stop offset='100%25' stop-color='%236D28D9'/></linearGradient></defs><rect width='100' height='100' fill='url(%23av_np)'/><circle cx='50' cy='38' r='18' fill='rgba(255,255,255,0.78)'/><path d='M22 84 C22 66, 34 58, 50 58 C66 58, 78 66, 78 84 Z' fill='rgba(255,255,255,0.78)'/></svg>";

        /// <summary>
        /// Ảnh bìa vector gradient Navy - Tím với hiệu ứng ánh sáng và họa tiết TDMU Connect
        /// </summary>
        public const string Cover = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 1200 400' width='1200' height='400'><defs><linearGradient id='cov_np' x1='0%25' y1='0%25' x2='100%25' y2='100%25'><stop offset='0%25' stop-color='%230B1026'/><stop offset='45%25' stop-color='%23172554'/><stop offset='100%25' stop-color='%236D28D9'/></linearGradient><radialGradient id='cov_glow' cx='85%25' cy='25%25' r='50%25'><stop offset='0%25' stop-color='%238B5CF6' stop-opacity='0.35'/><stop offset='100%25' stop-color='%236D28D9' stop-opacity='0'/></radialGradient></defs><rect width='1200' height='400' fill='url(%23cov_np)'/><rect width='1200' height='400' fill='url(%23cov_glow)'/><g opacity='0.15' stroke='%23FFF' stroke-width='1.5' fill='none'><circle cx='1050' cy='200' r='160'/><circle cx='1050' cy='200' r='100'/><path d='M0 320 Q 300 240, 600 320 T 1200 280'/></g><text x='50%25' y='50%25' fill='rgba(255,255,255,0.45)' font-family='sans-serif' font-size='26' font-weight='600' text-anchor='middle' dy='.3em' letter-spacing='4'>TDMU CONNECT</text></svg>";

        /// <summary>
        /// Ảnh Chợ / Marketplace vector gradient Navy - Tím với icon giỏ hàng mua sắm tinh tế
        /// </summary>
        public const string Market = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 600 600' width='600' height='600'><defs><linearGradient id='mkt_np' x1='0%25' y1='0%25' x2='100%25' y2='100%25'><stop offset='0%25' stop-color='%230B1026'/><stop offset='50%25' stop-color='%23172554'/><stop offset='100%25' stop-color='%236D28D9'/></linearGradient></defs><rect width='600' height='600' fill='url(%23mkt_np)'/><g fill='none' stroke='rgba(255,255,255,0.68)' stroke-width='14' stroke-linecap='round' stroke-linejoin='round' transform='translate(180, 160)'><path d='M40 80 L200 80 L180 260 L60 260 Z'/><path d='M85 80 C85 30, 155 30, 155 80'/></g><text x='50%25' y='76%25' fill='rgba(255,255,255,0.5)' font-family='sans-serif' font-size='22' font-weight='500' text-anchor='middle' letter-spacing='2'>TDMU MARKET</text></svg>";

        /// <summary>
        /// Ảnh bài viết / Tin tức / Kết nối vector gradient Navy - Tím với icon ảnh phong cảnh hiện đại
        /// </summary>
        public const string Post = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 800 500' width='800' height='500'><defs><linearGradient id='post_np' x1='0%25' y1='0%25' x2='100%25' y2='100%25'><stop offset='0%25' stop-color='%230B1026'/><stop offset='50%25' stop-color='%23172554'/><stop offset='100%25' stop-color='%236D28D9'/></linearGradient></defs><rect width='800' height='500' fill='url(%23post_np)'/><g fill='none' stroke='rgba(255,255,255,0.68)' stroke-width='12' stroke-linecap='round' stroke-linejoin='round' transform='translate(300, 150)'><rect x='0' y='0' width='200' height='150' rx='16'/><circle cx='55' cy='50' r='18' fill='rgba(255,255,255,0.68)' stroke='none'/><path d='M20 130 L80 75 L130 115 L160 90 L185 130'/></g><text x='50%25' y='78%25' fill='rgba(255,255,255,0.45)' font-family='sans-serif' font-size='20' font-weight='500' text-anchor='middle' letter-spacing='2'>STUDENT CONNECT</text></svg>";

        // Đường dẫn file ảnh tĩnh trên server để tương thích ngược
        public const string AvatarPath = "/Content/Images/default-avatar.png";
        public const string CoverPath = "/Content/Images/default-cover.jpg";
        public const string MarketPath = "/Content/Images/default-market.jpg";
        public const string PostImagePath = "/Content/Images/default-image.jpg";

        public static string ResolveAvatar(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar))
                return Avatar;
            return avatar;
        }

        public static string ResolveCover(string cover)
        {
            if (string.IsNullOrWhiteSpace(cover))
                return Cover;
            return cover;
        }

        public static string ResolveMarket(string market)
        {
            if (string.IsNullOrWhiteSpace(market))
                return Market;
            return market;
        }

        public static string ResolvePostImage(string image)
        {
            if (string.IsNullOrWhiteSpace(image))
                return Post;
            return image;
        }
    }
}
