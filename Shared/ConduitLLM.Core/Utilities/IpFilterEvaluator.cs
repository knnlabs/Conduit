using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// The outcome of evaluating an IP address against a set of filter rules.
    /// </summary>
    /// <param name="IsAllowed">Whether the IP is allowed.</param>
    /// <param name="MatchedRule">The IP/CIDR of the rule that decided the outcome, if any.</param>
    /// <param name="Reason">A human-readable reason for the decision.</param>
    public readonly record struct IpFilterDecision(bool IsAllowed, string? MatchedRule, string Reason);

    /// <summary>
    /// The single, canonical IP-filter precedence model shared by the Gateway data plane
    /// (<c>IpFilterService</c>) and the Admin control plane (<c>AdminIpFilterService</c>), so both
    /// evaluate identically. Precedence:
    /// <list type="number">
    /// <item>Blacklist match → Deny (a block always wins).</item>
    /// <item>Any whitelist rules exist: IP matches one → Allow; otherwise → Deny (a whitelist is
    ///       restrictive by nature, so every non-listed IP is denied).</item>
    /// <item>No whitelist and not blacklisted → the <c>defaultAllow</c> policy.</item>
    /// </list>
    /// </summary>
    public static class IpFilterEvaluator
    {
        /// <summary>
        /// Evaluates <paramref name="ipAddress"/> against the given whitelist/blacklist rules.
        /// </summary>
        public static IpFilterDecision Evaluate(
            string ipAddress,
            IReadOnlyCollection<IpFilterEntity> whitelist,
            IReadOnlyCollection<IpFilterEntity> blacklist,
            bool defaultAllow)
        {
            // 1. Blacklist wins.
            foreach (var rule in blacklist)
            {
                if (IpAddressHelper.IsIpInRange(ipAddress, rule.IpAddressOrCidr))
                {
                    return new IpFilterDecision(
                        false,
                        rule.IpAddressOrCidr,
                        $"IP address matched deny rule: {rule.Description ?? rule.IpAddressOrCidr}");
                }
            }

            // 2. A whitelist is restrictive: presence of any allow rule denies everything not listed.
            if (whitelist.Count > 0)
            {
                foreach (var rule in whitelist)
                {
                    if (IpAddressHelper.IsIpInRange(ipAddress, rule.IpAddressOrCidr))
                    {
                        return new IpFilterDecision(true, rule.IpAddressOrCidr, "IP address matched allow rule");
                    }
                }

                return new IpFilterDecision(
                    false,
                    null,
                    "IP address did not match any allow rule (a whitelist is active, so all other IPs are denied)");
            }

            // 3. No whitelist and not blacklisted — apply the default policy.
            return new IpFilterDecision(
                defaultAllow,
                null,
                defaultAllow ? "Default allow" : "IP address did not match any allow rule (default deny)");
        }
    }
}
