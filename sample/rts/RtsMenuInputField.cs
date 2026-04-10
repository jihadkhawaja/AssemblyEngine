namespace RtsSample;

internal sealed class RtsMenuInputField
{
    private readonly Func<char, bool> _validator;

    public RtsMenuInputField(string elementId, int maxLength, Func<char, bool> validator, string initialValue = "")
    {
        ElementId = elementId;
        MaxLength = maxLength;
        _validator = validator;
        SetValue(initialValue);
    }

    public string ElementId { get; }

    public int MaxLength { get; }

    public string Value { get; private set; } = string.Empty;

    public void SetValue(string? value)
    {
        Value = string.Concat((value ?? string.Empty).Where(_validator)).Trim();
        if (Value.Length > MaxLength)
            Value = Value[..MaxLength];
    }

    public bool Append(char value)
    {
        if (Value.Length >= MaxLength || !_validator(value))
            return false;

        Value += value;
        return true;
    }

    public bool Backspace()
    {
        if (Value.Length == 0)
            return false;

        Value = Value[..^1];
        return true;
    }
}