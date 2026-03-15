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
        try 
        {
            var posts = await _context.Posts
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedDate)
                .Take(5)
                .ToListAsync();

            return View(posts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HOME PAGE ERROR: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"INNER ERROR: {ex.InnerException.Message}");
            throw; // Re-throw so DeveloperExceptionPage catches it
        }
    }
}