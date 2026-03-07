using SistemaCambio.Models;
using SistemaCambio.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaCambio.Services
{
    public interface IDashboardService
    {
        Task<List<OperacionPorDia>> ObtenerOperacionesDiariasAsync(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null);

        List<OperacionPorDia> ObtenerOperacionesDiarias(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null);

        Task<List<OperacionPorMoneda>> ObtenerOperacionesPorMonedaAsync(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null);

        List<OperacionPorMoneda> ObtenerOperacionesPorMoneda(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null);

        Task<List<ComparativoMensual>> ObtenerComparativoMensualAsync(int cantidadMeses = 6);

        List<ComparativoMensual> ObtenerComparativoMensual(int cantidadMeses = 6);

        Task<(int compras, int ventas, decimal volumen)> ObtenerResumenHoyAsync();

        (int compras, int ventas, decimal volumen) ObtenerResumenHoy();
    }
}
