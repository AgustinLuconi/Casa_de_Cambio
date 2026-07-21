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

public class CompraViewModelTests
{
    // ── Datos de prueba compartidos ─────────────────────────────────
    // CrearCuentas/CrearMonedas/CrearCotizaciones y los mocks base viven en
    // TestHelpers (compartido con VentaViewModelTests).

    private static List<CotizacionDto> CrearCotizaciones(decimal compra = 1800m, decimal venta = 1820m)
        => TestHelpers.CrearCotizaciones(compra, venta);

    private static (Mock<ICasaCambioApiClient> api, Mock<IOfflineOperacionService> offline, Mock<IDialogService> dialog) CrearMocks(
        List<CotizacionDto>? cotizaciones = null)
    {
        var api = TestHelpers.CrearApiMock(cotizaciones);

        var offline = new Mock<IOfflineOperacionService>();
        offline.Setup(o => o.GuardarCompraAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        var dialog = TestHelpers.CrearDialogMockConfirmando();

        return (api, offline, dialog);
    }

    private static Task EjecutarAceptarAsync(CompraViewModel vm)
        => ((IAsyncRelayCommand)vm.AceptarCommand).ExecuteAsync(null);

    [Fact]
    public async Task Recalcular_CalculaPesosYVuelto()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);

        // El constructor dispara CargarDatosAsync() sin esperarlo (fire-and-forget).
        // Con mocks que devuelven tasks ya completadas (ReturnsAsync), toda la cadena de
        // carga inicial (cuentas/monedas/cotización) se resuelve de forma síncrona antes
        // de que este await haga nada útil. El delay queda como salvaguarda defensiva por
        // si en el futuro algún mock introduce latencia real — este patrón se repite en
        // todos los tests.
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1800";

        // La cultura del entorno de test no está garantizada, así que parseamos de
        // vuelta con MontoHelper en lugar de comparar el string formateado exacto.
        Assert.Equal(180000m, MontoHelper.Parsear(vm.PesosTexto));

        vm.IngresaTexto = "200000";
        Assert.Equal(20000m, MontoHelper.Parsear(vm.VueltoTexto));
    }

    [Fact]
    public async Task Recalcular_RedondeaAwayFromZero_EnPuntoMedioExacto()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        // 1.25 * 1800.1 = 2250.125 exacto (decimal, sin error de punto flotante):
        // el tercer decimal es exactamente 5 y el segundo decimal (2) es par, así que
        // AwayFromZero (2250.13) y ToEven (2250.12) dan resultados DISTINTOS. Un cambio
        // accidental de MidpointRounding.AwayFromZero a ToEven (o remover el redondeo)
        // haría que este assert falle.
        vm.MonedaExtranjeraTexto = "1.25";
        vm.CotizacionTexto = "1800.1";

        Assert.Equal(2250.13m, MontoHelper.Parsear(vm.PesosTexto));
    }

    [Fact]
    public async Task Aceptar_SinCuentasSeleccionadas_NoPideConfirmacion()
    {
        var (api, offline, dialog) = CrearMocks();
        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1800";
        // Forzamos null a propósito, sin depender de que la autoselección de carga los complete.
        vm.CuentaAcredita = null;
        vm.CuentaDebita = null;

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Aceptar_CotizacionInusual_PideConfirmacionExtra()
    {
        var (api, offline, dialog) = CrearMocks(CrearCotizaciones(compra: 1800m));
        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "2000"; // difiere >5% de 1800

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            "Cotización inusual", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task Aceptar_CotizacionDentroDeUmbral_NoPideConfirmacionExtra()
    {
        var (api, offline, dialog) = CrearMocks(CrearCotizaciones(compra: 1800m));
        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1810"; // ~0.55%, dentro del umbral del 5%

        await EjecutarAceptarAsync(vm);

        dialog.Verify(d => d.ConfirmarAsync(
            "Cotización inusual", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Aceptar_Exitoso_DisparaOperacionGuardada()
    {
        var (api, offline, dialog) = CrearMocks();
        offline.Setup(o => o.GuardarCompraAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 42, IsOffline = false, Mensaje = "ok" });

        var vm = new CompraViewModel(api.Object, offline.Object, dialog.Object);
        await Task.Delay(50);

        vm.MonedaExtranjeraTexto = "100";
        vm.CotizacionTexto = "1800";

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
