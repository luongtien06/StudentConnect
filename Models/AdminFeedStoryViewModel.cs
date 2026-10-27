using System.Collections.Generic;

namespace StudentConnect.Models
{
    public class AdminFeedStoryViewModel
    {
        public int TotalPosts { get; set; }
        public int TotalStories { get; set; }
        public int StoriesActive { get; set; }
        public int StoriesArchived { get; set; }
        public List<Post> Posts { get; set; } = new List<Post>();
        public List<Story> Stories { get; set; } = new List<Story>();
    }
}