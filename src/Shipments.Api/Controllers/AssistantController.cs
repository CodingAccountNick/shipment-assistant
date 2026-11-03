using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shipments.Api.Data;
using Shipments.Api.Services;

namespace Shipments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController(AiServiceClient ai, ShipmentsDbContext db) : ControllerBase
{
    // Finds tracking numbers like SH-1001 anywhere in a question
    private static readonly Regex TrackingNumberPattern =
        new(@"\bSH-\d{4,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // POST api/assistant/ask
    [HttpPost("ask")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AskResponse>> Ask(AskRequest request, CancellationToken ct)  
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Question is empty",
                detail: "Please enter a question.");

        // Retrieval: look up any shipments the question mentions
        var context = await BuildShipmentContextAsync(request.Question, ct);

        try
        {
            var answer = await ai.AskAsync(request.Question, context, ct);
            return Ok(new AskResponse(answer));
        }
        catch (HttpRequestException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI service is not available",
                detail: "The assistant can't answer right now. Please try again later.");
        }
    }

    private async Task<string?> BuildShipmentContextAsync(string question, CancellationToken ct)
    {
        var trackingNumbers = TrackingNumberPattern.Matches(question)
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct()
            .ToList();

        if (trackingNumbers.Count == 0)
            return null;   // nothing to look up: the AI answers from general knowledge

        var shipments = await db.Shipments
            .AsNoTracking()
            .Where(s => trackingNumbers.Contains(s.TrackingNumber))
            .ToListAsync(ct);

        var lines = trackingNumbers.Select(number =>
        {
            var s = shipments.FirstOrDefault(x => x.TrackingNumber == number);
            if (s is null)
                return $"Shipment {number}: not found in the system.";

            var delivery = s.DeliveredAt is null
                ? "not delivered yet"
                : $"delivered on {s.DeliveredAt:yyyy-MM-dd}";

            return $"Shipment {s.TrackingNumber}: from {s.Origin} to {s.Destination}, " +
                   $"status {s.Status}, created on {s.CreatedAt:yyyy-MM-dd}, {delivery}.";
        });

        return string.Join("\n", lines);
    }
}