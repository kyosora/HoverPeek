using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Settings;

namespace HoverPeek.App;

public sealed class NumericField : System.Windows.Controls.TextBox
{
    public static readonly DependencyProperty IsValueValidProperty = DependencyProperty.Register(
        nameof(IsValueValid), typeof(bool), typeof(NumericField), new PropertyMetadata(false));

    public static readonly DependencyProperty ValidationMessageProperty = DependencyProperty.Register(
        nameof(ValidationMessage), typeof(string), typeof(NumericField), new PropertyMetadata(string.Empty));

    private long _minimum;
    private long _maximum;

    public NumericField()
    {
        TextChanged += (_, _) => ValidateValue();
    }

    public bool IsValueValid
    {
        get => (bool)GetValue(IsValueValidProperty);
        private set => SetValue(IsValueValidProperty, value);
    }

    public string ValidationMessage
    {
        get => (string)GetValue(ValidationMessageProperty);
        private set => SetValue(ValidationMessageProperty, value);
    }

    public event EventHandler? ValidityChanged;

    public void SetRange(SettingsLimits.Range range)
    {
        _minimum = checked((long)range.Min);
        _maximum = checked((long)range.Max);
        ValidateValue();
    }

    public int GetInt32() => checked((int)GetInt64());

    public long GetInt64() => long.Parse(Text, NumberStyles.Integer, CultureInfo.CurrentCulture);

    private void ValidateValue()
    {
        var wasValid = IsValueValid;
        var parsed = long.TryParse(Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value);
        IsValueValid = parsed && value >= _minimum && value <= _maximum;
        ValidationMessage = IsValueValid
            ? string.Empty
            : Strings.Format("IntegerRangeValidation", _minimum, _maximum);

        if (wasValid != IsValueValid)
            ValidityChanged?.Invoke(this, EventArgs.Empty);
    }
}
