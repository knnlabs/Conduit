using System.Text;

namespace ConduitLLM.Gateway.UsageTracking;

public sealed class BoundedStringAccumulator
{
    private readonly StringBuilder _builder = new();

    public BoundedStringAccumulator(int maximumCharacters)
    {
        if (maximumCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        }

        MaximumCharacters = maximumCharacters;
    }

    public int MaximumCharacters { get; }

    public int Length => _builder.Length;

    public long TotalCharactersObserved { get; private set; }

    public bool LimitExceeded { get; private set; }

    public void Append(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        TotalCharactersObserved += value.Length;
        var remaining = MaximumCharacters - _builder.Length;
        if (remaining <= 0)
        {
            LimitExceeded = true;
            return;
        }

        if (value.Length > remaining)
        {
            _builder.Append(value.AsSpan(0, remaining));
            LimitExceeded = true;
            return;
        }

        _builder.Append(value);
    }

    public override string ToString() => _builder.ToString();
}
