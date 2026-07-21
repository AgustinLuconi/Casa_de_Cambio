using System;
using System.Collections.Generic;
using CasaCambio.Shared.DTOs;
using Moq;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;

namespace SistemaCambio.ViewModels.Tests;

/// <summary>
/// Helpers compartidos entre los tests de ViewModels de operaciones (Compra/Venta),
/// para evitar duplicar la construcción de cuentas, monedas, cotizaciones y mocks.
/// CreditoDebitoViewModelTests no los usa para los datos de dominio (cuentas/monedas/
/// cotizaciones) porque su escenario es genuinamente distinto — todas las cuentas
/// comparten moneda y no hay par de monedas involucrado — pero sí puede reutilizar
/// el mock de diálogo.
/// </summary>
internal static class TestHelpers
{
    public static List<CuentaDto> CrearCuentas() => new()
    {
        new CuentaDto
        {
            Id = 1, Nombre = "EFECTIVO ARS", Tipo = "Efectivo",
            Saldos = new() { new SaldoCuentaDto { Moneda = "ARS", Saldo = 10_000_000m } }
        },
        new CuentaDto
        {
            Id = 2, Nombre = "EFECTIVO USD", Tipo = "Efectivo",
            Saldos = new() { new SaldoCuentaDto { Moneda = "USD", Saldo = 50_000m } }
        }
    };

    public static List<MonedaDto> CrearMonedas() => new()
    {
        new MonedaDto { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true }
    };

    public static List<CotizacionDto> CrearCotizaciones(decimal compra = 1800m, decimal venta = 1820m) => new()
    {
        new CotizacionDto { Id = 1, CodigoMoneda = "USD", Fecha = DateTime.Today, CotizacionCompra = compra, CotizacionVenta = venta }
    };

    /// <summary>
    /// Mock de <see cref="ICasaCambioApiClient"/> con las respuestas comunes a la carga
    /// inicial (cuentas, monedas, cotización de hoy) que usan tanto CompraViewModel como
    /// VentaViewModel. Cada test agrega encima los setups específicos que necesite
    /// (p. ej. ValidarVentaPPPAsync en VentaViewModelTests).
    /// </summary>
    public static Mock<ICasaCambioApiClient> CrearApiMock(List<CotizacionDto>? cotizaciones = null)
    {
        var api = new Mock<ICasaCambioApiClient>();
        api.Setup(a => a.ObtenerCuentasAsync()).ReturnsAsync(CrearCuentas());
        api.Setup(a => a.ObtenerMonedasAsync()).ReturnsAsync(CrearMonedas());
        api.Setup(a => a.ObtenerCotizacionesHoyAsync()).ReturnsAsync(cotizaciones ?? CrearCotizaciones());
        return api;
    }

    /// <summary>
    /// Mock de <see cref="IDialogService"/> que confirma cualquier diálogo (ConfirmarAsync
    /// siempre devuelve true). Es el comportamiento por defecto que necesita la mayoría de
    /// los tests; los que quieran simular un "cancelar" lo sobreescriben puntualmente.
    /// </summary>
    public static Mock<IDialogService> CrearDialogMockConfirmando()
    {
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        return dialog;
    }
}
