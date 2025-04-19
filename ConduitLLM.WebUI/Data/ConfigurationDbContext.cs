using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Data;
using ConduitLLM.WebUI.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.WebUI.Data;

public class ConfigurationDbContext : DbContext
{
    public DbSet<DbProviderCredentials> ProviderCredentials { get; set; } = null!;
    public DbSet<DbModelProviderMapping> ModelMappings { get; set; } = null!;
    public DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;
    public DbSet<ConduitLLM.Configuration.Entities.VirtualKey> VirtualKeys { get; set; } = null!;
    public DbSet<RequestLog> RequestLogs { get; set; } = null!;
    public DbSet<RouterConfigEntity> RouterConfigurations { get; set; } = null!;
    public DbSet<ModelDeploymentEntity> ModelDeployments { get; set; } = null!;
    public DbSet<FallbackConfigurationEntity> FallbackConfigurations { get; set; } = null!;
    public DbSet<FallbackModelMappingEntity> FallbackModelMappings { get; set; } = null!;
    public DbSet<ModelCost> ModelCosts { get; set; } = null!;

    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationEntityConfigurations();

        // Add unique constraints to prevent duplicate entries
        modelBuilder.Entity<DbProviderCredentials>()
            .HasIndex(p => p.ProviderName)
            .IsUnique();

        modelBuilder.Entity<DbModelProviderMapping>()
            .HasIndex(m => m.ModelAlias)
            .IsUnique();

        modelBuilder.Entity<ConduitLLM.Configuration.Entities.VirtualKey>(entity =>
        {
            entity.HasIndex(e => e.KeyName).IsUnique();
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.ExpiresAt);

            // Configure precision for decimal properties
            entity.Property(e => e.MaxBudget).HasPrecision(18, 8);
            entity.Property(e => e.CurrentSpend).HasPrecision(18, 8);
        });
        
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasIndex(e => e.VirtualKeyId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.RequestType);
            entity.HasIndex(e => e.ModelName);
            
            // Configure precision for Cost
            entity.Property(e => e.Cost).HasPrecision(10, 6);
            
            // Configure relationship with VirtualKey
            entity.HasOne(e => e.VirtualKey)
                  .WithMany(v => v.RequestLogs)
                  .HasForeignKey(e => e.VirtualKeyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
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

        modelBuilder.Entity<ModelCost>(entity =>
        {
            entity.HasIndex(e => e.ModelIdPattern)
                  .IsUnique(false); // Patterns might not be unique if we allow overlaps
        });
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
