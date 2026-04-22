namespace PromoRadar.Web.Models;

public class Store
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#5b57f3";

    public ICollection<UserTrackedProduct> TrackedProducts { get; set; } = new List<UserTrackedProduct>();
}

