using Microsoft.EntityFrameworkCore;

namespace PromoRadar.Web.Data;

public static class DbInitializerExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var context = scopedServices.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public static async Task SeedReferenceDataAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await DataSeeder.SeedReferenceDataAsync(scope.ServiceProvider);
    }

    public static async Task EnsureRoleAsync(this IServiceProvider services, string roleName)
    {
        using var scope = services.CreateScope();
        await DataSeeder.EnsureRoleAsync(scope.ServiceProvider, roleName);
    }

    public static async Task SeedDevelopmentDemoAsync(this IServiceProvider services, string adminRoleName, string demoPassword)
    {
        using var scope = services.CreateScope();
        await DataSeeder.SeedDevelopmentDemoAsync(scope.ServiceProvider, adminRoleName, demoPassword);
    }
}
