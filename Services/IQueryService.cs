using SistemaCambio.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaCambio.Services
{
    public interface IQueryService
    {
        Task<List<Operacion>> ObtenerOperacionesAsync(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? tipoOperacion = null,
            int? clienteId = null,
            int maxResultados = 500);

        Task<List<Operacion>> BuscarOperacionesAsync(string textoBusqueda);

        Task<ResumenMensual> ObtenerResumenMesAsync(int año, int mes);

        Task<List<Cuenta>> ObtenerSaldosCuentasAsync(bool forzarActualizacion = false);

        void InvalidarCacheSaldos();

        Task<List<Movimiento>> ObtenerMovimientosCuentaAsync(
            int cuentaId,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int maxResultados = 500);

        Task<bool> ExisteOperacionAsync(int operacionId);

        Task<PaginatedResult<Operacion>> ObtenerOperacionesPaginadasAsync(
            int pageNumber = 1,
            int pageSize = 50,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? tipoOperacion = null);

        Task<(int compras, int ventas, decimal volumen)> ContarOperacionesHoyAsync();
    }
}
