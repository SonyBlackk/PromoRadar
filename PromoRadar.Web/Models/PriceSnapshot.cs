namespace PromoRadar.Web.Models;

public class PriceSnapshot
{
    public Guid Id { get; set; }

    public Guid UserTrackedProductId { get; set; }

    public decimal Price { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public UserTrackedProduct? UserTrackedProduct { get; set; }
}

