using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot ORDER BY / compare DateTimeOffset — store every DateTimeOffset as a UTC
        // DateTime (TEXT, sortable). Native on SQL Server / Azure SQL.
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
        });

        b.Entity<Team>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
        });

        b.Entity<TeamMembership>(e =>
        {
            e.HasKey(m => new { m.TeamId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.Team).WithMany(t => t.Memberships)
                .HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany()
                .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(200);
            // Stored as int so ORDER BY Status/Priority is semantic.
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.Priority).HasConversion<int>();
            e.HasIndex(t => t.TeamId);
            e.HasIndex(t => t.AssigneeUserId);

            // Visibility is enforced explicitly in endpoints via team membership (no global query
            // filter now). A task is deleted with its team; user FKs restrict (we never delete users).
            e.HasOne(t => t.Team).WithMany()
                .HasForeignKey(t => t.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.CreatedBy).WithMany()
                .HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Assignee).WithMany()
                .HasForeignKey(t => t.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Comment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Body).IsRequired().HasMaxLength(2000);
            e.HasIndex(c => c.TaskId);
            e.HasOne(c => c.Task).WithMany()
                .HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany()
                .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
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
