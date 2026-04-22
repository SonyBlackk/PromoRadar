using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.Models;
using PromoRadar.Web.Services;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDashboardService _dashboardService;

    public HomeController(UserManager<ApplicationUser> userManager, IDashboardService dashboardService)
    {
        _userManager = userManager;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        ViewData["ActiveNav"] = "home";
        var vm = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
