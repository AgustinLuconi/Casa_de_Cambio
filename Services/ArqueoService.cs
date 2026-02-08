using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Services
{
    public class ArqueoResult
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; } = "";
        public int? ArqueoId { get; set; }
        public decimal Diferencia { get; set; }

        public static ArqueoResult Success(int id, decimal diferencia) => 
            new() { Exitoso = true, ArqueoId = id, Diferencia = diferencia };
        public static ArqueoResult Error(string msg) => 
            new() { Exitoso = false, Mensaje = msg };
    }

    public static class ArqueoService
    {
        /// <summary>
        /// Realiza un arqueo ciego y genera asiento de ajuste automático si hay diferencia.
        /// </summary>
        public static ArqueoResult RealizarArqueoCiego(int cuentaId, decimal montoContado, string observaciones = "")
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var cuenta = db.Cuentas.Find(cuentaId);
                if (cuenta == null)
                    return ArqueoResult.Error("Cuenta no encontrada");

                decimal saldoSistema = cuenta.Saldo;
                decimal diferencia = montoContado - saldoSistema;

                // Crear registro de arqueo
                var arqueo = new Arqueo
                {
                    CuentaId = cuentaId,
                    Fecha = DateTime.Now,
                    SaldoSistema = saldoSistema,
                    SaldoArqueo = montoContado,
                    Diferencia = diferencia,
                    Observaciones = string.IsNullOrEmpty(observaciones) 
                        ? (diferencia == 0 ? "Cuadra" : (diferencia > 0 ? "Sobrante" : "Faltante"))
                        : observaciones
                };
                db.Arqueos.Add(arqueo);
                db.SaveChanges(); // Para obtener el ID del arqueo

                // Si hay diferencia, generar asiento de ajuste
                if (diferencia != 0)
                {
                    // Buscar o crear cuenta de Diferencias de Caja
                    var cuentaAjuste = db.Cuentas.FirstOrDefault(c =>
                        c.Nombre == "Diferencias de Caja" && c.Moneda == cuenta.Moneda);

                    if (cuentaAjuste == null)
                    {
                        cuentaAjuste = new Cuenta
                        {
                            Nombre = "Diferencias de Caja",
                            Tipo = "Resultado",
                            Moneda = cuenta.Moneda,
                            Saldo = 0
                        };
                        db.Cuentas.Add(cuentaAjuste);
                        db.SaveChanges();
                    }

                    // Crear operación de ajuste
                    var tipoAjuste = diferencia > 0 ? "Sobrante Caja" : "Faltante Caja";
                    var opAjuste = new Operacion
                    {
                        Fecha = DateTime.Now,
                        TipoOperacion = tipoAjuste,
                        MontoTotalOrigen = Math.Abs(diferencia),
                        MontoTotalDestino = Math.Abs(diferencia),
                        CotizacionAplicada = 1,
                        Observaciones = $"Ajuste automático por arqueo #{arqueo.Id}"
                    };
                    db.Operaciones.Add(opAjuste);

                    // Movimiento de ajuste en la caja
                    var movCaja = new Movimiento
                    {
                        Operacion = opAjuste,
                        CuentaId = cuentaId,
                        Monto = diferencia, // Positivo = sobrante, Negativo = faltante
                        Fecha = DateTime.Now
                    };
                    db.Movimientos.Add(movCaja);

                    // Movimiento contrario en cuenta de diferencias
                    var movDiferencias = new Movimiento
                    {
                        Operacion = opAjuste,
                        CuentaId = cuentaAjuste.Id,
                        Monto = -diferencia, // Contrario al movimiento de caja
                        Fecha = DateTime.Now
                    };
                    db.Movimientos.Add(movDiferencias);

                    // Actualizar saldos
                    cuenta.Saldo = montoContado; // Ajustar al monto contado
                    cuentaAjuste.Saldo -= diferencia;

                    db.SaveChanges();

                    // Vincular el movimiento al arqueo
                    arqueo.MovimientoAjusteId = movCaja.Id;

                    // Registrar en Audit
                    AuditService.Registrar("AJUSTE", "Arqueo", arqueo.Id,
                        datosNuevos: new { diferencia, tipoAjuste, saldoAnterior = saldoSistema, saldoNuevo = montoContado });
                }

                db.SaveChanges();
                transaction.Commit();

                return ArqueoResult.Success(arqueo.Id, diferencia);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return ArqueoResult.Error($"Error al realizar arqueo: {ex.Message}");
            }
        }
    }
}
