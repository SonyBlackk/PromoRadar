using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;

namespace PromoRadar.Web.Services;

public class PriceMonitoringJobService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPriceProvider _priceProvider;
    private readonly ILogger<PriceMonitoringJobService> _logger;

    public PriceMonitoringJobService(
        ApplicationDbContext dbContext,
        IPriceProvider priceProvider,
        ILogger<PriceMonitoringJobService> logger)
    {
        _dbContext = dbContext;
        _priceProvider = priceProvider;
        _logger = logger;
    }

    public async Task RunPriceScanAsync()
    {
        var trackedProducts = await _dbContext.UserTrackedProducts
            .Include(x => x.Product)
            .Where(x => x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(6)
            .ToListAsync();

        if (trackedProducts.Count == 0)
        {
            _logger.LogInformation("Hangfire scan skipped: no tracked products found.");
            return;
        }

        foreach (var tracked in trackedProducts)
        {
            var scannedPrice = await _priceProvider.GetCurrentPriceAsync(tracked);

            _dbContext.PriceSnapshots.Add(new Models.PriceSnapshot
            {
                UserTrackedProductId = tracked.Id,
                CapturedAtUtc = DateTime.UtcNow,
                Price = scannedPrice
            });
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hangfire scan finished with {Count} price snapshots.", trackedProducts.Count);
    }
}

