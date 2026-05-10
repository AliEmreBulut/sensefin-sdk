using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SenseFin.Application.Interfaces;
using SenseFin.Infrastructure.Caching;
using SenseFin.Infrastructure.Persistence;
using SenseFin.Infrastructure.Persistence.Interceptors;
using SenseFin.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace SenseFin.Infrastructure;

/// <summary>
/// Extension methods to register all Infrastructure services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── EF Core + PostgreSQL ────────────────────────────────

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

        // ─── Repositories ────────────────────────────────────────

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IRiskProfileRepository, RiskProfileRepository>();

        // ─── Redis ───────────────────────────────────────────────

        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddSingleton<RedisCacheService>();
        }

        return services;
    }
}
