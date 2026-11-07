using Microsoft.EntityFrameworkCore;
using Shipments.Api.Data;
using Shipments.Api.Services;
using Shipments.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddHttpClient<AiServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000"));

builder.Services.AddSingleton<DemoWorker>();
builder.Services.AddSingleton<RabbitMqPublisher>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ShipmentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Shipments")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();
    db.Database.Migrate();

    SeedData.Initialize(db);

}

app.UseHttpsRedirection();

app.UseAuthorization();


app.MapControllers();


app.Run();

public partial class Program { }