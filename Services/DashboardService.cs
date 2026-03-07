using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using SistemaCambio.ViewModels;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaCambio.Services
{
    /// <summary>
    /// Servicio para obtener datos del dashboard y gráficos.
    /// OPTIMIZADO: Usa async/await y AsNoTracking para mejor performance.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public DashboardService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Obtiene datos de operaciones diarias para el período especificado (ASYNC)
        /// </summary>
        public async Task<List<OperacionPorDia>> ObtenerOperacionesDiariasAsync(
            DateTime? fechaDesde = null, 
            DateTime? fechaHasta = null)
        {
            using var db = _contextFactory.CreateDbContext();

            var desde = fechaDesde ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var hasta = fechaHasta ?? DateTime.UtcNow.Date.AddDays(1);

            var operaciones = await db.Operaciones
                .Where(o => o.Fecha >= desde && o.Fecha < hasta)
                .AsNoTracking()
                .ToListAsync();

            var porDia = operaciones
                .GroupBy(o => o.Fecha.Date)
                .Select(g => new OperacionPorDia
                {
                    Fecha = g.Key,
                    CantidadCompras = g.Count(o => o.TipoOperacion == "Compra"),
                    CantidadVentas = g.Count(o => o.TipoOperacion == "Venta"),
                    MontoCompras = g.Where(o => o.TipoOperacion == "Compra")
                                    .Sum(o => o.MontoTotalOrigen),
                    MontoVentas = g.Where(o => o.TipoOperacion == "Venta")
                                   .Sum(o => o.MontoTotalDestino)
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            var resultado = new List<OperacionPorDia>();
            var fechaActual = desde.Date;
            
            while (fechaActual < hasta.Date)
            {
                var operacionDia = porDia.FirstOrDefault(p => p.Fecha == fechaActual);
                
                if (operacionDia != null)
                {
                    resultado.Add(operacionDia);
                }
                else
                {
                    resultado.Add(new OperacionPorDia 
                    { 
                        Fecha = fechaActual,
                        CantidadCompras = 0,
                        CantidadVentas = 0,
                        MontoCompras = 0,
                        MontoVentas = 0
                    });
                }
                
                fechaActual = fechaActual.AddDays(1);
            }

            return resultado;
        }

        public List<OperacionPorDia> ObtenerOperacionesDiarias(
            DateTime? fechaDesde = null, 
            DateTime? fechaHasta = null)
        {
            return ObtenerOperacionesDiariasAsync(fechaDesde, fechaHasta)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Obtiene operaciones agrupadas por moneda (ASYNC)
        /// Agrupa movimientos por la moneda de la cuenta asociada via SaldosCuenta.
        /// </summary>
        public async Task<List<OperacionPorMoneda>> ObtenerOperacionesPorMonedaAsync(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null)
        {
            using var db = _contextFactory.CreateDbContext();

            var desde = fechaDesde ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var hasta = fechaHasta ?? DateTime.UtcNow.Date.AddDays(1);

            // Obtener las monedas de cada cuenta desde SaldosCuenta
            var saldosPorCuenta = await db.SaldosCuenta
                .AsNoTracking()
                .ToListAsync();

            var monedaPorCuenta = saldosPorCuenta
                .GroupBy(s => s.CuentaId)
                .ToDictionary(g => g.Key, g => g.Select(s => s.Moneda).Distinct().ToList());

            var movimientos = await db.Movimientos
                .Include(m => m.Operacion)
                .Where(m => m.Operacion.Fecha >= desde && m.Operacion.Fecha < hasta)
                .AsNoTracking()
                .ToListAsync();

            var porMoneda = movimientos
                .SelectMany(m =>
                {
                    if (monedaPorCuenta.TryGetValue(m.CuentaId, out var monedas) && monedas.Count > 0)
                        return monedas.Select(moneda => new { m, moneda });
                    return new[] { new { m, moneda = "N/A" } };
                })
                .GroupBy(x => x.moneda)
                .Select(g => new OperacionPorMoneda
                {
                    Moneda = g.Key,
                    Cantidad = g.Select(x => x.m.OperacionId).Distinct().Count(),
                    MontoTotal = g.Sum(x => Math.Abs(x.m.Monto))
                })
                .OrderByDescending(x => x.Cantidad)
                .ToList();

            return porMoneda;
        }

        public List<OperacionPorMoneda> ObtenerOperacionesPorMoneda(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null)
        {
            return ObtenerOperacionesPorMonedaAsync(fechaDesde, fechaHasta)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Obtiene comparativo de los últimos N meses (ASYNC)
        /// </summary>
        public async Task<List<ComparativoMensual>> ObtenerComparativoMensualAsync(int cantidadMeses = 6)
        {
            using var db = _contextFactory.CreateDbContext();

            var resultado = new List<ComparativoMensual>();
            var fechaActual = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var mesInicio = fechaActual.AddMonths(-(cantidadMeses - 1));
            var mesFin = fechaActual.AddMonths(1);

            var todasOperaciones = await db.Operaciones
                .Where(o => o.Fecha >= mesInicio && o.Fecha < mesFin)
                .AsNoTracking()
                .ToListAsync();

            for (int i = cantidadMeses - 1; i >= 0; i--)
            {
                var mes = fechaActual.AddMonths(-i);
                var mesSiguiente = mes.AddMonths(1);

                var operacionesMes = todasOperaciones
                    .Where(o => o.Fecha >= mes && o.Fecha < mesSiguiente)
                    .ToList();

                var compras = operacionesMes
                    .Where(o => o.TipoOperacion == "Compra")
                    .Sum(o => o.MontoTotalOrigen);

                var ventas = operacionesMes
                    .Where(o => o.TipoOperacion == "Venta")
                    .Sum(o => o.MontoTotalDestino);

                var ganancia = ventas - compras;

                resultado.Add(new ComparativoMensual
                {
                    Mes = mes.ToString("MMM yy"),
                    TotalCompras = compras,
                    TotalVentas = ventas,
                    Ganancia = ganancia
                });
            }

            return resultado;
        }

        public List<ComparativoMensual> ObtenerComparativoMensual(int cantidadMeses = 6)
        {
            return ObtenerComparativoMensualAsync(cantidadMeses)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Obtiene resumen rápido para el dashboard (ASYNC)
        /// </summary>
        public async Task<(int compras, int ventas, decimal volumen)> ObtenerResumenHoyAsync()
        {
            using var db = _contextFactory.CreateDbContext();
            
            var hoy = DateTime.UtcNow.Date;
            var manana = hoy.AddDays(1);

            var operacionesHoy = await db.Operaciones
                .Where(o => o.Fecha >= hoy && o.Fecha < manana)
                .AsNoTracking()
                .ToListAsync();

            var compras = operacionesHoy.Count(o => o.TipoOperacion == "Compra");
            var ventas = operacionesHoy.Count(o => o.TipoOperacion == "Venta");
            var volumen = operacionesHoy.Sum(o => o.MontoTotalOrigen + o.MontoTotalDestino);

            return (compras, ventas, volumen);
        }

        public (int compras, int ventas, decimal volumen) ObtenerResumenHoy()
        {
            return ObtenerResumenHoyAsync().GetAwaiter().GetResult();
        }
    }
}
