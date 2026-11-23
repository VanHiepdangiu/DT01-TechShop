using TechShop.Services;
using Xunit;

namespace TechShop.UnitTests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Aa1!aaa", false)]
    [InlineData("abcdefgh", false)]
    [InlineData("ABCDEFG1!", false)]
    [InlineData("abcdefg1!", false)]
    [InlineData("Abcdefgh!", false)]
    [InlineData("Abcdefg1", false)]
    [InlineData("Aa1 aaaa", false)]
    [InlineData("Aa1\taaaa", false)]
    [InlineData("Aa1!aaaa", true)]
    [InlineData("Tech@123456", true)]
    public void Valid_requires_all_character_groups_and_minimum_length(string? password, bool expected)
        => Assert.Equal(expected, PasswordPolicy.Valid(password));

    [Fact]
    public void Valid_accepts_exactly_128_characters()
        => Assert.True(PasswordPolicy.Valid("Aa1!" + new string('a', 124)));

    [Fact]
    public void Valid_rejects_129_characters_even_with_all_character_groups()
        => Assert.False(PasswordPolicy.Valid("Aa1!" + new string('a', 125)));
}
