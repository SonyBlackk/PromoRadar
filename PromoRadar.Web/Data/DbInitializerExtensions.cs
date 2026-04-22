using Microsoft.EntityFrameworkCore;

namespace PromoRadar.Web.Data;

public static class DbInitializerExtensions
{
    public static async Task ApplyMigrationsAndSeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var context = scopedServices.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        await DataSeeder.SeedAsync(scopedServices);
    }
}

