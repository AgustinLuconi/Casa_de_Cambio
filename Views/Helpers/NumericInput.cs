using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SistemaCambio.Views.Helpers;

public static class NumericInput
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Enabled", typeof(NumericInput));

    public static bool GetEnabled(TextBox tb) => tb.GetValue(EnabledProperty);
    public static void SetEnabled(TextBox tb, bool value) => tb.SetValue(EnabledProperty, value);

    static NumericInput()
    {
        EnabledProperty.Changed.AddClassHandler<TextBox>((tb, e) =>
        {
            if ((bool?)e.NewValue == true)
                tb.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
            else
                tb.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        });
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        foreach (var c in e.Text)
        {
            if (!char.IsDigit(c) && c != '.' && c != ',')
            {
                e.Handled = true;
                return;
            }
        }
    }
}
