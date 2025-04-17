namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Interface for managing model-provider mappings.
    /// Placeholder implementation.
    /// </summary>
    public interface IModelProviderMappingService
    {
        Task<ModelProviderMapping?> GetMappingByIdAsync(int id);
        Task<List<ModelProviderMapping>> GetAllMappingsAsync();
        Task AddMappingAsync(ModelProviderMapping mapping);
        Task UpdateMappingAsync(ModelProviderMapping mapping);
        Task DeleteMappingAsync(int id);
        Task<ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias); // Example method
    }
}
