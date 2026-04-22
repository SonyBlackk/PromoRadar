using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Models;
using PromoRadar.Web.Models.Enums;

namespace PromoRadar.Web.Data;

public static class DataSeeder
{
    public const string DemoEmail = "luiz@promoradar.local";
    public const string DemoPassword = "PromoRadar@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Email == DemoEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                EmailConfirmed = true,
                DisplayName = "Luiz",
                AvatarInitials = "XB",
                PlanName = "Plano Gratuito"
            };

            var createResult = await userManager.CreateAsync(user, DemoPassword);
            if (!createResult.Succeeded)
            {
                var message = string.Join(", ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Não foi possível criar o usuário demo: {message}");
            }
        }

        if (!await context.Stores.AnyAsync())
        {
            var stores = new[]
            {
                new Store { Name = "Amazon BR", Slug = "amazon-br", AccentColor = "#111827" },
                new Store { Name = "KaBuM!", Slug = "kabum", AccentColor = "#0057ff" },
                new Store { Name = "Pichau", Slug = "pichau", AccentColor = "#ef233c" },
                new Store { Name = "Terabyte", Slug = "terabyte", AccentColor = "#f97316" },
                new Store { Name = "Mercado Livre", Slug = "mercado-livre", AccentColor = "#facc15" }
            };

            context.Stores.AddRange(stores);
        }

        if (!await context.Products.AnyAsync())
        {
            var products = new[]
            {
                new Product { Name = "NVIDIA RTX 4070 Super", Category = "GPU", ImageUrl = "/images/products/rtx-4070.svg", BaselinePrice = 2499.90m },
                new Product { Name = "RX 7800 XT 16GB", Category = "GPU", ImageUrl = "/images/products/rx-7800.svg", BaselinePrice = 2499.90m },
                new Product { Name = "SSD WD SN770 1TB", Category = "SSD", ImageUrl = "/images/products/sn770.svg", BaselinePrice = 459.90m },
                new Product { Name = "Ryzen 5 5600", Category = "CPU", ImageUrl = "/images/products/ryzen-5600.svg", BaselinePrice = 699.90m },
                new Product { Name = "Memória 16GB DDR4", Category = "RAM", ImageUrl = "/images/products/ddr4-16.svg", BaselinePrice = 329.90m },
                new Product { Name = "Fonte Corsair 650W", Category = "PSU", ImageUrl = "/images/products/corsair-650w.svg", BaselinePrice = 439.90m },
                new Product { Name = "SSD Kingston NV2 1TB", Category = "SSD", ImageUrl = "/images/products/nv2.svg", BaselinePrice = 399.90m },
                new Product { Name = "Ryzen 7 5700X", Category = "CPU", ImageUrl = "/images/products/ryzen-5700x.svg", BaselinePrice = 1299.90m }
            };

            context.Products.AddRange(products);
        }

        await context.SaveChangesAsync();

        if (!await context.UserTrackedProducts.AnyAsync(x => x.ApplicationUserId == user.Id))
        {
            var storesBySlug = await context.Stores.ToDictionaryAsync(x => x.Slug);
            var productsByName = await context.Products.ToDictionaryAsync(x => x.Name);

            var definitions = new (string product, string store, decimal target)[]
            {
                ("NVIDIA RTX 4070 Super", "amazon-br", 2500.00m),
                ("NVIDIA RTX 4070 Super", "kabum", 2520.00m),
                ("RX 7800 XT 16GB", "pichau", 2230.00m),
                ("SSD WD SN770 1TB", "kabum", 399.00m),
                ("Ryzen 5 5600", "terabyte", 669.00m),
                ("Memória 16GB DDR4", "amazon-br", 309.00m),
                ("Fonte Corsair 650W", "amazon-br", 409.00m),
                ("SSD Kingston NV2 1TB", "terabyte", 369.00m),
                ("Ryzen 7 5700X", "pichau", 1090.00m),
                ("Fonte Corsair 650W", "mercado-livre", 429.00m),
                ("SSD WD SN770 1TB", "amazon-br", 419.00m),
                ("Ryzen 5 5600", "kabum", 679.00m)
            };

            var tracked = definitions.Select(d => new UserTrackedProduct
            {
                ApplicationUserId = user.Id,
                ProductId = productsByName[d.product].Id,
                StoreId = storesBySlug[d.store].Id,
                TargetPrice = d.target,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-32)
            }).ToList();

            context.UserTrackedProducts.AddRange(tracked);
            await context.SaveChangesAsync();
        }

        if (!await context.PriceSnapshots.AnyAsync())
        {
            var trackedProducts = await context.UserTrackedProducts
                .Include(x => x.Product)
                .ToListAsync();

            var random = new Random(20260421);
            var snapshots = new List<PriceSnapshot>();

            foreach (var tracked in trackedProducts)
            {
                var baseline = tracked.Product?.BaselinePrice ?? tracked.TargetPrice;
                for (var day = 29; day >= 0; day--)
                {
                    var capturedAtUtc = DateTime.UtcNow.Date.AddDays(-day).AddHours(12);
                    var volatility = 0.94m + (decimal)random.NextDouble() * 0.14m;
                    var price = Math.Round(baseline * volatility, 2, MidpointRounding.AwayFromZero);

                    snapshots.Add(new PriceSnapshot
                    {
                        UserTrackedProductId = tracked.Id,
                        CapturedAtUtc = capturedAtUtc,
                        Price = price
                    });
                }
            }

            var featured = trackedProducts.FirstOrDefault(x => x.Product!.Name == "NVIDIA RTX 4070 Super" && x.Store!.Slug == "amazon-br");
            if (featured is not null)
            {
                snapshots.RemoveAll(x => x.UserTrackedProductId == featured.Id && x.CapturedAtUtc >= DateTime.UtcNow.Date.AddDays(-6));

                var featuredSeries = new decimal[]
                {
                    2460.00m,
                    2485.00m,
                    2415.00m,
                    2498.00m,
                    2452.00m,
                    2510.00m,
                    2389.90m
                };

                for (var index = 0; index < featuredSeries.Length; index++)
                {
                    snapshots.Add(new PriceSnapshot
                    {
                        UserTrackedProductId = featured.Id,
                        CapturedAtUtc = DateTime.UtcNow.Date.AddDays(-(featuredSeries.Length - 1 - index)).AddHours(13),
                        Price = featuredSeries[index]
                    });
                }
            }

            context.PriceSnapshots.AddRange(snapshots);
            await context.SaveChangesAsync();
        }

        if (!await context.PriceAlerts.AnyAsync())
        {
            var trackedByName = await context.UserTrackedProducts
                .Include(x => x.Product)
                .Include(x => x.Store)
                .ToListAsync();

            UserTrackedProduct Find(string productName, string storeSlug) => trackedByName.First(x =>
                x.Product!.Name == productName && x.Store!.Slug == storeSlug);

            var alerts = new[]
            {
                new PriceAlert
                {
                    UserTrackedProductId = Find("NVIDIA RTX 4070 Super", "amazon-br").Id,
                    TriggerPrice = 2389.90m,
                    Title = "Meta atingida",
                    Note = "RTX 4070 Super ficou abaixo da meta.",
                    Severity = AlertSeverity.Positive,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
                },
                new PriceAlert
                {
                    UserTrackedProductId = Find("Ryzen 7 5700X", "pichau").Id,
                    TriggerPrice = 1089.90m,
                    Title = "Queda relevante",
                    Note = "Caiu 8% nas últimas 24h.",
                    Severity = AlertSeverity.Positive,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
                },
                new PriceAlert
                {
                    UserTrackedProductId = Find("SSD Kingston NV2 1TB", "terabyte").Id,
                    TriggerPrice = 359.90m,
                    Title = "Menor preço em 30 dias",
                    Note = "Boa oportunidade para compra imediata.",
                    Severity = AlertSeverity.Warning,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-3)
                },
                new PriceAlert
                {
                    UserTrackedProductId = Find("Fonte Corsair 650W", "amazon-br").Id,
                    TriggerPrice = 389.90m,
                    Title = "Voltou ao estoque",
                    Note = "Produto voltou com preço competitivo.",
                    Severity = AlertSeverity.Positive,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            };

            context.PriceAlerts.AddRange(alerts);
            await context.SaveChangesAsync();
        }
    }
}

