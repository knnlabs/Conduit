using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Data;
using ConduitLLM.WebUI.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.WebUI.Data;

public class ConfigurationDbContext : DbContext
{
    // Removed DbSet<DbProviderCredentials> ProviderCredentials
    public DbSet<DbModelProviderMapping> ModelMappings { get; set; } = null!;
    // Removed DbSet<GlobalSetting> GlobalSettings
    // Removed DbSet<ConduitLLM.Configuration.Entities.VirtualKey> VirtualKeys
    // Removed DbSet<RequestLog> RequestLogs
    public DbSet<RouterConfigEntity> RouterConfigurations { get; set; } = null!;
    public DbSet<ModelDeploymentEntity> ModelDeployments { get; set; } = null!;
    public DbSet<FallbackConfigurationEntity> FallbackConfigurations { get; set; } = null!;
    public DbSet<FallbackModelMappingEntity> FallbackModelMappings { get; set; } = null!;
    // Removed DbSet<ModelCost> ModelCosts

    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationEntityConfigurations();

        // Removed configuration for DbProviderCredentials

        modelBuilder.Entity<DbModelProviderMapping>()
            .HasIndex(m => m.ModelAlias)
            .IsUnique();

        // Removed configuration for VirtualKey
        
        // Removed configuration for RequestLog
        
        // Configure Router entities
        modelBuilder.Entity<RouterConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.LastUpdated);
            
            // Configure relationships with model deployments and fallback configurations
            entity.HasMany(e => e.ModelDeployments)
                  .WithOne(e => e.RouterConfig)
                  .HasForeignKey(e => e.RouterConfigId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasMany(e => e.FallbackConfigurations)
                  .WithOne(e => e.RouterConfig)
                  .HasForeignKey(e => e.RouterConfigId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<ModelDeploymentEntity>(entity =>
        {
            entity.HasIndex(e => e.ModelName);
            entity.HasIndex(e => e.ProviderName);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.IsHealthy);
        });
        
        modelBuilder.Entity<FallbackConfigurationEntity>(entity =>
        {
            entity.HasIndex(e => e.PrimaryModelDeploymentId);
            
            // Configure relationship with fallback model mappings
            entity.HasMany(e => e.FallbackMappings)
                  .WithOne(e => e.FallbackConfiguration)
                  .HasForeignKey(e => e.FallbackConfigurationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<FallbackModelMappingEntity>(entity =>
        {
            entity.HasIndex(e => new { e.FallbackConfigurationId, e.Order }).IsUnique();
            entity.HasIndex(e => new { e.FallbackConfigurationId, e.ModelDeploymentId }).IsUnique();
        });

        // Removed configuration for ModelCost
    }

    // It's generally better to configure the database connection in Program.cs
    // using builder.Services.AddDbContext, but this shows how it could be done here.
    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // {
    //     if (!optionsBuilder.IsConfigured)
    //     {
    //         // Example: Use a file named 'config.db' in the application's base directory
    //         optionsBuilder.UseSqlite("Data Source=config.db");
    //     }
    // }
}
