using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StradigBlog.Data;

public class HomeController : Controller
{
    private readonly BlogDbContext _context;

    public HomeController(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var posts = await _context.Posts
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedDate)
            .Take(5)
            .ToListAsync();

        return View(posts);
    }
}