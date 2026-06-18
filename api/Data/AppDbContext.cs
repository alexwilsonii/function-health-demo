using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManager.Api.Auth;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Data;

public class AppDbContext : DbContext
{
    // Captured once per (scoped) context instance. Using a plain field is the EF-recommended
    // shape for a tenant-style global query filter — EF parameterizes it per query.
    private readonly Guid? _userId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _userId = currentUser.UserId;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot ORDER BY / compare DateTimeOffset. Store every DateTimeOffset as a UTC
        // DateTime (TEXT, sortable) so server-side sort-by-due works; it round-trips to a +00:00
        // offset. On SQL Server / Azure SQL this is unnecessary (native datetimeoffset).
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.HasMany(u => u.Tasks)
                .WithOne(t => t.User!)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(200);
            // Stored as int so ORDER BY Priority is semantic (Low < Medium < High).
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.Priority).HasConversion<int>();
            e.HasIndex(t => t.UserId);

            // Ownership at the data layer: every TaskItem query is scoped to the current user.
            // When there is no current user (unauthenticated request / design-time), _userId is null
            // and — because TaskItem.UserId is never null — no rows match. Defense in depth, not the
            // only defense: endpoints also filter explicitly and ownership is covered by tests.
            e.HasQueryFilter(t => t.UserId == _userId);
        });
    }
}

/// <summary>Stores DateTimeOffset as a UTC DateTime so SQLite can sort/compare it.</summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>
{
    public UtcDateTimeOffsetConverter()
        : base(
            d => d.UtcDateTime,
            d => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc), TimeSpan.Zero))
    {
    }
}
