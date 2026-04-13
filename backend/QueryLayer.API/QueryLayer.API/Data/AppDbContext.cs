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
    public DbSet<ProjectApiKey> ProjectApiKeys => Set<ProjectApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectSpec>().ToTable("project_specs");
        modelBuilder.Entity<ProjectSpec>()
            .Property(p => p.SpecJson)
            .HasColumnType("jsonb");
        modelBuilder.Entity<ProjectKey>().ToTable("project_keys");
        modelBuilder.Entity<ProjectApiKey>().ToTable("project_api_keys");
    }
}