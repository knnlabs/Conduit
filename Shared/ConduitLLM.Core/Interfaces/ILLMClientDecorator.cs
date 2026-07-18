namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Implemented by <see cref="ILLMClient"/> decorators so callers can traverse a decorator
    /// chain and reach capabilities exposed only by the innermost provider client.
    /// </summary>
    /// <remarks>
    /// Some capabilities (e.g. video generation, provider-specific progress callbacks) are not
    /// part of <see cref="ILLMClient"/> and are discovered via reflection on the concrete client.
    /// Without this interface a decorator hides those members, silently dropping the capability
    /// even though the wrapped provider client supports it (see issue #976).
    /// </remarks>
    public interface ILLMClientDecorator
    {
        /// <summary>
        /// Gets the client wrapped by this decorator. May itself be another decorator.
        /// </summary>
        ILLMClient InnerClient { get; }
    }

    /// <summary>
    /// Helpers for working with <see cref="ILLMClientDecorator"/> chains.
    /// </summary>
    public static class LLMClientDecoratorExtensions
    {
        /// <summary>
        /// Unwraps a decorator chain and returns the innermost (non-decorator) client.
        /// Returns the client itself when it is not a decorator.
        /// </summary>
        /// <param name="client">The potentially decorated client.</param>
        /// <returns>The innermost client in the chain.</returns>
        public static ILLMClient UnwrapInnermost(this ILLMClient client)
        {
            while (client is ILLMClientDecorator decorator)
            {
                client = decorator.InnerClient;
            }

            return client;
        }
    }
}
