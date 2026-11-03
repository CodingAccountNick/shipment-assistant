namespace Shipments.Api.Data;

public static class ShipmentStatusRules
{
    // For each status: which statuses it may change to next
    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]> AllowedChanges = new()
    {
        [ShipmentStatus.Created] = [ShipmentStatus.InTransit, ShipmentStatus.Delayed],
        [ShipmentStatus.InTransit] = [ShipmentStatus.OutForDelivery, ShipmentStatus.Delayed],
        [ShipmentStatus.OutForDelivery] = [ShipmentStatus.Delivered, ShipmentStatus.Delayed],
        [ShipmentStatus.Delayed] = [ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery],
        [ShipmentStatus.Delivered] =[] //[ShipmentStatus.InTransit] test fail
    };

    public static bool CanChange(ShipmentStatus from, ShipmentStatus to) =>
        AllowedChanges.TryGetValue(from, out var allowed) && allowed.Contains(to);
}