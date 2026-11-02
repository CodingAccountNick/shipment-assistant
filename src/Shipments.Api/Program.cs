using Microsoft.EntityFrameworkCore;
using Shipments.Api.Data;
using Shipments.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient<AiServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000"));
builder.Services.AddSingleton<DemoWorker>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ShipmentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Shipments")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();
    db.Database.Migrate();

}

app.UseHttpsRedirection();

app.UseAuthorization();


app.MapControllers();


app.Run();
