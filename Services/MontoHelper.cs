using System.Globalization;
using System.Linq;

namespace SistemaCambio.Services
{
    public static class MontoHelper
    {
        /// <summary>
        /// Parsea un monto en formato argentino (10.000.000,00) o americano (10,000,000.00)
        /// </summary>
        public static decimal Parsear(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0;

            // Limpiar espacios
            string limpio = texto.Trim();

            // Detectar formato: si tiene punto como separador de miles y coma como decimal
            // Formato argentino: 1.234.567,89
            // Formato americano: 1,234,567.89

            // Contar puntos y comas
            int puntos = limpio.Count(c => c == '.');
            int comas = limpio.Count(c => c == ',');

            // Si hay múltiples puntos, son separadores de miles (formato argentino)
            if (puntos > 1 || (puntos == 1 && comas == 1 && limpio.IndexOf('.') < limpio.IndexOf(',')))
            {
                // Formato argentino: 1.234.567,89 → quitar puntos, cambiar coma por punto
                limpio = limpio.Replace(".", "").Replace(",", ".");
            }
            // Si hay múltiples comas, son separadores de miles (formato americano)
            else if (comas > 1 || (comas == 1 && puntos == 1 && limpio.IndexOf(',') < limpio.IndexOf('.')))
            {
                // Formato americano: 1,234,567.89 → quitar comas
                limpio = limpio.Replace(",", "");
            }
            // Si solo hay una coma y ningún punto, la coma es decimal
            else if (comas == 1 && puntos == 0)
            {
                limpio = limpio.Replace(",", ".");
            }
            // Si solo hay un punto, asumimos que es decimal (ya está bien)

            if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultado))
            {
                return resultado;
            }

            return 0;
        }

        /// <summary>
        /// Formatea un decimal para mostrar en formato argentino
        /// </summary>
        public static string Formatear(decimal valor, int decimales = 2)
        {
            return valor.ToString($"N{decimales}", new CultureInfo("es-AR"));
        }
    }
}
