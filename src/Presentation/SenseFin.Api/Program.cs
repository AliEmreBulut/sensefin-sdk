using System.Text.Json.Serialization;
using SenseFin.Api.Middlewares;
using SenseFin.Application.Features.FraudEvaluation.Commands;
using SenseFin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enable string-to-enum deserialization (e.g., "Transfer" → TransactionType.Transfer)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Altyapı servislerini (DB, Redis, AI vb.) kaydet
builder.Services.AddInfrastructure(builder.Configuration);

// MediatR (Handler'ları otomatik bulur)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<AnalyzeTransactionHandler>());

var app = builder.Build();

// Veritabanını oluştur ve örnek verileri yükle
using (var scope = app.Services.CreateScope())
{
    await SenseFin.Infrastructure.Persistence.SenseFinDataSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only enforce HTTPS redirect in production (Docker containers use HTTP internally)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// HMAC Doğrulama (Controller'lardan önce çalışmalı)
app.UseMiddleware<HmacVerificationMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

