namespace PromoRadar.Web.ViewModels;

public class MyProductsPageViewModel
{
    public string Title { get; set; } = "Minhas Mercadorias";

    public string Subtitle { get; set; } =
        "Aqui você vê todos os produtos monitorados, metas de preço e performance por loja.";

    public string Search { get; set; } = string.Empty;

    public string? SelectedStore { get; set; }

    public string? SelectedCategory { get; set; }

    public string SelectedSort { get; set; } = "recent";

    public IReadOnlyList<MyProductsFilterOptionViewModel> StoreOptions { get; set; } = [];

    public IReadOnlyList<MyProductsFilterOptionViewModel> CategoryOptions { get; set; } = [];

    public IReadOnlyList<MyProductsFilterOptionViewModel> SortOptions { get; set; } = [];

    public IReadOnlyList<MyProductsKpiCardViewModel> KpiCards { get; set; } = [];

    public IReadOnlyList<MyProductItemViewModel> Products { get; set; } = [];

    public int ShowingFrom { get; set; }

    public int ShowingTo { get; set; }

    public int TotalProducts { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; } = 1;
}

public class MyProductsKpiCardViewModel
{
    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string AccentClass { get; set; } = "indigo";
}

public class MyProductItemViewModel
{
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string CategoryClass { get; set; } = "default";

    public string ImageUrl { get; set; } = "/images/products/default.svg";

    public decimal CurrentPrice { get; set; }

    public decimal TargetPrice { get; set; }

    public decimal BestPrice { get; set; }

    public string BestStoreName { get; set; } = string.Empty;

    public string BestStoreLogoText { get; set; } = string.Empty;

    public string BestStoreLogoClass { get; set; } = "default";

    public decimal VariationPercent { get; set; }

    public string VariationLabel => VariationPercent.ToString("+0.00;-0.00");

    public string VariationClass => VariationPercent <= 0 ? "is-positive" : "is-negative";

    public bool IsTrendNegative { get; set; }

    public DateTime LatestCapturedAtUtc { get; set; }

    public int UnreadAlertsCount { get; set; }

    public IReadOnlyList<MyProductStoreSummaryViewModel> MonitoredStores { get; set; } = [];

    public IReadOnlyList<decimal> TrendPoints { get; set; } = [];
}

public class MyProductsFilterOptionViewModel
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public class MyProductStoreSummaryViewModel
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
