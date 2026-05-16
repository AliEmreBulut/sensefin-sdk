using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SenseFin.Infrastructure.Persistence;

// Tasarım zamanı (design-time) DbContext factory.
// Migrations işlemleri için (dotnet ef CLI) kullanılır.
public sealed class SenseFinDbContextFactory : IDesignTimeDbContextFactory<SenseFinDbContext>
{
    public SenseFinDbContext CreateDbContext(string[] args)
    {
        // appsettings dosyasından yapılandırmayı oku
        //
        // The factory looks for appsettings in the Api (startup) project.
        // When running `dotnet ef` with --startup-project, the working
        // directory is set to the startup project's folder.

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? "Host=localhost;Port=5432;Database=sensefin_db;Username=sensefin_user;Password=sensefin_password";

        // DbContextOptions nesnesini oluştur

        var optionsBuilder = new DbContextOptionsBuilder<SenseFinDbContext>();

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(SenseFinDbContext).Assembly.FullName);
        });

        return new SenseFinDbContext(optionsBuilder.Options);
    }
}
