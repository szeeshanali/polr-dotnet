using Polr.Data;
using Polr.Services;

namespace Polr.Tests;

public class RulesTests
{
    [Theory]
    [InlineData(0, 32, "0")]
    [InlineData(31, 32, "v")]
    [InlineData(32, 32, "10")]
    [InlineData(5000, 32, "4s8")]
    [InlineData(61, 62, "Z")]
    [InlineData(62, 62, "10")]
    public void ToBase_matches_polr(long value, int numberBase, string expected) =>
        Assert.Equal(expected, Rules.ToBase(value, numberBase));

    [Fact]
    public void Crc32_matches_polr_hashes() =>
        Assert.Equal("2191738434", Rules.Crc32("The quick brown fox jumped over the lazy dog."));

    [Theory]
    [InlineData("abc", true)]
    [InlineData("my-link_2", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("a b", false)]
    [InlineData("a/b", false)]
    [InlineData("café", false)]
    public void IsValidEnding(string? ending, bool expected) => Assert.Equal(expected, Rules.IsValidEnding(ending));

    [Fact]
    public void Endings_longer_than_255_are_invalid() => Assert.False(Rules.IsValidEnding(new string('a', 256)));

    [Theory]
    [InlineData("admin")]
    [InlineData("Login")]
    [InlineData("about-polr")]
    public void App_paths_are_reserved(string ending) => Assert.True(Rules.IsReserved(ending));

    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("  http://example.com/a b  ", "http://example.com/a%20b")]
    public void TryNormalizeUrl_accepts_http_urls(string input, string expected)
    {
        Assert.True(Rules.TryNormalizeUrl(input, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormalizeUrl_rejects_everything_else(string? input) => Assert.False(Rules.TryNormalizeUrl(input, out _));

    [Theory]
    [InlineData("https://bit.ly/abc", true)]
    [InlineData("https://sho.rt/abc", true)]
    [InlineData("https://example.com/abc", false)]
    public void IsShortenedUrl(string url, bool expected) => Assert.Equal(expected, Rules.IsShortenedUrl(url, "sho.rt"));

    [Theory]
    [InlineData("someone@example.com", true)]
    [InlineData("Someone <someone@example.com>", false)]
    [InlineData("not-an-email", false)]
    public void IsValidEmail(string email, bool expected) => Assert.Equal(expected, Rules.IsValidEmail(email));

    [Fact]
    public void Bcrypt_hashes_from_polr_databases_verify()
    {
        // A $2y$ bcrypt test vector, the format stored in Polr databases.
        const string hash = "$2y$07$BCryptRequires22Chrcte/VlQH0piJtjXl.0t1XkA8pw9dMXTpOq";
        Assert.True(Crypto.VerifyPassword("rasmuslerdorf", hash));
        Assert.False(Crypto.VerifyPassword("wrong", hash));
        Assert.False(Crypto.VerifyPassword("rasmuslerdorf", "not a hash"));
    }

    [Fact]
    public void SecureEquals()
    {
        Assert.True(Crypto.SecureEquals("abcd", "abcd"));
        Assert.False(Crypto.SecureEquals("abcd", "abce"));
        Assert.False(Crypto.SecureEquals(null, "abcd"));
    }

    [Fact]
    public void Random_values_have_the_requested_size()
    {
        Assert.Equal(30, Crypto.RandomHex(15).Length);
        Assert.Matches("^[A-Za-z0-9]{5}$", Crypto.RandomString(5));
    }

    [Fact]
    public void CheckRange()
    {
        var now = DateTime.UtcNow;
        Assert.Null(ClickRepository.CheckRange(now.AddDays(-30), now, 365));
        Assert.Equal("Invalid bounds.", ClickRepository.CheckRange(now, now.AddDays(-1), 365));
        Assert.Equal("Bounds too broad.", ClickRepository.CheckRange(now.AddDays(-400), now, 365));
    }
}
