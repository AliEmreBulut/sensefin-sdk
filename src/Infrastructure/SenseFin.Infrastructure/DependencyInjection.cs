using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SenseFin.Application.Interfaces;
using SenseFin.Infrastructure.AiServices;
using SenseFin.Infrastructure.Caching;
using SenseFin.Infrastructure.Persistence;
using SenseFin.Infrastructure.Persistence.Interceptors;
using SenseFin.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace SenseFin.Infrastructure;

// Altyapı servislerini bağımlılık konteynerine (DI) kaydeden extension metotlar.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Veritabanı (PostgreSQL) ayarları

        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<SenseFinDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("PostgreSQL")
                ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(SenseFinDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        // Repository kayıtları

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IRiskProfileRepository, RiskProfileRepository>();
        services.AddScoped<IBlacklistRepository, BlacklistRepository>();

        // Redis ayarları

        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<IVelocityService, RedisVelocityService>();
        }

        // AI Servisleri (Gemini)

        services.AddHttpClient<GeminiRiskAnalystService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddScoped<IRiskAnalystService, GeminiRiskAnalystService>();

        return services;
    }
}
