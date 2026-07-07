namespace WinAiUsageBar.App.ViewModels;

public sealed class SecretEditorViewModel
{
    public string SecretNameText { get; set; } = string.Empty;

    public string SecretValueText { get; set; } = string.Empty;

    public SecretEditorValidationResult ValidateSave()
    {
        var errors = new List<string>();
        var name = TrimToNull(SecretNameText);

        if (name is null)
        {
            errors.Add("Secret name is required.");
        }

        if (string.IsNullOrWhiteSpace(SecretValueText))
        {
            errors.Add("Secret value is required.");
        }

        return new SecretEditorValidationResult(name, SecretValueText, errors);
    }

    public SecretEditorValidationResult ValidateDelete()
    {
        var errors = new List<string>();
        var name = TrimToNull(SecretNameText);

        if (name is null)
        {
            errors.Add("Secret name is required.");
        }

        return new SecretEditorValidationResult(name, SecretValue: null, errors);
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record SecretEditorValidationResult(
    string? SecretName,
    string? SecretValue,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
