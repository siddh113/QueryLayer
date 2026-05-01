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

    // Sprint 8: Workflow + Trigger Engine
    public DbSet<TriggerExecution> TriggerExecutions => Set<TriggerExecution>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAttempt> JobAttempts => Set<JobAttempt>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();

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

        modelBuilder.Entity<TriggerExecution>().ToTable("trigger_executions");
        modelBuilder.Entity<TriggerExecution>()
            .Property(t => t.RecordJson).HasColumnType("jsonb");

        modelBuilder.Entity<Job>().ToTable("jobs");
        modelBuilder.Entity<Job>()
            .Property(j => j.ActionJson).HasColumnType("jsonb");
        modelBuilder.Entity<Job>()
            .Property(j => j.ContextJson).HasColumnType("jsonb");

        modelBuilder.Entity<JobAttempt>().ToTable("job_attempts");
        modelBuilder.Entity<WorkflowExecution>().ToTable("workflow_executions");
    }
}