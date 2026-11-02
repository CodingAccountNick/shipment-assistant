namespace Shipments.Api.Data;

public class Shipment
{
    public int Id { get; set; }
    public required string TrackingNumber { get; set; }
    public required string Origin { get; set; }
    public required string Destination { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
}

public enum ShipmentStatus
{
    Created,
    InTransit,
    OutForDelivery,
    Delivered,
    Delayed
}