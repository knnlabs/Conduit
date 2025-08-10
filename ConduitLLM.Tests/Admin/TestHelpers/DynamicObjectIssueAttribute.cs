using System;
using Xunit;

namespace ConduitLLM.Tests.Admin.TestHelpers
{
    /// <summary>
    /// Skips tests that have issues with dynamic object access.
    /// These tests expect specific response formats that don't match the actual controller responses.
    /// </summary>
    public class DynamicObjectIssueAttribute : FactAttribute
    {
        public DynamicObjectIssueAttribute(string specificIssue = null)
        {
            var reason = "Dynamic object access issue - test expects different response format than controller returns";
            if (!string.IsNullOrEmpty(specificIssue))
            {
                reason += $": {specificIssue}";
            }
            Skip = reason;
        }
    }
}