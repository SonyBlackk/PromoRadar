using PromoRadar.Web.Models;

namespace PromoRadar.Web.Services;

public interface IPriceProvider
{
    Task<decimal> GetCurrentPriceAsync(UserTrackedProduct trackedProduct, CancellationToken cancellationToken = default);
}
