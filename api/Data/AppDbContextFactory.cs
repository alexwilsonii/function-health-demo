using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TaskManager.Api.Auth;

namespace TaskManager.Api.Data;

/// <summary>
/// Lets `dotnet ef` create the context at design time (migrations) without the DI graph /
/// an HttpContext. The design-time user is anonymous, so the global filter simply matches no rows.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=taskmanager.db")
            .Options;

        return new AppDbContext(options, new AnonymousUser());
    }

    private sealed class AnonymousUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
    }
}
