using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Configurations;
using PromoRadar.Web.Data;
using PromoRadar.Web.Models;
using PromoRadar.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.Configure<StartupTasksOptions>(options =>
{
    // Defaults seguros: tudo automático apenas em Development, com opt-in explícito fora dele.
    options.ApplyMigrationsOnStartup = builder.Environment.IsDevelopment();
    options.SeedReferenceDataOnStartup = builder.Environment.IsDevelopment();
    options.SeedDemoDataOnStartup = builder.Environment.IsDevelopment();
    options.ScheduleRecurringJobsOnStartup = builder.Environment.IsDevelopment();
    builder.Configuration.GetSection("StartupTasks").Bind(options);
});

builder.Services.AddHangfire(configuration =>
{
    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(storageOptions => storageOptions.UseNpgsqlConnection(connectionString));
});

var shouldRunHangfireServer = builder.Environment.IsDevelopment() ||
                              builder.Configuration.GetValue<bool>("StartupTasks:ScheduleRecurringJobsOnStartup");
if (shouldRunHangfireServer)
{
    builder.Services.AddHangfireServer();
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPriceProvider, SimulatedPriceProvider>();
builder.Services.AddScoped<PriceMonitoringJobService>();
builder.Services.AddScoped<ITrackedProductCreationService, TrackedProductCreationService>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter()]
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages().WithStaticAssets();

var startupTasks = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<StartupTasksOptions>>().Value;

if (startupTasks.ApplyMigrationsOnStartup)
{
    await app.Services.ApplyMigrationsAsync();
}

if (startupTasks.SeedReferenceDataOnStartup)
{
    await app.Services.SeedReferenceDataAsync();
}

if (startupTasks.ApplyMigrationsOnStartup || startupTasks.SeedReferenceDataOnStartup)
{
    await app.Services.EnsureRoleAsync(AppRoles.Administrator);
}

if (startupTasks.SeedDemoDataOnStartup)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("A seed de usuário demo só pode ser habilitada em Development.");
    }

    var demoPassword = app.Configuration["DevelopmentSeed:DemoPassword"];
    if (string.IsNullOrWhiteSpace(demoPassword))
    {
        throw new InvalidOperationException(
            "Defina 'DevelopmentSeed:DemoPassword' em appsettings.Development.json ou User Secrets para criar o usuário demo.");
    }

    await app.Services.SeedDevelopmentDemoAsync(AppRoles.Administrator, demoPassword);
}

if (startupTasks.ScheduleRecurringJobsOnStartup)
{
    RecurringJob.AddOrUpdate<PriceMonitoringJobService>(
        "price-scan-hourly",
        job => job.RunPriceScanAsync(),
        "0 * * * *");
}

app.Run();
