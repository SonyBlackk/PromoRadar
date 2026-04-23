using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;
using PromoRadar.Web.Models;
using PromoRadar.Web.Models.Enums;
using PromoRadar.Web.ViewModels.Dashboard;

namespace PromoRadar.Web.Services;

public class DashboardService : IDashboardService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(x => x.Id == userId, cancellationToken);

        var trackedProducts = await _dbContext.UserTrackedProducts
            .AsNoTracking()
            .Where(x => x.ApplicationUserId == userId)
            .Include(x => x.Product)
            .Include(x => x.Store)
            .Include(x => x.PriceSnapshots)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var alerts = await _dbContext.PriceAlerts
            .AsNoTracking()
            .Where(x => x.UserTrackedProduct != null && x.UserTrackedProduct.ApplicationUserId == userId)
            .Include(x => x.UserTrackedProduct!)
                .ThenInclude(x => x.Product)
            .Include(x => x.UserTrackedProduct!)
                .ThenInclude(x => x.Store)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        var featuredTracked = trackedProducts
            .FirstOrDefault(x => x.Product?.Name.Contains("4070", StringComparison.OrdinalIgnoreCase) == true)
            ?? trackedProducts.FirstOrDefault();

        FeaturedProductViewModel featuredProduct;
        if (featuredTracked is null)
        {
            featuredProduct = BuildEmptyFeaturedProduct();
        }
        else
        {
            var featuredSnapshots = featuredTracked.PriceSnapshots
                .OrderBy(x => x.CapturedAtUtc)
                .ToList();

            var currentPrice = featuredSnapshots.LastOrDefault()?.Price ?? featuredTracked.TargetPrice;
            var deltaPercent = featuredTracked.TargetPrice == 0
                ? 0
                : Math.Round(((currentPrice - featuredTracked.TargetPrice) / featuredTracked.TargetPrice) * 100, 1);
            var (priceSeriesByPeriod, labelSeriesByPeriod) = BuildPriceSeries(featuredSnapshots);

            featuredProduct = new FeaturedProductViewModel
            {
                Name = featuredTracked.Product?.Name ?? "Produto monitorado",
                Store = featuredTracked.Store?.Name ?? "Loja",
                StoreBadge = featuredTracked.Store?.Slug is "amazon-br" ? "a" : GetFirstLetter(featuredTracked.Store?.Name),
                ImageUrl = featuredTracked.Product?.ImageUrl ?? "/images/products/default.svg",
                TargetPrice = featuredTracked.TargetPrice,
                CurrentPrice = currentPrice,
                DeltaPercent = deltaPercent,
                DeltaLabel = deltaPercent <= 0 ? "abaixo da meta" : "acima da meta",
                LastUpdatedLabel = FormatLastUpdatedLabel(featuredSnapshots.LastOrDefault()?.CapturedAtUtc),
                LabelSeriesByPeriod = labelSeriesByPeriod,
                PriceSeriesByPeriod = priceSeriesByPeriod
            };
        }

        var totalEconomy = trackedProducts
            .Select(x =>
            {
                var latest = x.PriceSnapshots.OrderByDescending(s => s.CapturedAtUtc).FirstOrDefault();
                if (latest is null || x.Product is null)
                {
                    return 0m;
                }

                return Math.Max(0, x.Product.BaselinePrice - latest.Price);
            })
            .Sum();

        var cheapProducts = trackedProducts.Count(x =>
        {
            var latest = x.PriceSnapshots.OrderByDescending(s => s.CapturedAtUtc).FirstOrDefault();
            return latest is not null && x.Product is not null && latest.Price <= x.Product.BaselinePrice * 0.92m;
        });

        var monitoredThisWeek = trackedProducts.Count(x => x.CreatedAtUtc >= DateTime.UtcNow.AddDays(-7));

        var viewModel = new DashboardViewModel
        {
            GreetingName = user.DisplayName,
            GreetingSubtitle = trackedProducts.Count == 0
                ? "Vamos começar? Adicione sua primeira mercadoria para monitorar preços."
                : "Aqui estão as melhores oportunidades para você hoje.",
            SummaryCards =
            [
                new SummaryCardViewModel
                {
                    Title = "Monitorados",
                    Icon = "bi-wallet2",
                    Value = trackedProducts.Count.ToString(PtBr),
                    Subtitle = monitoredThisWeek > 0
                        ? $"+{monitoredThisWeek} nos últimos 7 dias"
                        : "Sem novos nos últimos 7 dias",
                    AccentClass = "accent-indigo"
                },
                new SummaryCardViewModel
                {
                    Title = "Alertas ativos",
                    Icon = "bi-tags",
                    Value = alerts.Count.ToString(PtBr),
                    Subtitle = "Ver alertas",
                    AccentClass = "accent-mint"
                },
                new SummaryCardViewModel
                {
                    Title = "Economia total",
                    Icon = "bi-graph-up-arrow",
                    Value = totalEconomy.ToString("C2", PtBr),
                    Subtitle = "Últimos 30 dias",
                    AccentClass = "accent-orange"
                },
                new SummaryCardViewModel
                {
                    Title = "Produtos baratos",
                    Icon = "bi-bar-chart-line",
                    Value = cheapProducts.ToString(PtBr),
                    Subtitle = "Oportunidades hoje",
                    AccentClass = "accent-blue"
                }
            ],
            FeaturedProduct = featuredProduct,
            RecentAlerts = alerts.Take(4).Select(alert => new RecentAlertViewModel
            {
                ProductName = alert.UserTrackedProduct?.Product?.Name ?? "Produto",
                StoreName = alert.UserTrackedProduct?.Store?.Name ?? "Loja",
                Price = alert.TriggerPrice,
                Note = alert.Note,
                TimeAgo = FormatTimeAgo(alert.CreatedAtUtc),
                Icon = alert.Severity switch
                {
                    AlertSeverity.Positive => "bi-graph-up-arrow",
                    AlertSeverity.Warning => "bi-fire",
                    AlertSeverity.Critical => "bi-exclamation-triangle",
                    _ => "bi-bell"
                },
                AccentClass = alert.Severity switch
                {
                    AlertSeverity.Positive => "positive",
                    AlertSeverity.Warning => "warning",
                    AlertSeverity.Critical => "critical",
                    _ => "neutral"
                }
            }).ToList(),
            DaySummary = BuildDaySummary(trackedProducts),
            StoreScores = BuildStoreScores(trackedProducts),
            Suggestions = BuildSuggestions(trackedProducts)
        };

        return viewModel;
    }

    private static (IReadOnlyDictionary<string, IReadOnlyList<decimal>> Prices, IReadOnlyDictionary<string, IReadOnlyList<string>> Labels) BuildPriceSeries(IReadOnlyList<PriceSnapshot> snapshots)
    {
        var ordered = snapshots.OrderBy(x => x.CapturedAtUtc).ToList();

        var sevenDays = ordered.TakeLast(7).ToList();
        var thirtyDays = ordered.TakeLast(30).ToList();
        var ninetyDays = ordered.TakeLast(90).ToList();

        var twelveMonths = ordered
            .Where(snapshot => snapshot.CapturedAtUtc >= DateTime.UtcNow.AddMonths(-12))
            .GroupBy(snapshot => new { snapshot.CapturedAtUtc.Year, snapshot.CapturedAtUtc.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new
            {
                Label = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM/yy", PtBr),
                Price = Math.Round(group.Average(snapshot => snapshot.Price), 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        var allData = ordered.TakeLast(120).ToList();

        var priceSeries = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase)
        {
            ["7D"] = sevenDays.Select(snapshot => snapshot.Price).ToList(),
            ["30D"] = thirtyDays.Select(snapshot => snapshot.Price).ToList(),
            ["90D"] = ninetyDays.Select(snapshot => snapshot.Price).ToList(),
            ["1A"] = twelveMonths.Select(month => month.Price).ToList(),
            ["Tudo"] = allData.Select(snapshot => snapshot.Price).ToList()
        };

        var labelSeries = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["7D"] = sevenDays.Select(snapshot => snapshot.CapturedAtUtc.ToString("dd/MM", PtBr)).ToList(),
            ["30D"] = thirtyDays.Select(snapshot => snapshot.CapturedAtUtc.ToString("dd/MM", PtBr)).ToList(),
            ["90D"] = ninetyDays.Select(snapshot => snapshot.CapturedAtUtc.ToString("dd/MM", PtBr)).ToList(),
            ["1A"] = twelveMonths.Select(month => month.Label).ToList(),
            ["Tudo"] = allData.Select(snapshot => snapshot.CapturedAtUtc.ToString("dd/MM", PtBr)).ToList()
        };

        return (priceSeries, labelSeries);
    }

    private static DaySummaryViewModel BuildDaySummary(IEnumerable<UserTrackedProduct> trackedProducts)
    {
        var down = 0;
        var up = 0;
        var stable = 0;

        foreach (var tracked in trackedProducts)
        {
            var lastTwo = tracked.PriceSnapshots
                .OrderByDescending(x => x.CapturedAtUtc)
                .Take(2)
                .ToList();

            if (lastTwo.Count < 2)
            {
                continue;
            }

            if (lastTwo[0].Price < lastTwo[1].Price)
            {
                down++;
            }
            else if (lastTwo[0].Price > lastTwo[1].Price)
            {
                up++;
            }
            else
            {
                stable++;
            }
        }

        return new DaySummaryViewModel
        {
            TotalVariations = down + up + stable,
            DownCount = down,
            UpCount = up,
            StableCount = stable,
            DateLabel = DateTime.Now.ToString("dd/MM/yyyy", PtBr)
        };
    }

    private static IReadOnlyList<StoreScoreViewModel> BuildStoreScores(IEnumerable<UserTrackedProduct> trackedProducts)
    {
        return trackedProducts
            .GroupBy(x => x.Store?.Name ?? "Loja")
            .Select(group =>
            {
                var avgScore = group
                    .Select(item =>
                    {
                        var last = item.PriceSnapshots.OrderByDescending(s => s.CapturedAtUtc).FirstOrDefault();
                        var baseline = item.Product?.BaselinePrice ?? item.TargetPrice;
                        if (last is null || baseline <= 0)
                        {
                            return 0m;
                        }

                        return Math.Clamp((baseline - last.Price) / baseline * 100m, -20m, 20m);
                    })
                    .DefaultIfEmpty(0)
                    .Average();

                var score = (int)Math.Clamp(Math.Round(92 + avgScore * 0.4m), 80, 99);
                var iconText = string.Concat(group.Key
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x[0]))
                    .ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(iconText))
                {
                    iconText = "ST";
                }

                return new StoreScoreViewModel
                {
                    StoreName = group.Key,
                    IconText = iconText[..Math.Min(2, iconText.Length)],
                    Score = score
                };
            })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<SuggestionItemViewModel> BuildSuggestions(IEnumerable<UserTrackedProduct> trackedProducts)
    {
        return trackedProducts
            .Select(x =>
            {
                var snapshots = x.PriceSnapshots
                    .OrderByDescending(s => s.CapturedAtUtc)
                    .Take(8)
                    .Select(s => s.Price)
                    .Reverse()
                    .ToList();

                var latest = snapshots.LastOrDefault();
                var average = snapshots.Count == 0 ? x.TargetPrice : snapshots.Average();
                var discountPercent = average <= 0 ? 0 : Math.Round((average - latest) / average * 100, 1);

                var badge = discountPercent switch
                {
                    >= 12 => ("Muito bom", "great"),
                    >= 6 => ("Bom", "good"),
                    _ => ("Atenção", "attention")
                };

                var comparisonText = discountPercent > 0
                    ? $"-{discountPercent.ToString("0.#", PtBr)}% abaixo da média recente"
                    : "Preço acima da média recente";

                return new
                {
                    Suggestion = new SuggestionItemViewModel
                    {
                        Name = x.Product?.Name ?? "Produto",
                        StoreName = x.Store?.Name ?? "Loja",
                        Price = latest,
                        BadgeText = badge.Item1,
                        BadgeClass = badge.Item2,
                        ComparisonText = comparisonText,
                        ImageUrl = x.Product?.ImageUrl ?? "/images/products/default.svg",
                        SparklinePoints = snapshots
                    },
                    Discount = discountPercent
                };
            })
            .OrderByDescending(x => x.Discount)
            .Take(4)
            .Select(x => x.Suggestion)
            .ToList();
    }

    private static string FormatTimeAgo(DateTime createdAtUtc)
    {
        var elapsed = DateTime.UtcNow - createdAtUtc;
        if (elapsed.TotalMinutes < 60)
        {
            return $"Há {(int)elapsed.TotalMinutes} min";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"Há {(int)elapsed.TotalHours} h";
        }

        var days = (int)elapsed.TotalDays;
        return days <= 1 ? "Há 1 dia" : $"Há {days} dias";
    }

    private static string FormatLastUpdatedLabel(DateTime? capturedAtUtc)
    {
        if (!capturedAtUtc.HasValue)
        {
            return "Sem leituras ainda";
        }

        var elapsed = DateTime.UtcNow - capturedAtUtc.Value;
        if (elapsed.TotalMinutes < 1)
        {
            return "Atualizado agora";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"Atualizado há {(int)elapsed.TotalMinutes} min";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"Atualizado há {(int)elapsed.TotalHours} h";
        }

        return $"Atualizado há {(int)elapsed.TotalDays} dia(s)";
    }

    private static string GetFirstLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "p";
        }

        return value[..1].ToLowerInvariant();
    }

    private static FeaturedProductViewModel BuildEmptyFeaturedProduct()
    {
        var labels7d = Enumerable.Range(0, 7)
            .Select(offset => DateTime.Now.Date.AddDays(-(6 - offset)).ToString("dd/MM", PtBr))
            .ToList();

        var labels30d = Enumerable.Range(0, 30)
            .Select(offset => DateTime.Now.Date.AddDays(-(29 - offset)).ToString("dd/MM", PtBr))
            .ToList();

        var labels90d = Enumerable.Range(0, 90)
            .Select(offset => DateTime.Now.Date.AddDays(-(89 - offset)).ToString("dd/MM", PtBr))
            .ToList();

        var labels1y = Enumerable.Range(0, 12)
            .Select(offset => DateTime.Now.Date.AddMonths(-(11 - offset)).ToString("MMM/yy", PtBr))
            .ToList();

        var empty7 = Enumerable.Repeat(0m, 7).ToList();
        var empty30 = Enumerable.Repeat(0m, 30).ToList();
        var empty90 = Enumerable.Repeat(0m, 90).ToList();
        var empty1y = Enumerable.Repeat(0m, 12).ToList();

        return new FeaturedProductViewModel
        {
            Name = "Nenhuma mercadoria monitorada",
            Store = "Cadastre seu primeiro produto para começar",
            StoreBadge = "+",
            ImageUrl = "/images/products/default.svg",
            TargetPrice = 0m,
            CurrentPrice = 0m,
            DeltaPercent = 0m,
            DeltaLabel = "Sem dados ainda",
            LastUpdatedLabel = "Aguardando primeiro monitoramento",
            LabelSeriesByPeriod = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["7D"] = labels7d,
                ["30D"] = labels30d,
                ["90D"] = labels90d,
                ["1A"] = labels1y,
                ["Tudo"] = labels30d
            },
            PriceSeriesByPeriod = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                ["7D"] = empty7,
                ["30D"] = empty30,
                ["90D"] = empty90,
                ["1A"] = empty1y,
                ["Tudo"] = empty30
            }
        };
    }
}
