using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Represents a configuration source for loading settings from an Entity Framework Core DbContext.
/// </summary>
public class EntityFrameworkConfigurationSource : IConfigurationSource
{
    private readonly Action<DbContextOptionsBuilder> _optionsAction;

    public EntityFrameworkConfigurationSource(Action<DbContextOptionsBuilder> optionsAction)
    {
        _optionsAction = optionsAction ?? throw new ArgumentNullException(nameof(optionsAction));
    }

    /// <summary>
    /// Builds the EntityFrameworkConfigurationProvider for this source.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>An EntityFrameworkConfigurationProvider.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EntityFrameworkConfigurationProvider(_optionsAction);
    }
}

/// <summary>
/// Extension methods for adding EntityFrameworkConfigurationProvider to an IConfigurationBuilder.
/// </summary>
public static class EntityFrameworkConfigurationExtensions
{
    /// <summary>
    /// Adds an Entity Framework Core configuration source to the builder.
    /// </summary>
    /// <param name="builder">The configuration builder to add to.</param>
    /// <param name="optionsAction">An action to configure the DbContextOptions for the context.</param>
    /// <returns>The IConfigurationBuilder.</returns>
    public static IConfigurationBuilder AddEntityFrameworkConfiguration(
        this IConfigurationBuilder builder,
        Action<DbContextOptionsBuilder> optionsAction)
    {
        return builder.Add(new EntityFrameworkConfigurationSource(optionsAction));
    }
}
