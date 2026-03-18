using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StradigBlog.Data;
using StradigBlog.Models;
using StradigBlog.Models.ViewModels;

namespace StradigBlog.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly BlogDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public DashboardController(BlogDbContext context, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var posts = await _context.Posts
                .Include(p => p.Category)
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            var totalPosts = posts.Count;
            var totalCategories = await _context.Categories.CountAsync();
            var latestPost = posts.FirstOrDefault();

            ViewBag.TotalPosts = totalPosts;
            ViewBag.TotalCategories = totalCategories;
            ViewBag.LatestPost = latestPost;
            ViewBag.UserEmail = user.Email;
            ViewBag.UserName = user.UserName;

            return View(posts);
        }

        // GET: Dashboard/EditProfile
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var model = new EditProfileViewModel
            {
                Email = user.Email ?? "",
                UserName = user.UserName ?? ""
            };

            return View(model);
        }

        // POST: Dashboard/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // Update username
            if (user.UserName != model.UserName)
            {
                var setUserName = await _userManager.SetUserNameAsync(user, model.UserName);
                if (!setUserName.Succeeded)
                {
                    foreach (var error in setUserName.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            // Update email
            if (user.Email != model.Email)
            {
                var setEmail = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmail.Succeeded)
                {
                    foreach (var error in setEmail.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            // Change password if provided
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (string.IsNullOrEmpty(model.CurrentPassword))
                {
                    ModelState.AddModelError(string.Empty, "Current password is required to set a new password.");
                    return View(model);
                }

                var changePassword = await _userManager.ChangePasswordAsync(
                    user, model.CurrentPassword, model.NewPassword);

                if (!changePassword.Succeeded)
                {
                    foreach (var error in changePassword.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            // Refresh the auth cookie so the updated username/email/security-stamp
            // takes effect immediately and the user can still log in after changes.
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Dashboard/DeletePost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var post = await _context.Posts.FindAsync(id);

            if (post == null) return NotFound();
            if (post.UserId != user?.Id) return Forbid();

            if (!string.IsNullOrEmpty(post.ImagePath) && post.ImagePath.StartsWith("/uploads/"))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", post.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                    System.IO.File.Delete(imagePath);
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Post deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}