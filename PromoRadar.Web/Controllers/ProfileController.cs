using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "profile";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Perfil",
            Description = "Atualize dados pessoais, preferências de monitoramento e configurações de conta.",
            PrimaryActionText = "Editar perfil",
            PrimaryActionUrl = "#"
        });
    }
}
