using System.ComponentModel.DataAnnotations;
using PromoRadar.Web.Models.Enums;

namespace PromoRadar.Web.ViewModels;

public class TrackedProductPreferencesViewModel
{
    [Required(ErrorMessage = "Informe o preço alvo.")]
    [Display(Name = "Preço alvo (R$)")]
    public string TargetPrice { get; set; } = string.Empty;

    [Display(Name = "Preço máximo (opcional)")]
    public string? MaximumPrice { get; set; }

    [Display(Name = "Receber alertas quando o preço estiver")]
    public PriceAlertTrigger AlertTrigger { get; set; } = PriceAlertTrigger.BelowTarget;

    [Display(Name = "Alertas por e-mail")]
    public bool EmailAlerts { get; set; } = true;

    [Display(Name = "Notificações push")]
    public bool PushNotifications { get; set; } = true;

    [Display(Name = "Resumo diário")]
    public bool DailySummary { get; set; }
}
