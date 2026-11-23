using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TechShop.Models;
using TechShop.Services;
using Xunit;

namespace TechShop.UnitTests;

public class VnPayServiceTests
{
    // Deliberately synthetic, public test-only signer; NEVER merchant credentials.
    private const string TestOnlySecret = "test-key";
    private static VnPayService Service(bool enabled = true) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VnPay:Enabled"] = enabled.ToString(),
            ["VnPay:TmnCode"] = "TESTONLY",
            ["VnPay:HashSecret"] = TestOnlySecret
        }).Build());

    private static Dictionary<string, string> SignedQuery()
    {
        var query = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "TESTONLY",
            ["vnp_Amount"] = "125000000",
            ["vnp_TxnRef"] = "42",
            ["vnp_ResponseCode"] = "00",
            ["vnp_OrderInfo"] = "unit test only"
        };
        // Independent input and BCL HMAC oracle: do not use production Sign/Canonical
        // to generate the signature that production Verify is expected to accept.
        const string canonical = "vnp_Amount=125000000&vnp_OrderInfo=unit+test+only&vnp_ResponseCode=00&vnp_TmnCode=TESTONLY&vnp_TxnRef=42";
        query["vnp_SecureHash"] = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestOnlySecret), Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        return query;
    }

    [Fact]
    public void Canonical_excludes_both_hash_fields_and_empty_values()
    {
        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "42", ["vnp_SecureHash"] = "ignored",
            ["vnp_SecureHashType"] = "SHA512", ["vnp_Empty"] = ""
        };
        Assert.Equal("vnp_TxnRef=42", VnPayService.Canonical(query));
    }

    [Fact]
    public void Canonical_sorts_ordinally_and_url_encodes_keys_and_values()
    {
        var query = new Dictionary<string, string>
        {
            ["vnp_z"] = "a+b &c=/", ["vnp_A"] = "A B", ["vnp_key x"] = "x+y"
        };
        Assert.Equal("vnp_A=A+B&vnp_key+x=x%2By&vnp_z=a%2Bb+%26c%3D%2F", VnPayService.Canonical(query));
    }

    [Fact]
    public void Sign_matches_independent_HmacSha512_and_lowercase_hex()
    {
        const string payload = "vnp_Amount=100&vnp_TxnRef=42";
        var expected = Convert.ToHexString(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(TestOnlySecret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        Assert.Equal(expected, VnPayService.Sign(payload, TestOnlySecret));
    }

    [Fact]
    public void Verify_accepts_independently_signed_callback_when_enabled()
    {
        var service = Service();
        Assert.True(service.Enabled);
        Assert.True(service.Verify(SignedQuery()));
    }

    [Fact]
    public void Verify_rejects_tampered_amount()
    {
        var query = SignedQuery();
        query["vnp_Amount"] = "1";
        Assert.False(Service().Verify(query));
    }

    [Fact]
    public void Verify_rejects_non_hex_signature_even_at_correct_length()
    {
        var query = SignedQuery();
        query["vnp_SecureHash"] = new string('z', 128);
        Assert.False(Service().Verify(query));
    }

    [Fact]
    public void Verify_rejects_short_signature()
    {
        var query = SignedQuery();
        query["vnp_SecureHash"] = "abc";
        Assert.False(Service().Verify(query));
    }

    [Fact]
    public void Verify_rejects_missing_signature()
    {
        var query = SignedQuery();
        query.Remove("vnp_SecureHash");
        Assert.False(Service().Verify(query));
    }

    [Fact]
    public void Verify_rejects_valid_signature_when_disabled()
    {
        var service = Service(enabled: false);
        Assert.False(service.Enabled);
        Assert.False(service.Verify(SignedQuery()));
    }

    [Fact]
    public void Verify_ignores_unsigned_non_vnp_fields()
    {
        var query = SignedQuery();
        query["tracking"] = "outside signed gateway namespace";
        Assert.True(Service().Verify(query));
    }

    [Fact]
    public void Create_refuses_disabled_configuration()
        => Assert.Throws<InvalidOperationException>(() => Service(false).Create(new Order { Total = 100m }, "127.0.0.1"));
}
