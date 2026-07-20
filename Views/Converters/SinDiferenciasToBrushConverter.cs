using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SistemaCambio.Views.Converters
{
    // Reemplaza el color literal que CierreCajaWindow.axaml.cs le ponía a txtTotalDiferencias.Foreground
    // segun cierre.TotalDiferencias == 0. Colores EXACTOS al original, no son brushes de tema.
    public class SinDiferenciasToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool sinDiferencias) return null;
            return sinDiferencias
                ? new SolidColorBrush(Color.Parse("#16A34A"))
                : new SolidColorBrush(Color.Parse("#DC2626"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
