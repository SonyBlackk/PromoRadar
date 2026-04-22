using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class PriceHistoryController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "history";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Histórico de Preços",
            Description = "Explore variações por período e descubra janelas ideais para compra em cada categoria.",
            PrimaryActionText = "Comparar períodos",
            PrimaryActionUrl = "#"
        });
    }
}
