// Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskSchedule> TaskSchedules { get; set; }
    public DbSet<TaskExecutionLog> TaskExecutionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 添加索引
        builder.Entity<TaskSchedule>()
            .HasIndex(t => new { t.Name, t.Url });
    }
}