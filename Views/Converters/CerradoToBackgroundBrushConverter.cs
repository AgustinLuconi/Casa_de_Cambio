using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SistemaCambio.Views.Converters
{
    // Reemplaza borderEstado.Background: cuando Cerrado==true usa el literal #16A34A20
    // (idéntico al original); cuando Cerrado==false vuelve al default original del XAML,
    // {DynamicResource CardBackgroundBrush}.
    public class CerradoToBackgroundBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool cerrado) return null;
            if (cerrado) return new SolidColorBrush(Color.Parse("#16A34A20"));
            return Application.Current?.TryGetResource("CardBackgroundBrush", Application.Current.ActualThemeVariant, out var brush) == true
                ? brush
                : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
