using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Models;
using PromoRadar.Web.Models.Enums;
using PromoRadar.Web.Services;

namespace PromoRadar.Web.Data;

public static class DataSeeder
{
    public const string DemoEmail = "luiz@promoradar.local";

    public static async Task EnsureRoleAsync(IServiceProvider services, string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("Role inválida para seed.", nameof(roleName));
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var createRoleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!createRoleResult.Succeeded)
        {
            var message = string.Join(", ", createRoleResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException($"Não foi possível criar a role '{roleName}': {message}");
        }
    }

    public static async Task SeedReferenceDataAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

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
                CreateProduct("NVIDIA RTX 4070 Super", "GPU", "/images/products/rtx-4070.svg", 2499.90m),
                CreateProduct("RX 7800 XT 16GB", "GPU", "/images/products/rx-7800.svg", 2499.90m),
                CreateProduct("SSD WD SN770 1TB", "SSD", "/images/products/sn770.svg", 459.90m),
                CreateProduct("Ryzen 5 5600", "CPU", "/images/products/ryzen-5600.svg", 699.90m),
                CreateProduct("Memória 16GB DDR4", "RAM", "/images/products/ddr4-16.svg", 329.90m),
                CreateProduct("Fonte Corsair 650W", "PSU", "/images/products/corsair-650w.svg", 439.90m),
                CreateProduct("SSD Kingston NV2 1TB", "SSD", "/images/products/nv2.svg", 399.90m),
                CreateProduct("Ryzen 7 5700X", "CPU", "/images/products/ryzen-5700x.svg", 1299.90m)
            };

            context.Products.AddRange(products);
        }
        else
        {
            var products = await context.Products
                .Where(product => string.IsNullOrWhiteSpace(product.NormalizedName) || string.IsNullOrWhiteSpace(product.NormalizedCategory))
                .ToListAsync();

            foreach (var product in products)
            {
                product.NormalizedName = ProductTextNormalizer.NormalizeLookupValue(product.Name);
                product.NormalizedCategory = ProductTextNormalizer.NormalizeLookupValue(product.Category);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedDevelopmentDemoAsync(IServiceProvider services, string adminRoleName, string demoPassword)
    {
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            throw new InvalidOperationException("Defina uma senha de demo para ambiente de desenvolvimento.");
        }

        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleAsync(services, adminRoleName);
        await SeedReferenceDataAsync(services);

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

            var createResult = await userManager.CreateAsync(user, demoPassword);
            if (!createResult.Succeeded)
            {
                var message = string.Join(", ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Não foi possível criar o usuário demo: {message}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, adminRoleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(user, adminRoleName);
            if (!addToRoleResult.Succeeded)
            {
                var message = string.Join(", ", addToRoleResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Não foi possível vincular usuário demo à role '{adminRoleName}': {message}");
            }
        }

        if (!await context.UserTrackedProducts.AnyAsync(x => x.ApplicationUserId == user.Id))
        {
            var storesBySlug = await context.Stores.ToDictionaryAsync(x => x.Slug, StringComparer.OrdinalIgnoreCase);
            var productsByName = await context.Products.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

            var definitions = new (string Product, string Store, decimal Target, decimal? Maximum, PriceAlertTrigger Trigger)[]
            {
                ("NVIDIA RTX 4070 Super", "amazon-br", 2500.00m, 2800.00m, PriceAlertTrigger.BelowTarget),
                ("NVIDIA RTX 4070 Super", "kabum", 2520.00m, 2800.00m, PriceAlertTrigger.BelowTarget),
                ("RX 7800 XT 16GB", "pichau", 2230.00m, 2400.00m, PriceAlertTrigger.BelowTarget),
                ("SSD WD SN770 1TB", "kabum", 399.00m, 460.00m, PriceAlertTrigger.BelowMaximum),
                ("Ryzen 5 5600", "terabyte", 669.00m, null, PriceAlertTrigger.AnyReduction),
                ("Memória 16GB DDR4", "amazon-br", 309.00m, 360.00m, PriceAlertTrigger.BelowTarget),
                ("Fonte Corsair 650W", "amazon-br", 409.00m, 470.00m, PriceAlertTrigger.BelowTarget),
                ("SSD Kingston NV2 1TB", "terabyte", 369.00m, 430.00m, PriceAlertTrigger.BelowMaximum),
                ("Ryzen 7 5700X", "pichau", 1090.00m, 1200.00m, PriceAlertTrigger.BelowTarget),
                ("Fonte Corsair 650W", "mercado-livre", 429.00m, 480.00m, PriceAlertTrigger.BelowTarget),
                ("SSD WD SN770 1TB", "amazon-br", 419.00m, 460.00m, PriceAlertTrigger.BelowTarget),
                ("Ryzen 5 5600", "kabum", 679.00m, null, PriceAlertTrigger.AnyReduction)
            };

            var tracked = definitions.Select(definition => new UserTrackedProduct
            {
                ApplicationUserId = user.Id,
                ProductId = productsByName[definition.Product].Id,
                StoreId = storesBySlug[definition.Store].Id,
                TargetPrice = definition.Target,
                MaximumPrice = definition.Maximum,
                AlertTrigger = definition.Trigger,
                EmailAlertsEnabled = true,
                PushNotificationsEnabled = true,
                DailySummaryEnabled = false,
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

    private static Product CreateProduct(string name, string category, string imageUrl, decimal baselinePrice)
    {
        return new Product
        {
            Name = name,
            NormalizedName = ProductTextNormalizer.NormalizeLookupValue(name),
            Category = category,
            NormalizedCategory = ProductTextNormalizer.NormalizeLookupValue(category),
            ImageUrl = imageUrl,
            BaselinePrice = baselinePrice
        };
    }
}
