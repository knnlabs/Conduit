using System.Net.Http.Headers;

using ConduitLLM.Providers.Authentication;

using AwesomeAssertions;

using Xunit;

namespace ConduitLLM.Tests.Providers.Bedrock;

/// <summary>
/// Tests for the AWS Signature Version 4 implementation used by the Bedrock adapter.
/// The primary case reproduces the worked example from the AWS signing documentation
/// (GET iam.amazonaws.com ListUsers with the AKIDEXAMPLE credentials), whose signature is a
/// published known-good value.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class AwsSigV4SignerTests
{
    private static readonly AwsSigV4Credentials DocCredentials = new(
        "AKIDEXAMPLE",
        "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY");

    private static readonly DateTimeOffset DocSigningTime =
        new(2015, 8, 30, 12, 36, 0, TimeSpan.Zero);

    [Fact]
    public void Sign_Reproduces_The_AWS_Documentation_Example_Signature()
    {
        // GET https://iam.amazonaws.com/?Action=ListUsers&Version=2010-05-08 with an empty body and
        // a signed content-type header, per the SigV4 documentation walkthrough.
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://iam.amazonaws.com/?Action=ListUsers&Version=2010-05-08");
        request.Content = new ByteArrayContent(Array.Empty<byte>());
        request.Content.Headers.ContentType =
            MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded; charset=utf-8");

        AwsSigV4Signer.Sign(request, Array.Empty<byte>(), DocCredentials, "us-east-1", "iam", DocSigningTime);

        var authorization = request.Headers.GetValues("Authorization").Single();
        authorization.Should().Be(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/iam/aws4_request, "
            + "SignedHeaders=content-type;host;x-amz-date, "
            + "Signature=5d672d79c15b13162d9279b0855cfba6789a8edb4c82c400e06b5924a6f2b5d7");
        request.Headers.GetValues("x-amz-date").Single().Should().Be("20150830T123600Z");
        request.Headers.Host.Should().Be("iam.amazonaws.com");
    }

    [Fact]
    public void Sign_With_Session_Token_Adds_And_Signs_The_Security_Token_Header()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var credentials = DocCredentials with { SessionToken = "the-session-token" };

        AwsSigV4Signer.Sign(request, Array.Empty<byte>(), credentials, "us-east-1", "bedrock-runtime", DocSigningTime);

        request.Headers.GetValues("x-amz-security-token").Single().Should().Be("the-session-token");
        request.Headers.GetValues("Authorization").Single()
            .Should().Contain("SignedHeaders=host;x-amz-date;x-amz-security-token");
    }

    [Fact]
    public void Sign_Is_Deterministic_For_Identical_Inputs()
    {
        HttpRequestMessage Build()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://bedrock-runtime.eu-west-1.amazonaws.com/model/m/converse");
            AwsSigV4Signer.Sign(
                request,
                "{\"messages\":[]}"u8.ToArray(),
                DocCredentials,
                "eu-west-1",
                "bedrock-runtime",
                DocSigningTime);
            return request;
        }

        Build().Headers.GetValues("Authorization").Single()
            .Should().Be(Build().Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public void BuildCanonicalPath_Double_Encodes_Percent_Encoded_Segments()
    {
        // Bedrock model IDs contain ':' which is %3A on the wire; SigV4 for non-S3 services
        // canonicalizes the once-encoded path by encoding it again (%3A -> %253A).
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-v1%3A0/converse");

        AwsSigV4Signer.BuildCanonicalPath(uri)
            .Should().Be("/model/anthropic.claude-v1%253A0/converse");
    }

    [Fact]
    public void BuildCanonicalPath_Keeps_Unreserved_Characters_And_Slashes()
    {
        var uri = new Uri("https://bedrock.us-east-1.amazonaws.com/foundation-models");

        AwsSigV4Signer.BuildCanonicalPath(uri).Should().Be("/foundation-models");
    }

    [Fact]
    public void BuildCanonicalQuery_Sorts_Parameters_And_Percent_Encodes_Strictly()
    {
        var uri = new Uri("https://example.amazonaws.com/?zeta=z%20value&Action=ListUsers");

        AwsSigV4Signer.BuildCanonicalQuery(uri)
            .Should().Be("Action=ListUsers&zeta=z%20value");
    }
}
