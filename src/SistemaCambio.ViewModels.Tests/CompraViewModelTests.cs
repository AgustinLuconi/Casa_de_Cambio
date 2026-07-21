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

        var offline = new Mock<IOfflineOperacionService>();
        offline.Setup(o => o.GuardarCompraAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(new OfflineOperacionResult { Exitoso = true, OperacionId = 1, IsOffline = false });

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

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
        // Damos tiempo a que la carga inicial (cuentas/monedas/cotización) se asiente
        // antes de tocar las propiedades de texto — este patrón se repite en todos los tests.
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
