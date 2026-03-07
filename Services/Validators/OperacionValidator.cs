using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Services.Validators
{
    /// <summary>
    /// Valida operaciones de compra/venta/crédito-débito ANTES de guardarlas.
    /// Usa SaldoCuenta para verificar saldos por moneda.
    /// </summary>
    public class OperacionValidator
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public OperacionValidator(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public ValidationResult ValidarOperacion(
            string tipoOperacion,
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion)
        {
            var result = new ValidationResult();

            using var db = _contextFactory.CreateDbContext();

            // 1. EXISTENCIA DE CUENTAS
            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
            var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);

            if (cuentaOrigen == null)
            {
                result.AddError("Cuenta origen no existe", $"ID: {cuentaOrigenId}");
                return result;
            }

            if (cuentaDestino == null)
            {
                result.AddError("Cuenta destino no existe", $"ID: {cuentaDestinoId}");
                return result;
            }

            // 2. MONTOS POSITIVOS
            if (montoOrigen <= 0)
                result.AddError("Monto origen debe ser mayor a cero",
                    $"Valor ingresado: {montoOrigen}");

            if (montoDestino <= 0)
                result.AddError("Monto destino debe ser mayor a cero",
                    $"Valor ingresado: {montoDestino}");

            // 3. COTIZACIÓN RAZONABLE
            if (cotizacion <= 0)
            {
                result.AddError("Cotización debe ser mayor a cero",
                    $"Valor ingresado: {cotizacion}");
            }
            else
            {
                if (cotizacion < 1)
                    result.AddWarning("Cotización muy baja",
                        $"Valor: {cotizacion:N2}. ¿Está seguro? Las cotizaciones normales están entre 100-2000.");

                if (cotizacion > 10000)
                    result.AddWarning("Cotización muy alta",
                        $"Valor: {cotizacion:N2}. Verifique que no sea un error de tipeo.");
            }

            // 4. MISMA CUENTA + MISMA MONEDA = Error
            if (cuentaOrigenId == cuentaDestinoId && monedaOrigen == monedaDestino)
                result.AddError("No se puede operar la misma moneda en la misma cuenta",
                    $"Cuenta: {cuentaOrigen.Nombre}, Moneda: {monedaOrigen}");

            // 5. COHERENCIA DE MONEDAS (compra/venta)
            ValidarCoherenciaMonedas(tipoOperacion, monedaOrigen, monedaDestino, result);

            // 6. SALDO SUFICIENTE (de SaldoCuenta)
            var saldoOrigen = db.SaldosCuenta
                .FirstOrDefault(s => s.CuentaId == cuentaOrigenId && s.Moneda == monedaOrigen);

            decimal saldoDisponible = saldoOrigen?.Saldo ?? 0;

            if (saldoDisponible < montoOrigen)
            {
                result.AddError(
                    $"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen})",
                    $"Disponible: ${saldoDisponible:N2} | Requerido: ${montoOrigen:N2}");
            }
            else if (saldoDisponible - montoOrigen < 100 && monedaOrigen != "ARS")
            {
                result.AddWarning("Saldo quedará muy bajo",
                    $"Después de la operación quedará: ${saldoDisponible - montoOrigen:N2} {monedaOrigen}");
            }

            // 7. COHERENCIA MATEMÁTICA
            ValidarCoherenciaMatematica(montoOrigen, montoDestino, cotizacion,
                monedaOrigen, monedaDestino, result);

            return result;
        }

        public ValidationResult ValidarCreditoDebito(
            int cuentaCreditoId,
            int cuentaDebitoId,
            string monedaCredito,
            string monedaDebito,
            decimal montoCredito,
            decimal montoDebito)
        {
            var result = new ValidationResult();

            using var db = _contextFactory.CreateDbContext();

            var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
            var cuentaDebito = db.Cuentas.Find(cuentaDebitoId);

            if (cuentaCredito == null)
            {
                result.AddError("Cuenta crédito no existe", $"ID: {cuentaCreditoId}");
                return result;
            }
            if (cuentaDebito == null)
            {
                result.AddError("Cuenta débito no existe", $"ID: {cuentaDebitoId}");
                return result;
            }

            if (montoCredito <= 0)
                result.AddError("Monto crédito debe ser mayor a cero");
            if (montoDebito <= 0)
                result.AddError("Monto débito debe ser mayor a cero");

            // Misma cuenta + misma moneda → error
            if (cuentaCreditoId == cuentaDebitoId && monedaCredito == monedaDebito)
                result.AddError("No se puede hacer crédito/débito en la misma cuenta y moneda",
                    $"Cuenta: {cuentaCredito.Nombre}, Moneda: {monedaCredito}");

            // Saldo suficiente en la cuenta a debitar
            var saldoDebito = db.SaldosCuenta
                .FirstOrDefault(s => s.CuentaId == cuentaDebitoId && s.Moneda == monedaDebito);

            decimal saldoDisponible = saldoDebito?.Saldo ?? 0;

            if (saldoDisponible < montoDebito)
            {
                result.AddError(
                    $"Saldo insuficiente en '{cuentaDebito.Nombre}' ({monedaDebito})",
                    $"Disponible: ${saldoDisponible:N2} | Requerido: ${montoDebito:N2}");
            }

            // Advertencia si monedas son diferentes
            if (monedaCredito != monedaDebito)
            {
                result.AddWarning(
                    "Monedas diferentes en crédito/débito",
                    $"Crédito: {monedaCredito} | Débito: {monedaDebito}. " +
                    "Verifique que los montos y cotización sean correctos.");
            }

            return result;
        }

        public ValidationResult ValidarOperacionInterbancaria(
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino)
        {
            var result = new ValidationResult();
            using var db = _contextFactory.CreateDbContext();

            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
            var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);

            if (cuentaOrigen == null) result.AddError("Cuenta origen no existe", $"ID: {cuentaOrigenId}");
            if (cuentaDestino == null) result.AddError("Cuenta destino no existe", $"ID: {cuentaDestinoId}");
            if (result.HasErrors) return result;

            if (montoOrigen <= 0) result.AddError("Monto origen debe ser mayor a cero");
            if (montoDestino <= 0) result.AddError("Monto destino debe ser mayor a cero");

            if (cuentaOrigenId == cuentaDestinoId)
                result.AddError("La cuenta origen y destino no pueden ser la misma en un Arbitraje", $"Cuenta: {cuentaOrigen.Nombre}");

            if (monedaOrigen == monedaDestino)
                result.AddError("Arbitraje requiere cambio de divisas. Seleccione monedas diferentes.", $"Moneda: {monedaOrigen}");

            // Verificar saldo en Origen de la moneda que sale
            var saldoOrigen = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaOrigenId && s.Moneda == monedaOrigen);
            decimal dispOrigen = saldoOrigen?.Saldo ?? 0;
            if (dispOrigen < montoOrigen)
                result.AddError($"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen})", 
                                $"Disponible: ${dispOrigen:N2} | Requerido: ${montoOrigen:N2}");

            // Verificar saldo en Destino de la moneda que sale
            var saldoDestino = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaDestinoId && s.Moneda == monedaDestino);
            decimal dispDestino = saldoDestino?.Saldo ?? 0;
            if (dispDestino < montoDestino)
                result.AddError($"Saldo insuficiente en '{cuentaDestino.Nombre}' ({monedaDestino})", 
                                $"Disponible: ${dispDestino:N2} | Requerido: ${montoDestino:N2}");

            return result;
        }

        public ValidationResult ValidarCotizacionContraOficial(
            string codigoMoneda, decimal cotizacionIngresada)
        {
            var result = new ValidationResult();

            using var db = _contextFactory.CreateDbContext();

            var cotizacionOficial = db.CotizacionesDiarias
                .Where(c => c.Moneda.Codigo == codigoMoneda &&
                            c.Fecha.Date == DateTime.UtcNow.Date)
                .OrderByDescending(c => c.Fecha)
                .FirstOrDefault();

            if (cotizacionOficial == null)
            {
                result.AddInfo(
                    "No hay cotización oficial cargada para hoy",
                    $"Se recomienda cargar la cotización oficial de {codigoMoneda} antes de operar.");
                return result;
            }

            var cotizacionRef = cotizacionOficial.CotizacionVenta;
            var desviacion = cotizacionRef > 0
                ? Math.Abs(cotizacionIngresada - cotizacionRef) / cotizacionRef
                : 0;

            if (desviacion > 0.10m)
            {
                result.AddWarning("Cotización se desvía mucho de la oficial",
                    $"Oficial: ${cotizacionRef:N2} | Ingresada: ${cotizacionIngresada:N2} | " +
                    $"Diferencia: {desviacion:P1}");
            }
            else if (desviacion > 0.05m)
            {
                result.AddInfo("Cotización ligeramente diferente a la oficial",
                    $"Oficial: ${cotizacionRef:N2} | Ingresada: ${cotizacionIngresada:N2}");
            }

            return result;
        }

        // ─── Métodos privados ─────────────────────────────────────

        private void ValidarCoherenciaMonedas(
            string tipoOperacion, string monedaOrigen, string monedaDestino, ValidationResult result)
        {
            if (tipoOperacion == "Compra")
            {
                if (monedaOrigen != "ARS")
                    result.AddWarning("Posible error de moneda",
                        $"En una compra normalmente se paga con ARS. " +
                        $"Está usando moneda origen: {monedaOrigen}");

                if (monedaDestino == "ARS")
                    result.AddWarning("Posible error de moneda",
                        $"En una compra normalmente se recibe divisa. " +
                        $"Está usando moneda destino: ARS");
            }
            else if (tipoOperacion == "Venta")
            {
                if (monedaOrigen == "ARS")
                    result.AddWarning("Posible error de moneda",
                        $"En una venta normalmente se vende divisa. " +
                        $"Está usando moneda origen: ARS");

                if (monedaDestino != "ARS")
                    result.AddWarning("Posible error de moneda",
                        $"En una venta normalmente se recibe ARS. " +
                        $"Está usando moneda destino: {monedaDestino}");
            }
        }

        private void ValidarCoherenciaMatematica(
            decimal montoOrigen, decimal montoDestino, decimal cotizacion,
            string monedaOrigen, string monedaDestino, ValidationResult result)
        {
            // Solo aplica si es operación ARS <-> Divisa
            if (!((monedaOrigen == "ARS" && monedaDestino != "ARS") ||
                  (monedaOrigen != "ARS" && monedaDestino == "ARS")))
                return;

            decimal montoEsperado;
            if (monedaOrigen == "ARS")
                montoEsperado = cotizacion != 0 ? montoOrigen / cotizacion : 0;
            else
                montoEsperado = montoOrigen * cotizacion;

            if (montoEsperado <= 0) return;

            var diferencia = Math.Abs(montoDestino - montoEsperado);
            var tolerancia = montoEsperado * 0.05m; // 5% de tolerancia

            if (diferencia > tolerancia)
            {
                result.AddWarning("⚠️ POSIBLE ERROR DE CÁLCULO",
                    $"Según la cotización {cotizacion:N2}, el monto esperado es ${montoEsperado:N2}. " +
                    $"Usted ingresó ${montoDestino:N2}. Diferencia: ${diferencia:N2}. " +
                    "Verifique los números antes de continuar.");
            }
        }
    }
}
