using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Services
{
    public class PPPValidacion
    {
        public bool Valido { get; set; }
        public decimal PPP { get; set; }
        public decimal CotizacionVenta { get; set; }
        public string Mensaje { get; set; } = "";
    }

    public class PPPService : IPPPService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PPPService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Registra una compra de divisa y actualiza el Costo Promedio Ponderado.
        /// Fórmula: PPP = (CostoTotalAnterior + NuevoCosto) / (CantidadAnterior + NuevaCantidad)
        /// </summary>
        public void RegistrarCompra(string codigoMoneda, decimal cantidadDivisa, decimal costoEnPesos)
        {
            using var db = _contextFactory.CreateDbContext();

            var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
            if (moneda == null) return;

            var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
            if (tenencia == null)
            {
                tenencia = new TenenciaMoneda
                {
                    MonedaId = moneda.Id,
                    CantidadTotal = 0,
                    CostoTotal = 0
                };
                db.TenenciasMoneda.Add(tenencia);
            }

            // Actualizar tenencia con nueva compra
            tenencia.CantidadTotal += cantidadDivisa;
            tenencia.CostoTotal += costoEnPesos;

            db.SaveChanges();
        }

        /// <summary>
        /// Registra una venta de divisa y reduce la tenencia.
        /// </summary>
        public void RegistrarVenta(string codigoMoneda, decimal cantidadDivisa)
        {
            using var db = _contextFactory.CreateDbContext();

            var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
            if (moneda == null) return;

            var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
            if (tenencia == null || tenencia.CantidadTotal == 0) return;

            // Calcular el costo proporcional que se "vende"
            decimal ppp = tenencia.CostoTotal / tenencia.CantidadTotal;
            decimal costoVendido = cantidadDivisa * ppp;

            tenencia.CantidadTotal -= cantidadDivisa;
            tenencia.CostoTotal -= costoVendido;

            // Evitar valores negativos por redondeo
            if (tenencia.CantidadTotal < 0) tenencia.CantidadTotal = 0;
            if (tenencia.CostoTotal < 0) tenencia.CostoTotal = 0;

            db.SaveChanges();
        }

        /// <summary>
        /// Valida si la cotización de venta está por encima del Costo Promedio Ponderado.
        /// Retorna advertencia si se vende por debajo del costo de adquisición.
        /// </summary>
        public PPPValidacion ValidarVenta(string codigoMoneda, decimal cotizacionVenta)
        {
            using var db = _contextFactory.CreateDbContext();

            var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
            if (moneda == null)
                return new PPPValidacion { Valido = true };

            var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
            if (tenencia == null || tenencia.CantidadTotal == 0)
                return new PPPValidacion { Valido = true, Mensaje = "Sin tenencia previa" };

            decimal ppp = tenencia.CostoTotal / tenencia.CantidadTotal;
            bool esRentable = cotizacionVenta >= ppp;

            return new PPPValidacion
            {
                Valido = true, // Siempre permite, pero advierte
                PPP = ppp,
                CotizacionVenta = cotizacionVenta,
                Mensaje = esRentable 
                    ? $"✓ Rentable. PPP: {ppp:N2}, Ganancia: {(cotizacionVenta - ppp):N2}/unidad"
                    : $"⚠️ ALERTA: Vendiendo por debajo del costo. PPP: {ppp:N2}, Pérdida: {(ppp - cotizacionVenta):N2}/unidad"
            };
        }

        /// <summary>
        /// Obtiene el PPP actual de una moneda.
        /// </summary>
        public decimal ObtenerPPP(string codigoMoneda)
        {
            using var db = _contextFactory.CreateDbContext();

            var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
            if (moneda == null) return 0;

            var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
            if (tenencia == null || tenencia.CantidadTotal == 0) return 0;

            return tenencia.CostoTotal / tenencia.CantidadTotal;
        }
    }
}
