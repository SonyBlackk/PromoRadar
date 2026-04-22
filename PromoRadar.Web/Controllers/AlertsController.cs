using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class AlertsController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "alerts";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Alertas",
            Description = "Gerencie regras de alerta por faixa de preço, queda percentual e estoque disponível.",
            PrimaryActionText = "Criar novo alerta",
            PrimaryActionUrl = "#"
        });
    }
}
