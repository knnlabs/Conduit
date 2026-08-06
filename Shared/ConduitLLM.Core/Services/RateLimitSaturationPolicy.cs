namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Saturation-aware priority shedding for group rate limits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A group's windows are shared by every key in it, so a noisy batch key can spend the
    /// whole allowance and crowd out a critical key in the same group. The remedy: a key whose
    /// <c>RateLimitPriority</c> is below normal is admitted against
    /// <c>floor(groupLimit × threshold)</c> rather than the full group ceiling. Once the shared
    /// window's fill passes the threshold, low-priority keys are shed while normal- and
    /// high-priority keys still see the remaining headroom.
    /// </para>
    /// <para>
    /// The reduced ceiling is evaluated inside the same atomic multi-window check as every
    /// other limit, so the decision cannot race the fill it reads, and — unlike a
    /// read-then-decide design — there is no shedding mode to flap: a low-priority key near
    /// the boundary sees the same deterministic 429-with-Retry-After contract as any limit.
    /// The capped window keeps its own key, only its ceiling and scope differ, and it reports
    /// a distinct <c>{scope}:saturation</c> scope so a caller can tell "your tenant is busy"
    /// from "you are over your own limit".
    /// </para>
    /// <para>
    /// A group with no ceiling contributes no window, so priority is inert there. A very small
    /// group ceiling can floor to zero, which excludes low-priority keys from that window
    /// entirely — the whole allowance is inside the reserved headroom.
    /// </para>
    /// </remarks>
    public static class RateLimitSaturationPolicy
    {
        /// <summary>Shed first when the key's group is saturated.</summary>
        public const int LowPriority = 0;

        /// <summary>The default tier; unset priority means this.</summary>
        public const int NormalPriority = 1;

        /// <summary>Behaves as normal today; reserved for future refinement.</summary>
        public const int HighPriority = 2;

        /// <summary>Appended to a group scope whose ceiling was capped for a low-priority key.</summary>
        public const string ScopeSuffix = ":saturation";

        /// <summary>
        /// The fraction of each group ceiling this key is admitted against, or null when the
        /// full ceiling applies (normal/high priority, or shedding disabled).
        /// </summary>
        /// <param name="priority">The key's tier; null means normal.</param>
        /// <param name="threshold">The deployment's saturation threshold; 1 disables shedding.</param>
        public static double? SaturationFractionFor(int? priority, double threshold)
        {
            if (priority is not < NormalPriority)
            {
                return null;
            }

            return threshold > 0 && threshold < 1 ? threshold : null;
        }

        /// <summary>Reduces a group ceiling to the shed tier's share of it.</summary>
        public static long Cap(long limit, double fraction) => (long)Math.Floor(limit * fraction);

        /// <summary>True when a denial came from a saturation-capped group window.</summary>
        public static bool IsSaturationScope(string scope) =>
            scope.EndsWith(ScopeSuffix, StringComparison.Ordinal);
    }
}
