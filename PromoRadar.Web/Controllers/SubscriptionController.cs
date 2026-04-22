using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class SubscriptionController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "subscription";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Assinatura",
            Description = "Gerencie seu plano, benefícios premium e limites de monitoramento.",
            PrimaryActionText = "Ver planos",
            PrimaryActionUrl = "#"
        });
    }
}
