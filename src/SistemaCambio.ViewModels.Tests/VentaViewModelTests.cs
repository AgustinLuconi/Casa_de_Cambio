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

public class VentaViewModelTests
{
    // ── Datos de prueba compartidos ─────────────────────────────────

    private static List<CuentaDto> CrearCuentas() => new()
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

    private static List<MonedaDto> CrearMonedas() => new()
    {
        new MonedaDto { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true }
    };

    private static List<CotizacionDto> CrearCotizaciones(decimal compra = 1800m, decimal venta = 1820m) => new()
    {
        new CotizacionDto { Id = 1, CodigoMoneda = "USD", Fecha = DateTime.Today, CotizacionCompra = compra, CotizacionVenta = venta }
    };

    private static (Mock<ICasaCambioApiClient> api, Mock<IOfflineOperacionService> offline, Mock<IDialogService> dialog) CrearMocks(
        List<CotizacionDto>? cotizaciones = null)
    {
        var api = new Mock<ICasaCambioApiClient>();
        api.Setup(a => a.ObtenerCuentasAsync()).ReturnsAsync(CrearCuentas());
        api.Setup(a => a.ObtenerMonedasAsync()).ReturnsAsync(CrearMonedas());
        api.Setup(a => a.ObtenerCotizacionesHoyAsync()).ReturnsAsync(cotizaciones ?? CrearCotizaciones());
        api.Setup(a => a.ValidarVentaPPPAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new PPPValidacionDto { Moneda = "USD", PPP = 1800m, CotizacionVenta = 1820m, Ganancia = 20m, EsRentable = true });

        var offline = new Mock<IOfflineOperacionService>();
        offline.Setup(o => o.GuardarVentaAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        return (api, offline, dialog);
    }

    private static Task EjecutarAceptarAsync(VentaViewModel vm)
        => ((IAsyncRelayCommand)vm.AceptarCommand).ExecuteAsync(null);

    [Fact]
    public async Task Recalcular_CalculaPesosYVuelto()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);

        // Ver comentario equivalente en CompraViewModelTests: el constructor dispara
        // CargarDatosAsync() sin esperarlo, hay que darle tiempo antes de tocar propiedades.
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820";

        Assert.Equal(182000m, MontoHelper.Parsear(vm.PesosTexto));

        vm.IngresaTexto = "200000";
        Assert.Equal(18000m, MontoHelper.Parsear(vm.VueltoTexto));
    }

    [Fact]
    public async Task Aceptar_VentaPorDebajoDelPPP_PideConfirmacionExtra()
    {
        var (api, offline, dialog) = CrearMocks();
        api.Setup(a => a.ValidarVentaPPPAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new PPPValidacionDto { Moneda = "USD", PPP = 1830m, CotizacionVenta = 1820m, Ganancia = -10m, EsRentable = false });

        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820"; // igual a la cotización del día: evita la advertencia de "Cotización inusual"

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            "Venta por debajo del PPP", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task Aceptar_VentaRentable_NoPideConfirmacionDePPP()
    {
        var (api, offline, dialog) = CrearMocks();
        api.Setup(a => a.ValidarVentaPPPAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new PPPValidacionDto { Moneda = "USD", PPP = 1750m, CotizacionVenta = 1820m, Ganancia = 70m, EsRentable = true });

        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820";

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            "Venta por debajo del PPP", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Aceptar_SinCuentasSeleccionadas_NoPideConfirmacion()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820";
        vm.CuentaAcreditar = null;
        vm.CuentaDebitar = null;

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Aceptar_Exitoso_DisparaOperacionGuardada()
    {
        var (api, offline, dialog) = CrearMocks();
        offline.Setup(o => o.GuardarVentaAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 42, IsOffline = false, Mensaje = "ok" });

        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820";

        int? operacionId = null;
        bool? isOffline = null;
        vm.OperacionGuardada += (id, offlineFlag, _) =>
        {
            operacionId = id;
            isOffline = offlineFlag;
        };

        await EjecutarAceptarAsync(vm);

        Assert.Equal(42, operacionId);
        Assert.False(isOffline);
    }
}
