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
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectSpec>().ToTable("project_specs");
        modelBuilder.Entity<ProjectKey>().ToTable("project_keys");

        modelBuilder.Entity<ProjectSpec>()
            .Property(x => x.SpecJson)
            .HasColumnName("spec_json");

        modelBuilder.Entity<ProjectKey>()
            .Property(x => x.ApiKeyHash)
            .HasColumnName("api_key_hash");

        modelBuilder.Entity<Project>()
            .Property(x => x.OwnerUserId)
            .HasColumnName("owner_user_id");

        modelBuilder.Entity<User>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Project>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<ProjectSpec>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<ProjectSpec>()
            .Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<ProjectKey>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");
    }
}