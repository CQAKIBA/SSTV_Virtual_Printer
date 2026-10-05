using SstvVPrinter.App.Validation;

namespace SstvVPrinter.App.Tests.Validation;

public sealed class CallSignValidatorTests
{
    [Fact]
    public void Valid_call_sign_is_normalized_to_uppercase()
    {
        var result = CallSignValidator.Validate(" ja1umw/p ");

        Assert.True(result.IsValid);
        Assert.Equal("JA1UMW/P", result.Normalized);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_call_sign_is_rejected(string? value)
    {
        var result = CallSignValidator.Validate(value);

        Assert.False(result.IsValid);
        Assert.Equal(string.Empty, result.Normalized);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("JA1UMW_1")]
    [InlineData("JA1UMW@HOME")]
    [InlineData("ＪＡ１ＵＭＷ")]
    public void Unsupported_characters_are_rejected(string value)
    {
        var result = CallSignValidator.Validate(value);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }
}
