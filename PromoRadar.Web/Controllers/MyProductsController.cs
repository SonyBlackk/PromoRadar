using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;
using PromoRadar.Web.Models;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class MyProductsController : Controller
{
    private const int PageSize = 4;
    private const string DefaultSort = "recent";
    private const string PartialContent = "content";
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static readonly IReadOnlyList<MyProductsFilterOptionViewModel> AvailableSortOptions =
    [
        new() { Value = "recent", Label = "Mais recentes" },
        new() { Value = "price-asc", Label = "Menor preço atual" },
        new() { Value = "price-desc", Label = "Maior preço atual" },
        new() { Value = "discount-desc", Label = "Maior economia" },
        new() { Value = "name-asc", Label = "Nome (A-Z)" }
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyProductsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? store = null,
        string? category = null,
        string sort = DefaultSort,
        int page = 1,
        string? partial = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ActiveNav"] = "my-products";

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var searchTerm = (search ?? string.Empty).Trim();
        var selectedSort = NormalizeSort(sort);

        var trackedProducts = await _dbContext.UserTrackedProducts
            .AsNoTracking()
            .Where(x => x.ApplicationUserId == userId && x.IsActive)
            .Include(x => x.Product)
            .Include(x => x.Store)
            .Include(x => x.PriceSnapshots)
            .ToListAsync(cancellationToken);

        var alertSummaryByProduct = await _dbContext.PriceAlerts
            .AsNoTracking()
            .Where(x => x.UserTrackedProduct != null && x.UserTrackedProduct.ApplicationUserId == userId)
            .GroupBy(x => x.UserTrackedProduct!.ProductId)
            .Select(group => new AlertSummaryProjection
            {
                ProductId = group.Key,
                TotalCount = group.Count(),
                UnreadCount = group.Count(alert => !alert.IsRead)
            })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        var groupedProducts = new List<MyProductItemViewModel>();
        foreach (var group in trackedProducts.Where(x => x.Product is not null).GroupBy(x => x.ProductId))
        {
            var unreadCount = alertSummaryByProduct.TryGetValue(group.Key, out var summary) ? summary.UnreadCount : 0;
            groupedProducts.Add(MapProductGroup(group, unreadCount));
        }

        var storeOptions = groupedProducts
            .SelectMany(product => product.MonitoredStores)
            .GroupBy(storeOption => storeOption.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MyProductsFilterOptionViewModel
            {
                Value = group.Key,
                Label = group.First().Name
            })
            .OrderBy(x => x.Label)
            .ToList();

        var categoryOptions = groupedProducts
            .Select(product => product.Category)
            .Where(categoryName => !string.IsNullOrWhiteSpace(categoryName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(categoryName => categoryName)
            .Select(categoryName => new MyProductsFilterOptionViewModel
            {
                Value = categoryName,
                Label = categoryName
            })
            .ToList();

        var selectedStore = storeOptions
            .FirstOrDefault(option => option.Value.Equals(store ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var selectedCategory = categoryOptions
            .FirstOrDefault(option => option.Value.Equals(category ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        IEnumerable<MyProductItemViewModel> filteredProducts = groupedProducts;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filteredProducts = filteredProducts.Where(product =>
                product.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                product.Brand.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                product.BestStoreName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                product.MonitoredStores.Any(monitoredStore =>
                    monitoredStore.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(selectedStore))
        {
            filteredProducts = filteredProducts.Where(product =>
                product.MonitoredStores.Any(monitoredStore =>
                    monitoredStore.Slug.Equals(selectedStore, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(selectedCategory))
        {
            filteredProducts = filteredProducts.Where(product =>
                product.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        filteredProducts = selectedSort switch
        {
            "price-asc" => filteredProducts.OrderBy(product => product.CurrentPrice),
            "price-desc" => filteredProducts.OrderByDescending(product => product.CurrentPrice),
            "discount-desc" => filteredProducts
                .OrderByDescending(product => product.TargetPrice - product.CurrentPrice)
                .ThenBy(product => product.CurrentPrice),
            "name-asc" => filteredProducts.OrderBy(product => product.Name),
            _ => filteredProducts.OrderByDescending(product => product.LatestCapturedAtUtc)
        };

        var filteredList = filteredProducts.ToList();
        var filteredTotalProducts = filteredList.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredTotalProducts / (double)PageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);

        var pagedProducts = filteredList
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        var averageCurrentPrice = groupedProducts.Count > 0 ? groupedProducts.Average(x => x.CurrentPrice) : 0m;
        var averageTargetPrice = groupedProducts.Count > 0 ? groupedProducts.Average(x => x.TargetPrice) : 0m;
        var averageVsTargetPercent = averageTargetPrice > 0
            ? Math.Round(((averageCurrentPrice - averageTargetPrice) / averageTargetPrice) * 100m, 2)
            : 0m;

        var createdThisMonth = trackedProducts.Count(x =>
            x.CreatedAtUtc >= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1));

        var totalAlertsCount = alertSummaryByProduct.Values.Sum(x => x.TotalCount);
        var unreadAlertsCount = alertSummaryByProduct.Values.Sum(x => x.UnreadCount);
        var economyPotential = groupedProducts.Sum(x => Math.Max(0m, x.CurrentPrice - x.TargetPrice));

        var viewModel = new MyProductsPageViewModel
        {
            Subtitle = groupedProducts.Count == 0
                ? "Você ainda não possui mercadorias monitoradas. Cadastre sua primeira para começar."
                : "Aqui você vê todos os produtos monitorados, metas de preço e performance por loja.",
            Search = searchTerm,
            SelectedStore = selectedStore,
            SelectedCategory = selectedCategory,
            SelectedSort = selectedSort,
            StoreOptions = storeOptions,
            CategoryOptions = categoryOptions,
            SortOptions = AvailableSortOptions,
            KpiCards =
            [
                new MyProductsKpiCardViewModel
                {
                    Icon = "bi-box-seam",
                    Title = "Produtos monitorados",
                    Value = groupedProducts.Count.ToString(PtBr),
                    Subtitle = createdThisMonth > 0 ? $"+{createdThisMonth} este mês" : "Sem novos este mês",
                    AccentClass = "indigo"
                },
                new MyProductsKpiCardViewModel
                {
                    Icon = "bi-graph-up-arrow",
                    Title = "Preço médio atual",
                    Value = averageCurrentPrice.ToString("C2", PtBr),
                    Subtitle = $"{averageVsTargetPercent.ToString("+0.00;-0.00", PtBr)}% vs metas",
                    AccentClass = "mint"
                },
                new MyProductsKpiCardViewModel
                {
                    Icon = "bi-bell",
                    Title = "Alertas ativos",
                    Value = totalAlertsCount.ToString(PtBr),
                    Subtitle = unreadAlertsCount == 1 ? "1 não lido" : $"{unreadAlertsCount} não lidos",
                    AccentClass = "orange"
                },
                new MyProductsKpiCardViewModel
                {
                    Icon = "bi-graph-up",
                    Title = "Economia potencial",
                    Value = economyPotential.ToString("C2", PtBr),
                    Subtitle = "Total possível",
                    AccentClass = "violet"
                }
            ],
            Products = pagedProducts,
            ShowingFrom = filteredTotalProducts == 0 ? 0 : ((currentPage - 1) * PageSize) + 1,
            ShowingTo = filteredTotalProducts == 0 ? 0 : Math.Min(currentPage * PageSize, filteredTotalProducts),
            TotalProducts = filteredTotalProducts,
            CurrentPage = currentPage,
            TotalPages = totalPages
        };

        if (IsPartialContentRequest(partial))
        {
            return PartialView("_ProductsTableContent", viewModel);
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProduct(
        Guid productId,
        string? search,
        string? store,
        string? category,
        string sort = DefaultSort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var trackedRows = await _dbContext.UserTrackedProducts
            .Where(x => x.ApplicationUserId == userId && x.ProductId == productId)
            .ToListAsync(cancellationToken);

        if (trackedRows.Count == 0)
        {
            TempData["WarningMessage"] = "Essa mercadoria já não está na sua lista.";
            return RedirectToFilteredIndex(search, store, category, sort, page);
        }

        _dbContext.UserTrackedProducts.RemoveRange(trackedRows);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Mercadoria removida com sucesso.";
        return RedirectToFilteredIndex(search, store, category, sort, page);
    }

    private bool IsPartialContentRequest(string? partial)
    {
        return string.Equals(partial, PartialContent, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult RedirectToFilteredIndex(string? search, string? store, string? category, string? sort, int page)
    {
        return RedirectToAction(nameof(Index), new
        {
            search = NullIfWhitespace(search),
            store = NullIfWhitespace(store),
            category = NullIfWhitespace(category),
            sort = NormalizeSort(sort),
            page = Math.Max(1, page)
        });
    }

    private static MyProductItemViewModel MapProductGroup(IGrouping<Guid, UserTrackedProduct> group, int unreadAlertsCount)
    {
        var product = group.Select(x => x.Product).FirstOrDefault(x => x is not null);
        var productName = product?.Name ?? "Produto";
        var category = product?.Category ?? "Categoria";

        var latestByTracked = group
            .Select(item => new
            {
                Tracked = item,
                Snapshot = item.PriceSnapshots.OrderByDescending(snapshot => snapshot.CapturedAtUtc).FirstOrDefault()
            })
            .Where(x => x.Snapshot is not null)
            .ToList();

        var latestCapturedAtUtc = latestByTracked.Count > 0
            ? latestByTracked.Max(x => x.Snapshot!.CapturedAtUtc)
            : group.Max(x => x.CreatedAtUtc);

        var currentPrice = latestByTracked.Count > 0
            ? Math.Round(latestByTracked.Average(x => x.Snapshot!.Price), 2)
            : Math.Round(group.Average(x => x.TargetPrice), 2);

        var bestOffer = latestByTracked
            .OrderBy(x => x.Snapshot!.Price)
            .ThenByDescending(x => x.Snapshot!.CapturedAtUtc)
            .FirstOrDefault();

        var bestPrice = bestOffer?.Snapshot?.Price ?? currentPrice;
        var bestStore = bestOffer?.Tracked.Store ?? group.Select(x => x.Store).FirstOrDefault();
        var targetPrice = Math.Round(group.Average(x => x.TargetPrice), 2);

        var trendPoints = group
            .SelectMany(item => item.PriceSnapshots)
            .OrderBy(snapshot => snapshot.CapturedAtUtc)
            .Select(snapshot => snapshot.Price)
            .TakeLast(12)
            .ToList();

        if (trendPoints.Count == 0)
        {
            trendPoints.Add(currentPrice);
        }

        if (trendPoints.Count == 1)
        {
            trendPoints.Add(trendPoints[0]);
        }

        var previousPoint = trendPoints[^2];
        var latestPoint = trendPoints[^1];
        var variationPercent = previousPoint == 0m
            ? 0m
            : Math.Round(((latestPoint - previousPoint) / previousPoint) * 100m, 2);

        var monitoredStores = group
            .Select(item => item.Store)
            .Where(storeEntity => storeEntity is not null && !string.IsNullOrWhiteSpace(storeEntity.Slug))
            .GroupBy(storeEntity => storeEntity!.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(groupedStore => new MyProductStoreSummaryViewModel
            {
                Slug = groupedStore.Key,
                Name = groupedStore.First()!.Name
            })
            .OrderBy(storeOption => storeOption.Name)
            .ToList();

        var storeLogo = GetStoreLogo(bestStore?.Slug, bestStore?.Name);

        return new MyProductItemViewModel
        {
            ProductId = group.Key,
            Name = productName,
            Brand = ResolveBrand(productName),
            Category = category,
            CategoryClass = ResolveCategoryClass(category),
            ImageUrl = string.IsNullOrWhiteSpace(product?.ImageUrl) ? "/images/products/default.svg" : product.ImageUrl,
            CurrentPrice = currentPrice,
            TargetPrice = targetPrice,
            BestPrice = bestPrice,
            BestStoreName = bestStore?.Name ?? "Loja monitorada",
            BestStoreLogoText = storeLogo.LogoText,
            BestStoreLogoClass = storeLogo.LogoClass,
            VariationPercent = variationPercent,
            IsTrendNegative = trendPoints[^1] > trendPoints[0],
            LatestCapturedAtUtc = latestCapturedAtUtc,
            UnreadAlertsCount = unreadAlertsCount,
            MonitoredStores = monitoredStores,
            TrendPoints = trendPoints
        };
    }

    private static string ResolveCategoryClass(string category)
    {
        var normalized = category.ToLowerInvariant();

        if (normalized.Contains("placa") || normalized.Contains("gpu"))
        {
            return "gpu";
        }

        if (normalized.Contains("console"))
        {
            return "console";
        }

        if (normalized.Contains("smart") || normalized.Contains("phone") || normalized.Contains("celular"))
        {
            return "phone";
        }

        if (normalized.Contains("monitor"))
        {
            return "monitor";
        }

        return "default";
    }

    private static string ResolveBrand(string productName)
    {
        var normalized = productName.ToLowerInvariant();

        if (normalized.Contains("rtx") || normalized.Contains("nvidia"))
        {
            return "NVIDIA";
        }

        if (normalized.Contains("ryzen"))
        {
            return "AMD";
        }

        if (normalized.Contains("playstation") || normalized.Contains("ps5"))
        {
            return "Sony";
        }

        if (normalized.Contains("iphone"))
        {
            return "Apple";
        }

        if (normalized.Contains("ultragear") || normalized.Contains("lg"))
        {
            return "LG";
        }

        return "Marca não informada";
    }

    private static string NormalizeSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return DefaultSort;
        }

        return AvailableSortOptions.Any(option => option.Value.Equals(sort, StringComparison.OrdinalIgnoreCase))
            ? sort.ToLowerInvariant()
            : DefaultSort;
    }

    private static string? NullIfWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static (string LogoText, string LogoClass) GetStoreLogo(string? storeSlug, string? storeName)
    {
        var normalized = (storeSlug ?? storeName ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "amazon-br" or "amazon" => ("a", "amazon"),
            "mercado-livre" => ("ml", "mercado-livre"),
            "magazine-luiza" => ("magalu", "magalu"),
            "kabum" => ("Kabum", "kabum"),
            "pichau" => ("Pichau", "pichau"),
            "casas-bahia" => ("Casas", "casas-bahia"),
            "americanas" => ("americanas", "americanas"),
            "shopee" => ("S", "shopee"),
            "terabyte" => ("Tera", "terabyte"),
            _ => (GetFallbackLogoText(storeName), "default")
        };
    }

    private static string GetFallbackLogoText(string? storeName)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            return "Loja";
        }

        var token = storeName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Loja";
        }

        return token.Length <= 6 ? token : token[..6];
    }

    private sealed class AlertSummaryProjection
    {
        public Guid ProductId { get; set; }

        public int TotalCount { get; set; }

        public int UnreadCount { get; set; }
    }
}
