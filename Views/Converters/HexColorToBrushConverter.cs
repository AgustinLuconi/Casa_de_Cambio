using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SistemaCambio.Views.Converters
{
    public class HexColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string hex && !string.IsNullOrEmpty(hex) ? new SolidColorBrush(Color.Parse(hex)) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
