namespace PromoRadar.Web.ViewModels;

public class CreateTrackedProductStep3ViewModel
{
    public string? SearchTerm { get; set; }

    public List<TrackedStoreSelectionItemViewModel> Stores { get; set; } = [];
}

public class TrackedStoreSelectionItemViewModel
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string StoreType { get; set; } = string.Empty;

    public bool IsRecommended { get; set; }

    public bool IsSelected { get; set; }

    public string LogoText { get; set; } = string.Empty;

    public string LogoVariant { get; set; } = "neutral";
}
