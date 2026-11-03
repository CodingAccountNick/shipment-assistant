using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shipments.Api.Tests;

public class ShipmentsEndpointTests(ShipmentsApiFactory factory) : IClassFixture<ShipmentsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---------- GET ----------

    [Fact]
    public async Task GetAll_ReturnsSeededShipments()
    {
        // Act
        var response = await _client.GetAsync("/api/shipments");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SH-1001", body);
    }

    // ---------- POST ----------

    [Fact]
    public async Task Create_NewShipment_Returns201WithLocation()
    {
        // Act
        var response = await CreateShipmentAsync("SH-5001", "London", "Prague");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_DuplicateTrackingNumber_Returns409()
    {
        // Arrange
        await CreateShipmentAsync("SH-5002", "Vienna", "Munich");

        // Act
        var response = await CreateShipmentAsync("SH-5002", "Vienna", "Munich");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidTrackingNumber_Returns400()
    {
        // Act
        var response = await CreateShipmentAsync("ABC", "Prague", "Bratislava");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- PATCH status ----------

    [Fact]
    public async Task UpdateStatus_UnknownShipment_Returns404()
    {
        // Act
        var response = await UpdateStatusAsync("SH-9999", "InTransit");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Shipment not found", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UpdateStatus_FullJourneyToDelivered_SetsDeliveredAt()
    {
        // Arrange: a new shipment moved along the allowed path
        await CreateShipmentAsync("SH-5003", "Bratislava", "Vienna");
        await UpdateStatusAsync("SH-5003", "InTransit");
        await UpdateStatusAsync("SH-5003", "OutForDelivery");

        // Act
        var response = await UpdateStatusAsync("SH-5003", "Delivered");

        // Assert
        response.EnsureSuccessStatusCode();
        var shipment = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Delivered", shipment.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, shipment.GetProperty("deliveredAt").ValueKind);
    }

    [Fact]
    public async Task UpdateStatus_NotAllowedChange_Returns409()
    {
        // Act: SH-1004 is seeded as Delivered, and delivered is final
        var response = await UpdateStatusAsync("SH-1004", "InTransit");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------- Helpers ----------

    private Task<HttpResponseMessage> CreateShipmentAsync(string trackingNumber, string origin, string destination) =>
        _client.PostAsJsonAsync("/api/shipments", new { trackingNumber, origin, destination });

    private Task<HttpResponseMessage> UpdateStatusAsync(string trackingNumber, string status) =>
        _client.PatchAsJsonAsync($"/api/shipments/{trackingNumber}/status", new { status });
}