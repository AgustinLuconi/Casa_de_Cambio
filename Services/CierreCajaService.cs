using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Services
{
    public class CierreResult
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; } = "";
        public int? CierreId { get; set; }
        public CierreCaja? Cierre { get; set; }

        public static CierreResult Success(CierreCaja cierre) =>
            new() { Exitoso = true, CierreId = cierre.Id, Cierre = cierre };

        public static CierreResult Error(string msg) =>
            new() { Exitoso = false, Mensaje = msg };
    }

    /// <summary>
    /// Servicio para el cierre diario de caja.
    /// </summary>
    public class CierreCajaService : ICierreCajaService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IAuditService _auditService;

        public CierreCajaService(
            IDbContextFactory<AppDbContext> contextFactory,
            IAuditService auditService)
        {
            _contextFactory = contextFactory;
            _auditService = auditService;
        }

        /// <summary>
        /// Genera el cierre del día actual (sin cerrar definitivamente)
        /// </summary>
        public CierreResult GenerarCierre(string observaciones = "")
        {
            using var db = _contextFactory.CreateDbContext();

            var hoy = DateTime.UtcNow.Date;

            // Verificar si ya existe un cierre CERRADO para hoy
            var cierreExistente = db.CierresCaja
                .AsEnumerable()
                .FirstOrDefault(c => c.Fecha.Date == hoy);

            if (cierreExistente != null && cierreExistente.Cerrado)
            {
                return CierreResult.Error(
                    $"Ya existe un cierre cerrado para el día {hoy:dd/MM/yyyy}. " +
                    "No se pueden realizar más operaciones.");
            }

            var cierre = new CierreCaja
            {
                Fecha = hoy,
                FechaCierre = DateTime.Now,
                Usuario = "Admin",
                Observaciones = observaciones
            };

            // Calcular operaciones del día
            var inicioHoy = hoy;
            var finHoy = hoy.AddDays(1);

            var operacionesHoy = db.Operaciones
                .Where(o => o.Fecha >= inicioHoy && o.Fecha < finHoy)
                .ToList();

            // COMPRAS (recibo divisa, pago pesos)
            var compras = operacionesHoy.Where(o => o.TipoOperacion == "Compra").ToList();
            cierre.CantidadCompras = compras.Count;
            cierre.TotalComprasUSD = compras.Sum(o => o.MontoTotalDestino);
            cierre.TotalComprasARS = compras.Sum(o => o.MontoTotalOrigen);

            // VENTAS (vendo divisa, recibo pesos)
            var ventas = operacionesHoy.Where(o => o.TipoOperacion == "Venta").ToList();
            cierre.CantidadVentas = ventas.Count;
            cierre.TotalVentasUSD = ventas.Sum(o => o.MontoTotalOrigen);
            cierre.TotalVentasARS = ventas.Sum(o => o.MontoTotalDestino);

            // SALDOS ACTUALES de las cajas (desde SaldosCuenta)
            cierre.SaldoCajaARS = db.SaldosCuenta
                .Where(s => s.Moneda == "ARS" && s.Cuenta.Tipo == "Caja")
                .Sum(s => s.Saldo);

            cierre.SaldoCajaUSD = db.SaldosCuenta
                .Where(s => s.Moneda == "USD" && s.Cuenta.Tipo == "Caja")
                .Sum(s => s.Saldo);

            cierre.SaldoCajaEUR = db.SaldosCuenta
                .Where(s => s.Moneda == "EUR" && s.Cuenta.Tipo == "Caja")
                .Sum(s => s.Saldo);

            // DIFERENCIAS de arqueos del día
            cierre.TotalDiferencias = db.Arqueos
                .AsEnumerable()
                .Where(a => a.Fecha.Date == hoy)
                .Sum(a => a.Diferencia);

            // Guardar o actualizar
            if (cierreExistente != null)
            {
                cierreExistente.FechaCierre = cierre.FechaCierre;
                cierreExistente.CantidadCompras = cierre.CantidadCompras;
                cierreExistente.TotalComprasUSD = cierre.TotalComprasUSD;
                cierreExistente.TotalComprasARS = cierre.TotalComprasARS;
                cierreExistente.CantidadVentas = cierre.CantidadVentas;
                cierreExistente.TotalVentasUSD = cierre.TotalVentasUSD;
                cierreExistente.TotalVentasARS = cierre.TotalVentasARS;
                cierreExistente.SaldoCajaARS = cierre.SaldoCajaARS;
                cierreExistente.SaldoCajaUSD = cierre.SaldoCajaUSD;
                cierreExistente.SaldoCajaEUR = cierre.SaldoCajaEUR;
                cierreExistente.TotalDiferencias = cierre.TotalDiferencias;
                cierreExistente.Observaciones = observaciones;

                cierre = cierreExistente;
            }
            else
            {
                db.CierresCaja.Add(cierre);
            }

            db.SaveChanges();

            // Registrar en auditoría
            _auditService.Registrar("CREATE", "CierreCaja", cierre.Id,
                datosNuevos: new
                {
                    fecha = cierre.Fecha,
                    compras = cierre.CantidadCompras,
                    ventas = cierre.CantidadVentas,
                    diferencias = cierre.TotalDiferencias
                });

            return CierreResult.Success(cierre);
        }

        /// <summary>
        /// Marca el cierre como definitivo (IRREVERSIBLE)
        /// </summary>
        public CierreResult CerrarDefinitivo(int cierreId)
        {
            using var db = _contextFactory.CreateDbContext();

            var cierre = db.CierresCaja.Find(cierreId);
            if (cierre == null)
                return CierreResult.Error("Cierre no encontrado");

            if (cierre.Cerrado)
                return CierreResult.Error("El cierre ya está cerrado");

            cierre.Cerrado = true;
            cierre.FechaCierre = DateTime.Now;
            db.SaveChanges();

            // Registrar en auditoría
            _auditService.Registrar("CLOSE", "CierreCaja", cierre.Id,
                datosNuevos: new { cerradoDefinitivamente = true });

            return CierreResult.Success(cierre);
        }

        /// <summary>
        /// Verifica si hoy ya hay un cierre cerrado (para bloquear operaciones)
        /// </summary>
        public bool HayDiaCerrado()
        {
            try
            {
                using var db = _contextFactory.CreateDbContext();
                var hoy = DateTime.UtcNow.Date;

                return db.CierresCaja
                    .Where(c => c.Cerrado)
                    .AsEnumerable()
                    .Any(c => c.Fecha.Date == hoy);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene el cierre del día actual (si existe)
        /// </summary>
        public CierreCaja? ObtenerCierreDeHoy()
        {
            try
            {
                using var db = _contextFactory.CreateDbContext();
                var hoy = DateTime.UtcNow.Date;

                return db.CierresCaja
                    .AsEnumerable()
                    .FirstOrDefault(c => c.Fecha.Date == hoy);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene el último cierre realizado
        /// </summary>
        public CierreCaja? ObtenerUltimoCierre()
        {
            using var db = _contextFactory.CreateDbContext();

            return db.CierresCaja
                .OrderByDescending(c => c.Fecha)
                .FirstOrDefault();
        }
    }
}
