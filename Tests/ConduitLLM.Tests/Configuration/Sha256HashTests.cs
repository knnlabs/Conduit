using System.Text;

using ConduitLLM.Configuration.Utilities;

namespace ConduitLLM.Tests.Configuration;

public sealed class Sha256HashTests
{
    private const string LowerHex =
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string UpperHex =
        "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";

    [Fact]
    public void HexEncodings_PreserveExistingCasing()
    {
        Assert.Equal(LowerHex, Sha256Hash.LowerHex("abc"));
        Assert.Equal(UpperHex, Sha256Hash.UpperHex("abc"));
        Assert.Equal(LowerHex, VirtualKeyUtilities.HashKey("abc"));
    }

    [Fact]
    public void Base64Encodings_PreserveBothExistingAlphabets()
    {
        var value = Encoding.UTF8.GetBytes("abc");

        Assert.Equal("ungWv48Bz-pBQUDeXa4iI7ADYaOWF3qctBD_YfIAFa0", Sha256Hash.Base64Url(value));
        Assert.Equal("ungWv48Bz_pBQUDeXa4iI7ADYaOWF3qctBD-YfIAFa0", Sha256Hash.LegacyStorageBase64Url(value));
    }

    [Fact]
    public async Task StreamEncoding_MatchesLegacyByteEncoding()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal(
            Sha256Hash.LegacyStorageBase64Url("abc"),
            await Sha256Hash.LegacyStorageBase64UrlAsync(stream));
    }
}
