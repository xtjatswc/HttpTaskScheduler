using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using HttpTaskScheduler.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace HttpTaskScheduler.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllLogs()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null || !user.IsAdmin)
        {
            TempData["ErrorMessage"] = "Insufficient permissions. Only administrators can clear logs.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // Log the operation
            _logger.LogInformation("User {UserName} started clearing all execution logs", User.Identity?.Name);

            // 1. Clear all table data (using EF Core's ExecuteDeleteAsync method)
            await _context.TaskExecutionLogs.ExecuteDeleteAsync();

            // 2. Reset auto-increment counter
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM sqlite_sequence WHERE name = 'TaskExecutionLogs'");

            // 3. Reclaim database space (execute VACUUM command)
            await _context.Database.ExecuteSqlRawAsync("VACUUM");
            
            _logger.LogInformation("All execution logs have been cleared");
            TempData["SuccessMessage"] = "All execution logs have been successfully cleared";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear execution logs");
            TempData["ErrorMessage"] = $"Failed to clear logs: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
