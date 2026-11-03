using System.ComponentModel.DataAnnotations;
using Shipments.Api.Data;

namespace Shipments.Api.Models;

public record CreateShipmentRequest(
    [Required]
    [RegularExpression(@"^SH-\d{4,}$", ErrorMessage = "Tracking number must look like SH-1234")]
    string TrackingNumber,

    [Required, StringLength(100, MinimumLength = 2)]
    string Origin,

    [Required, StringLength(100, MinimumLength = 2)]
    string Destination);

public record UpdateStatusRequest([Required] ShipmentStatus Status);

public record ShipmentResponse(
    string TrackingNumber,
    string Origin,
    string Destination,
    ShipmentStatus Status,
    DateTime CreatedAt,
    DateTime? DeliveredAt)
{
    public static ShipmentResponse FromEntity(Shipment s) =>
        new(s.TrackingNumber, s.Origin, s.Destination, s.Status, s.CreatedAt, s.DeliveredAt);
}