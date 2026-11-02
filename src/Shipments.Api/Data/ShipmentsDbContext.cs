using Microsoft.EntityFrameworkCore;

namespace Shipments.Api.Data;

public class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(s => s.TrackingNumber).IsUnique();
            entity.Property(s => s.TrackingNumber).HasMaxLength(20);
            entity.Property(s => s.Origin).HasMaxLength(100);
            entity.Property(s => s.Destination).HasMaxLength(100);

            // Store the status as readable text ("InTransit") instead of a number
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}