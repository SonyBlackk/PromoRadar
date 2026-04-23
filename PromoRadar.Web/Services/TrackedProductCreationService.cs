using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;
using PromoRadar.Web.Models;

namespace PromoRadar.Web.Services;

public class TrackedProductCreationService : ITrackedProductCreationService
{
    private const string DefaultProductImageUrl = "/images/products/default.svg";

    private readonly ApplicationDbContext _dbContext;

    public TrackedProductCreationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TrackedProductCreationResult> CreateAsync(
        string userId,
        TrackedProductCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = request.ProductName.Trim();
        var normalizedCategory = request.ProductCategory.Trim();
        var normalizedNameLookup = ProductTextNormalizer.NormalizeLookupValue(normalizedName);
        var normalizedCategoryLookup = ProductTextNormalizer.NormalizeLookupValue(normalizedCategory);
        var productImageUrl = string.IsNullOrWhiteSpace(request.ProductImageUrl)
            ? DefaultProductImageUrl
            : request.ProductImageUrl;

        var product = await _dbContext.Products.FirstOrDefaultAsync(
            productEntity =>
                productEntity.NormalizedName == normalizedNameLookup &&
                productEntity.NormalizedCategory == normalizedCategoryLookup,
            cancellationToken);

        if (product is null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                NormalizedName = normalizedNameLookup,
                Category = normalizedCategory,
                NormalizedCategory = normalizedCategoryLookup,
                ImageUrl = productImageUrl,
                BaselinePrice = request.TargetPrice
            };

            _dbContext.Products.Add(product);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(product.NormalizedName))
            {
                product.NormalizedName = normalizedNameLookup;
            }

            if (string.IsNullOrWhiteSpace(product.NormalizedCategory))
            {
                product.NormalizedCategory = normalizedCategoryLookup;
            }

            if (string.Equals(product.ImageUrl, DefaultProductImageUrl, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(productImageUrl, DefaultProductImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                product.ImageUrl = productImageUrl;
            }

            if (product.BaselinePrice <= 0)
            {
                product.BaselinePrice = request.TargetPrice;
            }
        }

        var requestedStores = request.Stores
            .Where(store => !string.IsNullOrWhiteSpace(store.Key))
            .GroupBy(store => store.Key.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (requestedStores.Count == 0)
        {
            return new TrackedProductCreationResult();
        }

        var selectedSlugs = requestedStores
            .Select(store => store.Key.Trim().ToLowerInvariant())
            .ToList();

        var storesBySlug = await _dbContext.Stores
            .Where(store => selectedSlugs.Contains(store.Slug))
            .ToDictionaryAsync(store => store.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var requestedStore in requestedStores)
        {
            var slug = requestedStore.Key.Trim().ToLowerInvariant();
            if (storesBySlug.ContainsKey(slug))
            {
                continue;
            }

            var newStore = new Store
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(requestedStore.Name) ? slug : requestedStore.Name.Trim(),
                Slug = slug,
                AccentColor = string.IsNullOrWhiteSpace(requestedStore.AccentColor)
                    ? "#5b57f3"
                    : requestedStore.AccentColor
            };

            _dbContext.Stores.Add(newStore);
            storesBySlug[slug] = newStore;
        }

        var selectedStoreIds = storesBySlug.Values
            .Select(store => store.Id)
            .ToList();

        var existingStoreIds = await _dbContext.UserTrackedProducts
            .Where(tracked =>
                tracked.ApplicationUserId == userId &&
                tracked.ProductId == product.Id &&
                tracked.IsActive &&
                selectedStoreIds.Contains(tracked.StoreId))
            .Select(tracked => tracked.StoreId)
            .ToHashSetAsync(cancellationToken);

        var createdCount = 0;
        foreach (var store in storesBySlug.Values)
        {
            if (existingStoreIds.Contains(store.Id))
            {
                continue;
            }

            var trackedProduct = new UserTrackedProduct
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                ProductId = product.Id,
                StoreId = store.Id,
                TargetPrice = request.TargetPrice,
                MaximumPrice = request.MaximumPrice,
                AlertTrigger = request.AlertTrigger,
                EmailAlertsEnabled = request.EmailAlerts,
                PushNotificationsEnabled = request.PushNotifications,
                DailySummaryEnabled = request.DailySummary,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.UserTrackedProducts.Add(trackedProduct);
            _dbContext.PriceSnapshots.Add(new PriceSnapshot
            {
                Id = Guid.NewGuid(),
                UserTrackedProductId = trackedProduct.Id,
                Price = request.TargetPrice,
                CapturedAtUtc = DateTime.UtcNow
            });

            createdCount += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new TrackedProductCreationResult { CreatedCount = createdCount };
    }
}
