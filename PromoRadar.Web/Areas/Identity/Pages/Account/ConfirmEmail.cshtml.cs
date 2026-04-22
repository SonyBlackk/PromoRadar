using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using PromoRadar.Web.Models;

namespace PromoRadar.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public bool IsSuccess { get; set; }

    public string Message { get; set; } = "Não foi possível confirmar o e-mail.";

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (userId is null || code is null)
        {
            Message = "Parâmetros inválidos para confirmação.";
            return Page();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            Message = "Usuário não encontrado.";
            return Page();
        }

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, decoded);

        IsSuccess = result.Succeeded;
        Message = result.Succeeded
            ? "E-mail confirmado com sucesso."
            : "Não foi possível confirmar o e-mail.";

        return Page();
    }
}
