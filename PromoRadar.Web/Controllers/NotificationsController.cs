using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "notifications";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Notificações",
            Description = "Central de notificações com eventos de preço, oportunidades e mudanças de estoque.",
            PrimaryActionText = "Configurar canais",
            PrimaryActionUrl = "#"
        });
    }
}
