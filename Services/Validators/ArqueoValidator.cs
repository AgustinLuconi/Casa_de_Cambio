using SistemaCambio.Models;
using System;

namespace SistemaCambio.Services.Validators
{
    /// <summary>
    /// Valida diferencias detectadas en arqueos de caja.
    /// Trabaja con SaldoCuenta (saldo por moneda específica).
    /// </summary>
    public class ArqueoValidator
    {
        public ValidationResult ValidarArqueo(string cuentaNombre, string moneda, decimal saldoSistema, decimal montoContado)
        {
            var result = new ValidationResult();

            if (montoContado < 0)
            {
                result.AddError("El monto contado no puede ser negativo",
                    $"Valor ingresado: {montoContado}");
                return result;
            }

            var diferencia = montoContado - saldoSistema;
            var diferenciaAbs = Math.Abs(diferencia);

            if (diferenciaAbs == 0)
            {
                result.AddInfo("✓ Caja cuadra perfectamente",
                    $"{cuentaNombre} ({moneda}): sin diferencias");
                return result;
            }

            string tipoStr = diferencia > 0 ? "Sobrante" : "Faltante";
            decimal porcentajeDif = saldoSistema != 0
                ? (diferenciaAbs / Math.Abs(saldoSistema)) * 100
                : 100;

            if (diferenciaAbs <= 50)
            {
                result.AddInfo("Diferencia menor detectada",
                    $"{tipoStr}: ${diferenciaAbs:N2} en {cuentaNombre} ({moneda})");
            }
            else if (diferenciaAbs <= 500)
            {
                result.AddWarning("Diferencia detectada",
                    $"{tipoStr}: ${diferenciaAbs:N2} ({porcentajeDif:N1}%) en {cuentaNombre} ({moneda}). " +
                    "Verifique el conteo.");
            }
            else if (diferenciaAbs <= 2000)
            {
                result.AddWarning("⚠️ DIFERENCIA SIGNIFICATIVA",
                    $"{tipoStr}: ${diferenciaAbs:N2} ({porcentajeDif:N1}%) en {cuentaNombre} ({moneda}). " +
                    "Se recomienda volver a contar.");
            }
            else
            {
                result.AddError("🚨 DIFERENCIA MUY GRANDE",
                    $"{tipoStr}: ${diferenciaAbs:N2} ({porcentajeDif:N1}%) en {cuentaNombre} ({moneda}). " +
                    "Se requiere revisión antes de ajustar.");
            }

            return result;
        }
    }
}
