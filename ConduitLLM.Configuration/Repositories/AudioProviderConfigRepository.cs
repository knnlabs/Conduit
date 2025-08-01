using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for audio provider configurations.
    /// </summary>
    public class AudioProviderConfigRepository : IAudioProviderConfigRepository
    {
        private readonly IConfigurationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioProviderConfigRepository"/> class.
        /// </summary>
        public AudioProviderConfigRepository(IConfigurationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfig>> GetAllAsync()
        {
            try
            {
                return await _context.AudioProviderConfigs
                    .Include(c => c.Provider)
                    .OrderBy(c => c.Provider != null ? c.Provider.ProviderType : ProviderType.OpenAI)
                    .ThenByDescending(c => c.RoutingPriority)
                    .ToListAsync();
            }
            catch (Exception)
            {
                // Return empty list if database tables don't exist or there's a connection issue
                return new List<AudioProviderConfig>();
            }
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfig?> GetByIdAsync(int id)
        {
            return await _context.AudioProviderConfigs
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfig?> GetByProviderIdAsync(int ProviderId)
        {
            return await _context.AudioProviderConfigs
                .Include(c => c.Provider)
                .FirstOrDefaultAsync(c => c.ProviderId == ProviderId);
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfig>> GetByProviderTypeAsync(ProviderType providerType)
        {
            return await _context.AudioProviderConfigs
                .Include(c => c.Provider)
                .Where(c => c.Provider.ProviderType == providerType)
                .OrderByDescending(c => c.RoutingPriority)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfig>> GetEnabledForOperationAsync(string operationType)
        {
            var query = _context.AudioProviderConfigs
                .Include(c => c.Provider)
                .Where(c => c.Provider.IsEnabled);

            query = operationType.ToLower() switch
            {
                "transcription" => query.Where(c => c.TranscriptionEnabled),
                "tts" or "texttospeech" => query.Where(c => c.TextToSpeechEnabled),
                "realtime" => query.Where(c => c.RealtimeEnabled),
                _ => query
            };

            return await query
                .OrderByDescending(c => c.RoutingPriority)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfig> CreateAsync(AudioProviderConfig config)
        {
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;

            _context.AudioProviderConfigs.Add(config);
            await _context.SaveChangesAsync();

            return config;
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfig> UpdateAsync(AudioProviderConfig config)
        {
            config.UpdatedAt = DateTime.UtcNow;

            _context.AudioProviderConfigs.Update(config);
            await _context.SaveChangesAsync();

            return config;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var config = await _context.AudioProviderConfigs.FindAsync(id);
            if (config == null)
                return false;

            _context.AudioProviderConfigs.Remove(config);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsForProviderAsync(int ProviderId)
        {
            return await _context.AudioProviderConfigs
                .AnyAsync(c => c.ProviderId == ProviderId);
        }
    }
}
