namespace SstvVPrinter.App.Validation;

public sealed record CallSignValidationResult(bool IsValid, string Normalized, string? Error);

public static class CallSignValidator
{
    private const string SupportedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-: .";

    public static CallSignValidationResult Validate(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return new(false, string.Empty, "コールサインを入力してください。");
        }

        if (normalized.Any(character => !SupportedCharacters.Contains(character)))
        {
            return new(false, normalized, "コールサインに使用できない文字があります。");
        }

        return new(true, normalized, null);
    }
}
