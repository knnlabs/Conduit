using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>
/// Applies DataAnnotations validation to Minimal-API handler arguments and returns the Admin
/// validation envelope used by the Admin API contract.
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var errors = new List<string>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var argument in context.Arguments)
        {
            if (argument is null || IsFrameworkOrServiceArgument(argument, context.HttpContext.RequestServices))
            {
                continue;
            }

            ValidateObjectGraph(argument, null, errors, visited);
        }

        return errors.Count == 0
            ? next(context)
            : ValueTask.FromResult<object?>(AdminResults.ValidationError(string.Join("; ", errors)));
    }

    private static void ValidateObjectGraph(
        object value,
        string? path,
        ICollection<string> errors,
        ISet<object> visited)
    {
        var type = value.GetType();
        if (IsSimple(type) || (!type.IsValueType && !visited.Add(value)))
        {
            return;
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        foreach (var result in results)
        {
            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "The value is invalid."
                : result.ErrorMessage!;
            var members = result.MemberNames.DefaultIfEmpty(string.Empty);
            foreach (var member in members)
            {
                var memberPath = Join(path, member);
                errors.Add(string.IsNullOrEmpty(memberPath) ? message : $"{memberPath}: {message}");
            }
        }

        if (value is IEnumerable enumerable and not string)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    ValidateObjectGraph(item, $"{path ?? string.Empty}[{index}]", errors, visited);
                }
                index++;
            }
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || IsSimple(property.PropertyType))
            {
                continue;
            }

            object? child;
            try
            {
                child = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (child is not null)
            {
                ValidateObjectGraph(child, Join(path, property.Name), errors, visited);
            }
        }
    }

    private static bool IsFrameworkOrServiceArgument(object value, IServiceProvider services)
    {
        var type = value.GetType();
        return value is HttpContext or HttpRequest or HttpResponse or CancellationToken
            || typeof(ILogger).IsAssignableFrom(type)
            || services.GetService(type) is not null;
    }

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan)
            || type == typeof(Guid) || type == typeof(Uri);
    }

    private static string Join(string? path, string member) =>
        string.IsNullOrEmpty(path) ? member
        : string.IsNullOrEmpty(member) ? path
        : $"{path}.{member}";

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
