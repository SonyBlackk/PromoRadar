namespace PromoRadar.Web.ViewModels;

public class CreateTrackedProductReviewViewModel
{
    public string ProductName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string ProductUrl { get; set; } = string.Empty;

    public string ProductImageUrl { get; set; } = "/images/products/default.svg";

    public decimal TargetPrice { get; set; }

    public decimal? MaximumPrice { get; set; }

    public IReadOnlyList<ReviewAlertItemViewModel> ConfiguredAlerts { get; set; } = [];

    public IReadOnlyList<TrackedStoreSelectionItemViewModel> SelectedStores { get; set; } = [];
}

public class ReviewAlertItemViewModel
{
    public string Label { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
