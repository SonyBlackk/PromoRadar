using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;

namespace PromoRadar.Web.Services;

public class PriceMonitoringJobService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PriceMonitoringJobService> _logger;

    public PriceMonitoringJobService(ApplicationDbContext dbContext, ILogger<PriceMonitoringJobService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RunPriceScanAsync()
    {
        var random = new Random(DateTime.UtcNow.Minute + DateTime.UtcNow.Hour * 100);

        var trackedProducts = await _dbContext.UserTrackedProducts
            .Include(x => x.Product)
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
            var baseline = tracked.Product?.BaselinePrice ?? tracked.TargetPrice;
            var adjustment = (decimal)(random.NextDouble() - 0.5) * 0.06m;
            var scannedPrice = Math.Round(baseline * (1 + adjustment), 2, MidpointRounding.AwayFromZero);

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

