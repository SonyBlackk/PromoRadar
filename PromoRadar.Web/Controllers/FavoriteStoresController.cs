using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class FavoriteStoresController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "stores";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Lojas Preferidas",
            Description = "Priorize lojas confiáveis e acompanhe score de competitividade no seu radar.",
            PrimaryActionText = "Adicionar loja favorita",
            PrimaryActionUrl = "#"
        });
    }
}
