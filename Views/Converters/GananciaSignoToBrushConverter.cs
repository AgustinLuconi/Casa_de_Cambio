using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SistemaCambio.Views.Converters
{
    public class GananciaSignoToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not decimal ganancia) return null;
            string key = ganancia > 0 ? "SuccessBrush" : ganancia < 0 ? "DangerBrush" : "PrimaryTextBrush";
            return Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var brush) == true ? brush : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
