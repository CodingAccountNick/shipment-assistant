namespace Shipments.Api.Data;

public static class SeedData
{
    public static void Initialize(ShipmentsDbContext db)
    {
        // Only seed an empty database, so restarts don't add duplicates
        if (db.Shipments.Any())
            return;

        db.Shipments.AddRange(
            new Shipment { TrackingNumber = "SH-1001", Origin = "London", Destination = "Vienna", Status = ShipmentStatus.InTransit },
            new Shipment { TrackingNumber = "SH-1002", Origin = "Prague", Destination = "Munich", Status = ShipmentStatus.OutForDelivery },
            new Shipment { TrackingNumber = "SH-1003", Origin = "Vienna", Destination = "Prague", Status = ShipmentStatus.Delayed },
            new Shipment { TrackingNumber = "SH-1004", Origin = "Bratislava", Destination = "London", Status = ShipmentStatus.Delivered, DeliveredAt = DateTime.UtcNow.AddDays(-1) });

        db.SaveChanges();
    }
}