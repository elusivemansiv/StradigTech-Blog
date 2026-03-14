using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace StradigBlog.Models
{
    public class Post
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public string? ImagePath { get; set; }

        // Track who created the post
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }
    }
}
