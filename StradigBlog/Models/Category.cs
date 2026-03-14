using System.Collections.Generic;

namespace StradigBlog.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        // Navigation property
        public ICollection<Post>? Posts { get; set; }
    }
}