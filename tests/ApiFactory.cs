using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;

namespace TaskManager.Tests;

/// <summary>
/// Spins up the real API against an isolated temp-file SQLite database (so persistence across a
/// fresh DbContext is genuinely exercised). Runs in the Development environment so (a) the dev JWT
/// key from appsettings.Development.json is used consistently by both the token issuer and the
/// validator, and (b) the auth cookie's Secure flag is off, letting the in-memory HttpClient
/// round-trip it over http.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tm-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Service-level override (applied after the app's own registrations, unlike config read at
        // build time) repoints the DbContext at an isolated temp database per factory.
        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.FullName?.StartsWith(
                        "Microsoft.EntityFrameworkCore.IDbContextOptionsConfiguration") ?? false))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
            }
            catch
            {
                /* best effort */
            }
        }
    }
}
