using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Models;

namespace PromoRadar.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<UserTrackedProduct> UserTrackedProducts => Set<UserTrackedProduct>();

    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();

    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(entity =>
        {
            entity.Property(x => x.BaselinePrice).HasPrecision(12, 2);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Category).HasMaxLength(80);
        });

        builder.Entity<Store>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(80);
            entity.Property(x => x.AccentColor).HasMaxLength(20);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<UserTrackedProduct>(entity =>
        {
            entity.Property(x => x.TargetPrice).HasPrecision(12, 2);
            entity.HasIndex(x => new { x.ApplicationUserId, x.IsActive });
            entity.HasOne(x => x.ApplicationUser)
                .WithMany()
                .HasForeignKey(x => x.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PriceSnapshot>(entity =>
        {
            entity.Property(x => x.Price).HasPrecision(12, 2);
            entity.HasIndex(x => new { x.UserTrackedProductId, x.CapturedAtUtc });
        });

        builder.Entity<PriceAlert>(entity =>
        {
            entity.Property(x => x.TriggerPrice).HasPrecision(12, 2);
            entity.Property(x => x.Title).HasMaxLength(140);
            entity.Property(x => x.Note).HasMaxLength(220);
            entity.HasIndex(x => new { x.UserTrackedProductId, x.CreatedAtUtc });
        });
    }
}

