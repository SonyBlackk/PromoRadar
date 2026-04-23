using PromoRadar.Web.Models;

namespace PromoRadar.Web.Services;

public class SimulatedPriceProvider : IPriceProvider
{
    public Task<decimal> GetCurrentPriceAsync(UserTrackedProduct trackedProduct, CancellationToken cancellationToken = default)
    {
        // Provider explícito de simulação para manter o MVP funcional até existir integração real.
        var random = Random.Shared;
        var baseline = trackedProduct.Product?.BaselinePrice ?? trackedProduct.TargetPrice;
        var adjustment = ((decimal)random.NextDouble() - 0.5m) * 0.06m;
        var simulatedPrice = Math.Round(baseline * (1 + adjustment), 2, MidpointRounding.AwayFromZero);
        return Task.FromResult(simulatedPrice);
    }
}
