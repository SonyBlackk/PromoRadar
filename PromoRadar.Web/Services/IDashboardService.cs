using PromoRadar.Web.ViewModels.Dashboard;

namespace PromoRadar.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
}
