using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Newtonsoft.Json;
using StudentConnect.Models;

namespace StudentConnect.Helpers
{
    public static class StoryHelper
    {
        private static readonly object _fileLock = new object();

        private static string GetStorageDir()
        {
            string path = HostingEnvironment.MapPath("~/App_Data/StoryData");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        // =========================================================================
        // STORY HIGHLIGHTS (TIN NỔI BẬT)
        // =========================================================================

        public static List<StoryHighlightViewModel> GetHighlights(int userId)
        {
            lock (_fileLock)
            {
                string file = Path.Combine(GetStorageDir(), $"highlights_{userId}.json");
                if (!File.Exists(file)) return new List<StoryHighlightViewModel>();

                try
                {
                    string json = File.ReadAllText(file);
                    return JsonConvert.DeserializeObject<List<StoryHighlightViewModel>>(json) ?? new List<StoryHighlightViewModel>();
                }
                catch
                {
                    return new List<StoryHighlightViewModel>();
                }
            }
        }

        public static StoryHighlightViewModel SaveHighlight(int userId, CreateHighlightInputModel model)
        {
            lock (_fileLock)
            {
                var list = GetHighlights(userId);
                string highlightId = string.IsNullOrWhiteSpace(model.HighlightID)
                    ? Guid.NewGuid().ToString("N").Substring(0, 10)
                    : model.HighlightID;

                var existing = list.FirstOrDefault(h => h.HighlightID == highlightId);
                if (existing != null)
                {
                    existing.Title = model.Title?.Trim();
                    existing.CoverUrl = model.CoverUrl;
                    existing.StoryIDs = model.StoryIDs ?? new List<int>();
                }
                else
                {
                    existing = new StoryHighlightViewModel
                    {
                        HighlightID = highlightId,
                        UserID = userId,
                        Title = model.Title?.Trim() ?? "Tin nổi bật",
                        CoverUrl = model.CoverUrl,
                        CreatedAt = DateTime.Now,
                        StoryIDs = model.StoryIDs ?? new List<int>()
                    };
                    list.Insert(0, existing);
                }

                string file = Path.Combine(GetStorageDir(), $"highlights_{userId}.json");
                File.WriteAllText(file, JsonConvert.SerializeObject(list, Formatting.Indented));
                return existing;
            }
        }

        public static bool DeleteHighlight(int userId, string highlightId)
        {
            lock (_fileLock)
            {
                var list = GetHighlights(userId);
                int removed = list.RemoveAll(h => h.HighlightID == highlightId);
                if (removed > 0)
                {
                    string file = Path.Combine(GetStorageDir(), $"highlights_{userId}.json");
                    File.WriteAllText(file, JsonConvert.SerializeObject(list, Formatting.Indented));
                    return true;
                }
                return false;
            }
        }

        public static StoryHighlightViewModel FindHighlight(string highlightId)
        {
            lock (_fileLock)
            {
                var dir = GetStorageDir();
                var files = Directory.GetFiles(dir, "highlights_*.json");
                foreach (var f in files)
                {
                    try
                    {
                        string json = File.ReadAllText(f);
                        var list = JsonConvert.DeserializeObject<List<StoryHighlightViewModel>>(json);
                        var found = list?.FirstOrDefault(h => h.HighlightID == highlightId);
                        if (found != null) return found;
                    }
                    catch { }
                }
                return null;
            }
        }

        // =========================================================================
        // STORY REACTIONS (CẢM XÚC TRÊN TIN)
        // =========================================================================

        public static void SaveReaction(int storyId, int userId, string username, string avatar, string emoji)
        {
            lock (_fileLock)
            {
                string file = Path.Combine(GetStorageDir(), $"reactions_{storyId}.json");
                List<StoryReactionModel> list = new List<StoryReactionModel>();
                if (File.Exists(file))
                {
                    try
                    {
                        list = JsonConvert.DeserializeObject<List<StoryReactionModel>>(File.ReadAllText(file)) ?? new List<StoryReactionModel>();
                    }
                    catch { }
                }

                list.RemoveAll(r => r.UserID == userId);
                list.Add(new StoryReactionModel
                {
                    StoryID = storyId,
                    UserID = userId,
                    Username = username,
                    Avatar = avatar,
                    Emoji = emoji,
                    ReactedAt = DateTime.Now,
                    TimeAgo = "Vừa xong"
                });

                File.WriteAllText(file, JsonConvert.SerializeObject(list, Formatting.Indented));
            }
        }

        public static List<StoryReactionModel> GetReactions(int storyId)
        {
            lock (_fileLock)
            {
                string file = Path.Combine(GetStorageDir(), $"reactions_{storyId}.json");
                if (!File.Exists(file)) return new List<StoryReactionModel>();

                try
                {
                    var list = JsonConvert.DeserializeObject<List<StoryReactionModel>>(File.ReadAllText(file)) ?? new List<StoryReactionModel>();
                    foreach (var item in list)
                    {
                        item.TimeAgo = SocialHelper.FormatTimeAgo(item.ReactedAt);
                    }
                    return list;
                }
                catch
                {
                    return new List<StoryReactionModel>();
                }
            }
        }

        public static string GetUserReaction(int storyId, int userId)
        {
            var reactions = GetReactions(storyId);
            return reactions.FirstOrDefault(r => r.UserID == userId)?.Emoji;
        }
    }
}
