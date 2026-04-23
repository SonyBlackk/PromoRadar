using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class MyProductsController : Controller
{
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "my-products";
        return View("~/Views/Modules/Index.cshtml", new SectionPageViewModel
        {
            Title = "Minhas Mercadorias",
            Description = "Aqui você verá todos os produtos monitorados, metas de preço e performance por loja.",
            PrimaryActionText = "Monitorar nova mercadoria",
            PrimaryActionUrl = "/TrackedProducts/Create"
        });
    }
}
