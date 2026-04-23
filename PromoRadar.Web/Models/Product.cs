namespace PromoRadar.Web.Models;

public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string NormalizedCategory { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal BaselinePrice { get; set; }

    public ICollection<UserTrackedProduct> TrackedProducts { get; set; } = new List<UserTrackedProduct>();
}

