using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.EntityConfigurations;

namespace ConduitLLM.Configuration.EntityConfigurations
{
    /// <summary>
    /// Extension methods for configuring Model-related entities in DbContext.
    /// </summary>
    public static class ModelDbContextExtensions
    {
        /// <summary>
        /// Applies all model-related entity configurations to the ModelBuilder.
        /// Call this from your DbContext.OnModelCreating method.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        public static void ApplyModelConfigurations(this ModelBuilder modelBuilder)
        {
            // Apply all configurations
            modelBuilder.ApplyConfiguration(new ModelEntityConfiguration());
            modelBuilder.ApplyConfiguration(new ModelIdentifierEntityConfiguration());
            modelBuilder.ApplyConfiguration(new ModelSeriesEntityConfiguration());
            modelBuilder.ApplyConfiguration(new ModelCapabilitiesEntityConfiguration());
            modelBuilder.ApplyConfiguration(new ModelProviderMappingEntityConfiguration());
            modelBuilder.ApplyConfiguration(new ModelAuthorEntityConfiguration());
        }
    }
}

/* 
Usage in your DbContext:

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Apply model configurations with indexes
    modelBuilder.ApplyModelConfigurations();
    
    // Apply other configurations...
}
*/