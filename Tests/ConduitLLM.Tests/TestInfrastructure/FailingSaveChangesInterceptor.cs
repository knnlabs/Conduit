using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConduitLLM.Tests.TestInfrastructure;

/// <summary>
/// Deterministically fails a selected SaveChanges call after being armed.
/// Schema creation and seed operations can complete before fault injection starts.
/// </summary>
public sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _remainingCalls = -1;
    private Func<Exception> _exceptionFactory =
        () => new DbUpdateException("Injected SaveChanges failure.");

    public void Arm(int failOnCall = 1, Func<Exception>? exceptionFactory = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failOnCall, 1);
        _exceptionFactory = exceptionFactory
            ?? (() => new DbUpdateException("Injected SaveChanges failure."));
        Volatile.Write(ref _remainingCalls, failOnCall);
    }

    public void Disarm() => Volatile.Write(ref _remainingCalls, -1);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfArmed();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfArmed();
        return ValueTask.FromResult(result);
    }

    private void ThrowIfArmed()
    {
        while (true)
        {
            var remaining = Volatile.Read(ref _remainingCalls);
            if (remaining < 1)
            {
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _remainingCalls,
                    remaining - 1,
                    remaining) != remaining)
            {
                continue;
            }

            if (remaining == 1)
            {
                throw _exceptionFactory();
            }

            return;
        }
    }
}
