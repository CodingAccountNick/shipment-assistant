using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shipments.Api.Data;
using Shipments.Api.Messaging;
using Shipments.Api.Models;
using Shipments.Api.Messaging;

namespace Shipments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController(ShipmentsDbContext db,RabbitMqPublisher publisher,ILogger<ShipmentsController> logger) : ControllerBase
{
    // GET api/shipments
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShipmentResponse>>> GetAll(CancellationToken ct)
    {
        var shipments = await db.Shipments
            .AsNoTracking()
            .OrderBy(s => s.TrackingNumber)
            .ToListAsync(ct);

        return shipments.Select(ShipmentResponse.FromEntity).ToList();
    }

    // GET api/shipments/SH-1001
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> GetByTrackingNumber(string trackingNumber, CancellationToken ct)
    {
        var shipment = await db.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, ct);

        return shipment is null
      ? Problem(statusCode: StatusCodes.Status404NotFound,
          title: "Shipment not found",
          detail: $"Shipment {trackingNumber} not found")
      : ShipmentResponse.FromEntity(shipment);
    }

    // POST api/shipments
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentResponse>> Create(CreateShipmentRequest request, CancellationToken ct)
    {
        if (await db.Shipments.AnyAsync(s => s.TrackingNumber == request.TrackingNumber, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Shipment already exists",
                detail: $"Shipment {request.TrackingNumber} already exists");

        var shipment = new Shipment
        {
            TrackingNumber = request.TrackingNumber,
            Origin = request.Origin,
            Destination = request.Destination
        };

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);

        var response = ShipmentResponse.FromEntity(shipment);
        return CreatedAtAction(nameof(GetByTrackingNumber), new { trackingNumber = shipment.TrackingNumber }, response);
    }

    // PATCH api/shipments/SH-1001/status
    [HttpPatch("{trackingNumber}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, ct);
        if (shipment is null)
             return Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment not found",
                    detail: $"Shipment {trackingNumber} not found");

        // Business rules for status changes live in ShipmentStatusRules
        if (!ShipmentStatusRules.CanChange(shipment.Status, request.Status))
            return Problem(statusCode: StatusCodes.Status409Conflict,
               title: "Status change not allowed",
               detail: $"Shipment {trackingNumber} can't change from {shipment.Status} to {request.Status}");

        shipment.Status = request.Status;
        if (request.Status == ShipmentStatus.Delivered)
            shipment.DeliveredAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);



        // Tell the rest of the system, but only after the change is saved
        if (shipment.Status == ShipmentStatus.Delivered)
        {
            try
            {
                await publisher.PublishAsync("shipment.delivered",
                    new ShipmentDelivered(shipment.TrackingNumber, shipment.Origin,
                        shipment.Destination, shipment.DeliveredAt!.Value), ct);
            }
            catch (Exception ex)
            {
                // The delivery is saved either way; a failed notification shouldn't fail the request
                logger.LogError(ex, "Could not publish ShipmentDelivered for {TrackingNumber}", shipment.TrackingNumber);
            }
        }



        return ShipmentResponse.FromEntity(shipment);
    }
}                                                               