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
    // CrearCuentas/CrearMonedas/CrearCotizaciones y los mocks base viven en
    // TestHelpers (compartido con CompraViewModelTests).

    private static List<CotizacionDto> CrearCotizaciones(decimal compra = 1800m, decimal venta = 1820m)
        => TestHelpers.CrearCotizaciones(compra, venta);

    private static (Mock<ICasaCambioApiClient> api, Mock<IOfflineOperacionService> offline, Mock<IDialogService> dialog) CrearMocks(
        List<CotizacionDto>? cotizaciones = null)
    {
        var api = TestHelpers.CrearApiMock(cotizaciones);
        api.Setup(a => a.ValidarVentaPPPAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new PPPValidacionDto { Moneda = "USD", PPP = 1800m, CotizacionVenta = 1820m, Ganancia = 20m, EsRentable = true });

        var offline = new Mock<IOfflineOperacionService>();
        offline.Setup(o => o.GuardarVentaAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        var dialog = TestHelpers.CrearDialogMockConfirmando();

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
        // CargarDatosAsync() sin esperarlo. Con mocks que devuelven tasks ya completadas
        // (ReturnsAsync), la carga inicial se resuelve de forma síncrona antes de que este
        // await haga nada útil; queda como salvaguarda defensiva por si en el futuro algún
        // mock introduce latencia real.
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1820";

        Assert.Equal(182000m, MontoHelper.Parsear(vm.PesosTexto));

        vm.IngresaTexto = "200000";
        Assert.Equal(18000m, MontoHelper.Parsear(vm.VueltoTexto));
    }

    [Fact]
    public async Task Recalcular_RedondeaAwayFromZero_EnPuntoMedioExacto()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new VentaViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        // Ver comentario equivalente en CompraViewModelTests: 1.25 * 1800.1 = 2250.125
        // exacto, con tercer decimal 5 y segundo decimal par, de modo que AwayFromZero
        // (2250.13) y ToEven (2250.12) difieren. Detecta un cambio accidental de modo
        // de redondeo o la eliminación del redondeo.
        vm.MonedaExtranjeraTexto = "1.25";
        vm.CotizacionTexto = "1800.1";

        Assert.Equal(2250.13m, MontoHelper.Parsear(vm.PesosTexto));
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
        string? mensaje = null;
        vm.OperacionGuardada += (id, offlineFlag, msg) =>
        {
            operacionId = id;
            isOffline = offlineFlag;
            mensaje = msg;
        };

        await EjecutarAceptarAsync(vm);

        Assert.Equal(42, operacionId);
        Assert.False(isOffline);
        Assert.Equal("ok", mensaje);
    }
}
