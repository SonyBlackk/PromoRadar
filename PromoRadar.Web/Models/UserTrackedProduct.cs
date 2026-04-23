using PromoRadar.Web.Models.Enums;

namespace PromoRadar.Web.Models;

public class UserTrackedProduct
{
    public Guid Id { get; set; }

    public string ApplicationUserId { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public Guid StoreId { get; set; }

    public decimal TargetPrice { get; set; }

    public decimal? MaximumPrice { get; set; }

    public PriceAlertTrigger AlertTrigger { get; set; } = PriceAlertTrigger.BelowTarget;

    public bool EmailAlertsEnabled { get; set; } = true;

    public bool PushNotificationsEnabled { get; set; } = true;

    public bool DailySummaryEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApplicationUser? ApplicationUser { get; set; }

    public Product? Product { get; set; }

    public Store? Store { get; set; }

    public ICollection<PriceSnapshot> PriceSnapshots { get; set; } = new List<PriceSnapshot>();

    public ICollection<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();
}
