using Microsoft.AspNetCore.Identity;

namespace PromoRadar.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "Luiz";

    public string AvatarInitials { get; set; } = "LZ";

    public string PlanName { get; set; } = "Plano Gratuito";
}

