using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.Input;
using Moq;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.ViewModels;
using Xunit;

namespace SistemaCambio.ViewModels.Tests;

public class CreditoDebitoViewModelTests
{
    // ── Datos de prueba compartidos ─────────────────────────────────
    // A diferencia de Compra/Venta, esta ventana no excluye ARS de MonedasDisponibles,
    // y todas las cuentas de CuentasDisponibles quedan etiquetadas con la MISMA moneda
    // (la moneda seleccionada), por construcción de RefrescarCombosCuentas.

    private static List<CuentaDto> CrearCuentas() => new()
    {
        new CuentaDto
        {
            Id = 1, Nombre = "CAJA 1", Tipo = "Efectivo",
            Saldos = new() { new SaldoCuentaDto { Moneda = "ARS", Saldo = 10_000_000m } }
        },
        new CuentaDto
        {
            Id = 2, Nombre = "CAJA 2", Tipo = "Efectivo",
            Saldos = new() { new SaldoCuentaDto { Moneda = "ARS", Saldo = 5_000_000m } }
        }
    };

    private static List<MonedaDto> CrearMonedas() => new()
    {
        new MonedaDto { Id = 1, Codigo = "ARS", Nombre = "Peso", Activa = true }
    };

    private static List<CotizacionDto> CrearCotizaciones() => new()
    {
        new CotizacionDto { Id = 1, CodigoMoneda = "ARS", Fecha = DateTime.Today, CotizacionCompra = 1m, CotizacionVenta = 1m }
    };

    private static (Mock<ICasaCambioApiClient> api, Mock<IOfflineOperacionService> offline, Mock<IDialogService> dialog) CrearMocks()
    {
        var api = new Mock<ICasaCambioApiClient>();
        api.Setup(a => a.ObtenerCuentasAsync()).ReturnsAsync(CrearCuentas());
        api.Setup(a => a.ObtenerMonedasAsync()).ReturnsAsync(CrearMonedas());
        api.Setup(a => a.ObtenerCotizacionesHoyAsync()).ReturnsAsync(CrearCotizaciones());

        var offline = new Mock<IOfflineOperacionService>();
        offline.Setup(o => o.GuardarCreditoDebitoAsync(It.IsAny<CrearCreditoDebitoRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        var dialog = TestHelpers.CrearDialogMockConfirmando();

        return (api, offline, dialog);
    }

    private static Task EjecutarAceptarAsync(CreditoDebitoViewModel vm)
        => ((IAsyncRelayCommand)vm.AceptarCommand).ExecuteAsync(null);

    [Fact]
    public async Task Aceptar_MismaMoneda_IgnoraElTextoDeCotizacionYUsaUno()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new CreditoDebitoViewModel(api.Object, offline.Object, dialog.Object);

        // El constructor dispara CargarDatosAsync() sin esperarlo. Con mocks que
        // devuelven tasks ya completadas (ReturnsAsync), la carga inicial se resuelve
        // de forma síncrona antes de que este await haga nada útil; queda como
        // salvaguarda defensiva por si en el futuro algún mock introduce latencia real.
        await Task.Delay(50);

        Assert.Equal(2, vm.CuentasDisponibles.Count);
        vm.CuentaCredito = vm.CuentasDisponibles[0]; // CAJA 1
        vm.CuentaDebito = vm.CuentasDisponibles[1];  // CAJA 2 (misma moneda: ambas etiquetadas "ARS")
        Assert.Equal(vm.CuentaCredito.Moneda, vm.CuentaDebito.Moneda);

        vm.ImporteCreditoTexto = "1000";
        vm.ImporteDebitoTexto = "1000";
        // Cotización arbitraria en el textbox: no debería importar, porque al ser
        // operación de la misma moneda el VM fuerza Cotizacion = 1m.
        vm.CotizacionCreditoTexto = "999.00000";

        CrearCreditoDebitoRequest? requestCapturado = null;
        offline.Setup(o => o.GuardarCreditoDebitoAsync(It.IsAny<CrearCreditoDebitoRequest>()))
            .Callback<CrearCreditoDebitoRequest>(req => requestCapturado = req)
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        await EjecutarAceptarAsync(vm);

        Assert.NotNull(requestCapturado);
        Assert.Equal(1m, requestCapturado!.Cotizacion);
    }

    [Fact]
    public async Task Aceptar_AmbosImportesEnCero_NoPideConfirmacion()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new CreditoDebitoViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        // ImporteCreditoTexto/ImporteDebitoTexto quedan en su valor por defecto "0.00".
        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }
}
