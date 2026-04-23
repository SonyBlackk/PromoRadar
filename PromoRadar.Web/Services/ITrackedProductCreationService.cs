using PromoRadar.Web.Models.Enums;

namespace PromoRadar.Web.Services;

public interface ITrackedProductCreationService
{
    Task<TrackedProductCreationResult> CreateAsync(
        string userId,
        TrackedProductCreationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class TrackedProductCreationRequest
{
    public string ProductName { get; init; } = string.Empty;

    public string ProductCategory { get; init; } = string.Empty;

    public string ProductImageUrl { get; init; } = "/images/products/default.svg";

    public decimal TargetPrice { get; init; }

    public decimal? MaximumPrice { get; init; }

    public PriceAlertTrigger AlertTrigger { get; init; } = PriceAlertTrigger.BelowTarget;

    public bool EmailAlerts { get; init; } = true;

    public bool PushNotifications { get; init; } = true;

    public bool DailySummary { get; init; }

    public IReadOnlyCollection<TrackedStoreRequest> Stores { get; init; } = [];
}

public sealed class TrackedStoreRequest
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string AccentColor { get; init; } = "#5b57f3";
}

public sealed class TrackedProductCreationResult
{
    public int CreatedCount { get; init; }
}
