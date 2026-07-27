using System.Text.Json;

using ConduitLLM.Configuration.Events;
using ConduitLLM.Core.Events;

using FluentAssertions;

namespace ConduitLLM.Tests.Configuration.Events;

public class ProviderKeyCredentialEventsTests
{
    public static TheoryData<DomainEvent> CredentialEvents => new()
    {
        new ProviderKeyCredentialCreated(),
        new ProviderKeyCredentialUpdated(),
        new ProviderKeyCredentialDeleted(),
        new ProviderKeyCredentialPrimaryChanged()
    };

    [Theory]
    [MemberData(nameof(CredentialEvents))]
    public void CredentialEvent_UsesCanonicalDomainEventContract(DomainEvent domainEvent)
    {
        domainEvent.Should().BeAssignableTo<IDomainEvent>();
        domainEvent.EventId.Should().NotBeNullOrWhiteSpace();
        domainEvent.CorrelationId.Should().BeEmpty();
    }

    [Fact]
    public void CredentialEvent_DeserializesLegacyGuidCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var json = $$"""
            {
              "keyId": 42,
              "providerId": 7,
              "correlationId": "{{correlationId}}"
            }
            """;

        var result = JsonSerializer.Deserialize<ProviderKeyCredentialCreated>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        result.Should().NotBeNull();
        result!.CorrelationId.Should().Be(correlationId.ToString());
        result.EventId.Should().NotBeNullOrWhiteSpace();
    }
}
