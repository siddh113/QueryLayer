using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Models;

namespace QueryLayer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSpec> ProjectSpecs => Set<ProjectSpec>();
    public DbSet<ProjectKey> ProjectKeys => Set<ProjectKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table names are explicit because EF snake_case convention uses singular form
        // but our Supabase tables use plural snake_case names
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectSpec>().ToTable("project_specs");
        modelBuilder.Entity<ProjectKey>().ToTable("project_keys");
    }
}