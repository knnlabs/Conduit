using ConduitLLM.Core.Constants;

namespace ConduitLLM.Tests.Core.Constants;

public sealed class SignalRConstantsTests
{
    [Theory]
    [InlineData("https://hooks.example.com/orders/created", "webhook-hooks-example-com--orders-created")]
    [InlineData("https://hooks.example.com:8443/orders?source=test", "webhook-hooks-example-com--orders")]
    [InlineData("https://localhost/", "webhook-localhost--")]
    public void Webhook_UsesTheSharedHostAndPathContract(string webhookUrl, string expected)
    {
        Assert.Equal(expected, SignalRConstants.Groups.Webhook(webhookUrl));
    }
}
