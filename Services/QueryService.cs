using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaCambio.Services
{
    /// <summary>
    /// Servicio para consultas optimizadas a la base de datos.
    /// Usa async/await, eager loading, y AsNoTracking para máxima performance.
    /// </summary>
    public class QueryService : IQueryService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // CACHE SIMPLE PARA SALDOS
        private DateTime _ultimaActualizacionSaldos = DateTime.MinValue;
        private List<Cuenta>? _cacheSaldos;

        public QueryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Obtiene operaciones con filtros (optimizado con eager loading)
        /// </summary>
        public async Task<List<Operacion>> ObtenerOperacionesAsync(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? tipoOperacion = null,
            int? clienteId = null,
            int maxResultados = 500)
        {
            using var db = _contextFactory.CreateDbContext();

            IQueryable<Operacion> query = db.Operaciones
                .Include(o => o.Cliente)
                .Include(o => o.Movimientos)
                    .ThenInclude(m => m.Cuenta);

            if (fechaDesde.HasValue)
                query = query.Where(o => o.Fecha >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(o => o.Fecha < fechaHasta.Value);

            if (!string.IsNullOrEmpty(tipoOperacion))
                query = query.Where(o => o.TipoOperacion == tipoOperacion);

            if (clienteId.HasValue)
                query = query.Where(o => o.ClienteId == clienteId.Value);

            var resultado = await query
                .OrderByDescending(o => o.Fecha)
                .Take(maxResultados)
                .AsNoTracking()
                .ToListAsync();

            return resultado;
        }

        /// <summary>
        /// Busca operaciones por texto (DNI cliente, observaciones, etc.)
        /// </summary>
        public async Task<List<Operacion>> BuscarOperacionesAsync(string textoBusqueda)
        {
            if (string.IsNullOrWhiteSpace(textoBusqueda))
                return new List<Operacion>();

            using var db = _contextFactory.CreateDbContext();

            textoBusqueda = textoBusqueda.ToLower().Trim();

            var resultado = await db.Operaciones
                .Include(o => o.Cliente)
                .Include(o => o.Movimientos)
                    .ThenInclude(m => m.Cuenta)
                .Where(o =>
                    o.Observaciones.ToLower().Contains(textoBusqueda) ||
                    (o.Cliente != null && o.Cliente.Documento.Contains(textoBusqueda)) ||
                    (o.Cliente != null && o.Cliente.Nombre.ToLower().Contains(textoBusqueda)) ||
                    o.Id.ToString().Contains(textoBusqueda)
                )
                .OrderByDescending(o => o.Fecha)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();

            return resultado;
        }

        /// <summary>
        /// Obtiene resumen de operaciones del mes (optimizado - agregaciones en BD)
        /// </summary>
        public async Task<ResumenMensual> ObtenerResumenMesAsync(int año, int mes)
        {
            using var db = _contextFactory.CreateDbContext();

            var inicioMes = new DateTime(año, mes, 1);
            var finMes = inicioMes.AddMonths(1);

            var resumen = await db.Operaciones
                .Where(o => o.Fecha >= inicioMes && o.Fecha < finMes)
                .GroupBy(o => 1)
                .Select(g => new ResumenMensual
                {
                    TotalOperaciones = g.Count(),
                    TotalCompras = g.Count(o => o.TipoOperacion == "Compra"),
                    TotalVentas = g.Count(o => o.TipoOperacion == "Venta"),
                    MontoTotalCompras = g.Where(o => o.TipoOperacion == "Compra")
                                         .Sum(o => o.MontoTotalOrigen),
                    MontoTotalVentas = g.Where(o => o.TipoOperacion == "Venta")
                                        .Sum(o => o.MontoTotalDestino)
                })
                .FirstOrDefaultAsync();

            return resumen ?? new ResumenMensual();
        }

        /// <summary>
        /// Obtiene saldos de cuentas (cacheado por 1 minuto)
        /// </summary>
        public async Task<List<Cuenta>> ObtenerSaldosCuentasAsync(bool forzarActualizacion = false)
        {
            if (!forzarActualizacion && 
                _cacheSaldos != null && 
                (DateTime.Now - _ultimaActualizacionSaldos).TotalMinutes < 1)
            {
                return _cacheSaldos;
            }

            using var db = _contextFactory.CreateDbContext();

            _cacheSaldos = await db.Cuentas
                .AsNoTracking()
                .ToListAsync();

            _ultimaActualizacionSaldos = DateTime.Now;

            return _cacheSaldos;
        }

        /// <summary>
        /// Invalida el cache de saldos
        /// </summary>
        public void InvalidarCacheSaldos()
        {
            _cacheSaldos = null;
            _ultimaActualizacionSaldos = DateTime.MinValue;
        }

        /// <summary>
        /// Obtiene movimientos de una cuenta (optimizado)
        /// </summary>
        public async Task<List<Movimiento>> ObtenerMovimientosCuentaAsync(
            int cuentaId,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int maxResultados = 500)
        {
            using var db = _contextFactory.CreateDbContext();

            IQueryable<Movimiento> query = db.Movimientos
                .Include(m => m.Cuenta)
                .Include(m => m.Operacion)
                .Where(m => m.CuentaId == cuentaId);

            if (fechaDesde.HasValue)
                query = query.Where(m => m.Fecha >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(m => m.Fecha < fechaHasta.Value);

            return await query
                .OrderByDescending(m => m.Fecha)
                .Take(maxResultados)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Verifica si existe una operación (sin traer todo el objeto)
        /// </summary>
        public async Task<bool> ExisteOperacionAsync(int operacionId)
        {
            using var db = _contextFactory.CreateDbContext();
            return await db.Operaciones.AnyAsync(o => o.Id == operacionId);
        }

        /// <summary>
        /// Obtiene operaciones paginadas para grandes volúmenes
        /// </summary>
        public async Task<PaginatedResult<Operacion>> ObtenerOperacionesPaginadasAsync(
            int pageNumber = 1,
            int pageSize = 50,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? tipoOperacion = null)
        {
            using var db = _contextFactory.CreateDbContext();

            IQueryable<Operacion> query = db.Operaciones
                .Include(o => o.Cliente)
                .Include(o => o.Movimientos)
                    .ThenInclude(m => m.Cuenta);

            if (fechaDesde.HasValue)
                query = query.Where(o => o.Fecha >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(o => o.Fecha < fechaHasta.Value);

            if (!string.IsNullOrEmpty(tipoOperacion))
                query = query.Where(o => o.TipoOperacion == tipoOperacion);

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.Fecha)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return new PaginatedResult<Operacion>
            {
                Items = items,
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Cuenta operaciones de hoy de forma eficiente
        /// </summary>
        public async Task<(int compras, int ventas, decimal volumen)> ContarOperacionesHoyAsync()
        {
            using var db = _contextFactory.CreateDbContext();
            
            var hoy = DateTime.UtcNow.Date;
            var manana = hoy.AddDays(1);

            var stats = await db.Operaciones
                .Where(o => o.Fecha >= hoy && o.Fecha < manana)
                .GroupBy(o => 1)
                .Select(g => new
                {
                    Compras = g.Count(o => o.TipoOperacion == "Compra"),
                    Ventas = g.Count(o => o.TipoOperacion == "Venta"),
                    Volumen = g.Sum(o => o.MontoTotalOrigen + o.MontoTotalDestino)
                })
                .FirstOrDefaultAsync();

            return stats != null 
                ? (stats.Compras, stats.Ventas, stats.Volumen) 
                : (0, 0, 0m);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MODELOS DE SOPORTE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Modelo para el resumen mensual de operaciones
    /// </summary>
    public class ResumenMensual
    {
        public int TotalOperaciones { get; set; }
        public int TotalCompras { get; set; }
        public int TotalVentas { get; set; }
        public decimal MontoTotalCompras { get; set; }
        public decimal MontoTotalVentas { get; set; }
    }

    /// <summary>
    /// Modelo genérico para resultados paginados
    /// </summary>
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
