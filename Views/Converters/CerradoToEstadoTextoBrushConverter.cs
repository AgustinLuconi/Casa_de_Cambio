using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SistemaCambio.Views.Converters
{
    // Reemplaza txtEstado.Foreground: cuando Cerrado==true usa el literal #16A34A
    // (idéntico al original); cuando Cerrado==false vuelve al default original del XAML,
    // {DynamicResource WarningBrush}.
    public class CerradoToEstadoTextoBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool cerrado) return null;
            if (cerrado) return new SolidColorBrush(Color.Parse("#16A34A"));
            return Application.Current?.TryGetResource("WarningBrush", Application.Current.ActualThemeVariant, out var brush) == true
                ? brush
                : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
