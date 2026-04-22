using PromoRadar.Web.Models.Enums;

namespace PromoRadar.Web.Models;

public class PriceAlert
{
    public Guid Id { get; set; }

    public Guid UserTrackedProductId { get; set; }

    public decimal TriggerPrice { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserTrackedProduct? UserTrackedProduct { get; set; }
}

