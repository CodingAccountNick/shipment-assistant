namespace Shipments.Api.Messaging;

// The event's content: what any listener needs to know about the delivery
public record ShipmentDelivered(
    string TrackingNumber,
    string Origin,
    string Destination,
    DateTime DeliveredAt);