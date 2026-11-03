using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Shipments.Api.Tests;

// Starts the real API in memory, connected to a throwaway PostgreSQL container
public class ShipmentsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")   // same image as docker-compose.yml
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Point the API at the test container instead of your dev database
        builder.UseSetting("ConnectionStrings:Shipments", _db.GetConnectionString());

        // Development mode runs migrations and adds the sample shipments
        builder.UseEnvironment("Development");
    }

    // xUnit calls this before the tests: start the database container
    public Task InitializeAsync() => _db.StartAsync();

    // ...and this after the tests: delete the container
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}