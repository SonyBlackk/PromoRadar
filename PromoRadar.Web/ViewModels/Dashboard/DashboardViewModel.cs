namespace PromoRadar.Web.ViewModels.Dashboard;

public class DashboardViewModel
{
    public string GreetingName { get; set; } = "Luiz";

    public string GreetingSubtitle { get; set; } = "Aqui estão as melhores oportunidades para você hoje.";

    public IReadOnlyList<SummaryCardViewModel> SummaryCards { get; set; } = [];

    public FeaturedProductViewModel FeaturedProduct { get; set; } = new();

    public IReadOnlyList<RecentAlertViewModel> RecentAlerts { get; set; } = [];

    public DaySummaryViewModel DaySummary { get; set; } = new();

    public IReadOnlyList<StoreScoreViewModel> StoreScores { get; set; } = [];

    public IReadOnlyList<SuggestionItemViewModel> Suggestions { get; set; } = [];
}

public class SummaryCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Icon { get; set; } = "bi-grid";

    public string Value { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string AccentClass { get; set; } = "accent-indigo";
}

public class FeaturedProductViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Store { get; set; } = string.Empty;

    public string StoreBadge { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal TargetPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal DeltaPercent { get; set; }

    public string DeltaLabel { get; set; } = string.Empty;

    public string LastUpdatedLabel { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, IReadOnlyList<decimal>> PriceSeriesByPeriod { get; set; } =
        new Dictionary<string, IReadOnlyList<decimal>>();

    public IReadOnlyList<string> LabelsByPeriod7d { get; set; } = [];
}

public class RecentAlertViewModel
{
    public string ProductName { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Note { get; set; } = string.Empty;

    public string TimeAgo { get; set; } = string.Empty;

    public string Icon { get; set; } = "bi-bell";

    public string AccentClass { get; set; } = "positive";
}

public class DaySummaryViewModel
{
    public int TotalVariations { get; set; }

    public int DownCount { get; set; }

    public int UpCount { get; set; }

    public int StableCount { get; set; }

    public string DateLabel { get; set; } = string.Empty;
}

public class StoreScoreViewModel
{
    public string StoreName { get; set; } = string.Empty;

    public string IconText { get; set; } = string.Empty;

    public int Score { get; set; }
}

public class SuggestionItemViewModel
{
    public string Name { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string BadgeText { get; set; } = string.Empty;

    public string BadgeClass { get; set; } = "good";

    public string ComparisonText { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public IReadOnlyList<decimal> SparklinePoints { get; set; } = [];
}

