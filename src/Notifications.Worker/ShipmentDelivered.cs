namespace Notifications.Worker;

// This service's own copy of the event. The contract is the JSON, not shared code.
public record ShipmentDelivered(
    string TrackingNumber,
    string Origin,
    string Destination,
    DateTime DeliveredAt);