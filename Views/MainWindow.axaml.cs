using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Controls;
using SistemaCambio.Views.Helpers;
using CasaCambio.Shared.DTOs;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SistemaCambio.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private readonly ICasaCambioApiClient _apiClient;
    private bool _diaCerrado;
    private bool _historialVisible;
    private int _notificacionesNoVistas;

    public MainWindow()
    {
        _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();

        InitializeComponent();
        Services.NotificationService.Initialize(notificationPanel);
        Services.NotificationService.HistorialActualizado += ActualizarBadgeHistorial;
        AddHandler(PointerPressedEvent, VentanaPointerPressed, handledEventsToo: false);

        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                _viewModel = vm;
                _viewModel.SolicitarAbrirVentana += AbrirVentanaPorNombre;
                _viewModel.SolicitarDetalleCuenta += async (id) => await AbrirDetalleCuentaWindow(id);
                _viewModel.SolicitarEdicionCuenta += async (id) => await AbrirEdicionCuentaWindow(id);
                _viewModel.MostrarMensajeEvent += MostrarMensajeEnUI;
                _viewModel.MostrarConfirmacionEvent += (t, m) => DialogHelper.ConfirmarAsync(this, t, m, "Si, eliminar cuenta", destructivo: true);
                _viewModel.DashboardCargado += dashboard =>
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        txtTotalCuentas.Text   = dashboard.TotalOperacionesHoy.ToString();
                        txtOperacionesHoy.Text = (dashboard.TotalComprasHoy + dashboard.TotalVentasHoy).ToString();
                        txtSaldoUSD.Text = $"${dashboard.SaldosCaja.Where(s => s.Moneda == "USD").Sum(s => s.Saldo):N2}";
                        txtSaldoARS.Text = $"${dashboard.SaldosCaja.Where(s => s.Moneda == "ARS").Sum(s => s.Saldo):N2}";
                        PoblarGraficoSaldos(dashboard);
                        PoblarGraficoPie(dashboard);
                        PoblarGraficoOperacionesDiarias(dashboard);
                        PoblarGraficoComparativo(dashboard);
                        PoblarGraficoDistribucion(dashboard);
                    });
                VerificarDiaCerradoAsync();
            }
        };
    }

    private async void AbrirVentanaPorNombre(string nombreVentana)
    {
        switch (nombreVentana)
        {
            case "Dashboard": BtnDashboard_Click(null, null); break;
            case "Cuentas": BtnCuentas_Click(null, null); break;
            case "Compra": await AbrirCompraWindow(); break;
            case "Venta": await AbrirVentaWindow(); break;
            case "CreditoDebito": await AbrirCreditoDebitoWindow(); break;
            case "Arqueo": await AbrirArqueoWindow(); break;
            case "Movimientos": await AbrirMovimientosWindow(); break;
            case "NuevaCuenta": await AbrirNuevaCuentaWindow(); break;
            case "Reportes": await AbrirReportesWindow(); break;
            case "Configuracion": await AbrirConfiguracionWindow(); break;
        }
    }

    private void BtnDashboard_Click(object? sender, RoutedEventArgs e)
    {
        dashboardPanel.IsVisible = true;
        cuentasPanel.IsVisible = false;
        txtMainTitle.Text = "Dashboard";
        btnDashboard.Classes.Set("SidebarButtonActive", true);
        btnDashboard.Classes.Set("SidebarButton", false);
        btnCuentas.Classes.Set("SidebarButtonActive", false);
        btnCuentas.Classes.Set("SidebarButton", true);
        _viewModel?.RefrescarDatos();
        CargarUltimasOperaciones();
    }

    private void BtnCuentas_Click(object? sender, RoutedEventArgs e)
    {
        dashboardPanel.IsVisible = false;
        cuentasPanel.IsVisible = true;
        txtMainTitle.Text = "Cuentas Overview";
        btnCuentas.Classes.Set("SidebarButtonActive", true);
        btnCuentas.Classes.Set("SidebarButton", false);
        btnDashboard.Classes.Set("SidebarButtonActive", false);
        btnDashboard.Classes.Set("SidebarButton", true);
        _viewModel?.RefrescarDatos();
    }

    private async void CargarUltimasOperaciones()
    {
        try
        {
            var opsResponse = await _apiClient.ObtenerOperacionesAsync(page: 1, pageSize: 10);
            dgUltimasOperaciones.ItemsSource = opsResponse.Items;
        }
        catch (Exception ex) { AppLogger.Warn("CargarUltimasOperaciones", ex); }
    }

    private void PoblarGraficoSaldos(CasaCambio.Shared.DTOs.DashboardDto dashboard)
    {
        pltBalanceEvolution.Plot.Clear();
        var monedas = dashboard.SaldosCaja
            .Where(s => s.Saldo != 0)
            .OrderByDescending(s => Math.Abs(s.Saldo))
            .Take(8).ToList();
        if (!monedas.Any()) { MostrarSinDatos(pltBalanceEvolution.Plot); pltBalanceEvolution.Refresh(); return; }

        var values = monedas.Select(s => (double)Math.Abs(s.Saldo)).ToArray();
        var bar = pltBalanceEvolution.Plot.Add.Bars(values);
        var barList = bar.Bars.ToList();
        for (int i = 0; i < monedas.Count; i++)
        {
            barList[i].FillColor = monedas[i].Saldo >= 0
                ? ScottPlot.Color.FromHex("#13a4ec")
                : ScottPlot.Color.FromHex("#ef4444");
        }
        var tickGen = new ScottPlot.TickGenerators.NumericManual();
        for (int i = 0; i < monedas.Count; i++)
            tickGen.AddMajor(i + 1, monedas[i].Moneda);
        pltBalanceEvolution.Plot.Axes.Bottom.TickGenerator = tickGen;
        pltBalanceEvolution.Plot.Axes.Left.Label.Text = "Saldo";
        ConfigurarTemaOscuro(pltBalanceEvolution.Plot);
        pltBalanceEvolution.Refresh();
    }

    private void PoblarGraficoPie(CasaCambio.Shared.DTOs.DashboardDto dashboard)
    {
        pltCurrencyDistribution.Plot.Clear();
        var positivos = dashboard.SaldosCaja
            .Where(s => s.Saldo > 0)
            .OrderByDescending(s => s.Saldo)
            .Take(6).ToList();
        if (!positivos.Any()) { MostrarSinDatos(pltCurrencyDistribution.Plot); pltCurrencyDistribution.Refresh(); return; }

        var pie = pltCurrencyDistribution.Plot.Add.Pie(
            positivos.Select(s => (double)s.Saldo).ToArray());
        var colores = new[] { "#13a4ec", "#0d79b3", "#1e3441", "#22c55e", "#eab308", "#94a3b8" };
        for (int i = 0; i < pie.Slices.Count; i++)
        {
            pie.Slices[i].Label     = positivos[i].Moneda;
            pie.Slices[i].FillColor = ScottPlot.Color.FromHex(colores[i % colores.Length]);
        }
        pltCurrencyDistribution.Plot.ShowLegend();
        ConfigurarTemaOscuro(pltCurrencyDistribution.Plot);
        pltCurrencyDistribution.Refresh();
    }

    private static void ConfigurarTemaOscuro(ScottPlot.Plot plot)
    {
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#14232b");
        plot.DataBackground.Color   = ScottPlot.Color.FromHex("#0c151a");
        plot.Axes.Color(ScottPlot.Color.FromHex("#94a3b8"));
    }

    private static void MostrarSinDatos(ScottPlot.Plot plot, string mensaje = "Sin datos")
    {
        plot.Clear();
        plot.Add.Annotation(mensaje);
        ConfigurarTemaOscuro(plot);
    }

    private void PoblarGraficoOperacionesDiarias(DashboardDto dashboard)
    {
        pltOperacionesDiarias.Plot.Clear();
        var data = dashboard.OperacionesPorDia;
        if (!data.Any()) { MostrarSinDatos(pltOperacionesDiarias.Plot); pltOperacionesDiarias.Refresh(); return; }

        var xs      = data.Select(d => d.Fecha.ToOADate()).ToArray();
        var compras = data.Select(d => (double)d.CantidadCompras).ToArray();
        var ventas  = data.Select(d => (double)d.CantidadVentas).ToArray();

        var scCompras = pltOperacionesDiarias.Plot.Add.Scatter(xs, compras);
        scCompras.Color = ScottPlot.Color.FromHex("#13a4ec");
        scCompras.LegendText = "Compras";

        var scVentas = pltOperacionesDiarias.Plot.Add.Scatter(xs, ventas);
        scVentas.Color = ScottPlot.Color.FromHex("#22c55e");
        scVentas.LegendText = "Ventas";

        pltOperacionesDiarias.Plot.Axes.DateTimeTicksBottom();
        pltOperacionesDiarias.Plot.ShowLegend();
        ConfigurarTemaOscuro(pltOperacionesDiarias.Plot);
        pltOperacionesDiarias.Refresh();
    }

    private void PoblarGraficoComparativo(DashboardDto dashboard)
    {
        pltComparativoMensual.Plot.Clear();
        var meses = dashboard.ComparativoMensual;
        if (!meses.Any()) { MostrarSinDatos(pltComparativoMensual.Plot); pltComparativoMensual.Refresh(); return; }

        int n = meses.Count;
        var barsCompras = meses.Select((m, i) => new ScottPlot.Bar
        {
            Position  = i * 2.5,
            Value     = (double)m.VolumenComprasARS / 1_000,
            FillColor = ScottPlot.Color.FromHex("#13a4ec"),
            Size      = 1.0
        }).ToList();
        var barsVentas = meses.Select((m, i) => new ScottPlot.Bar
        {
            Position  = i * 2.5 + 1.0,
            Value     = (double)m.VolumenVentasARS / 1_000,
            FillColor = ScottPlot.Color.FromHex("#22c55e"),
            Size      = 1.0
        }).ToList();

        var bcCompras = pltComparativoMensual.Plot.Add.Bars(barsCompras);
        bcCompras.LegendText = "Compras";
        var bcVentas = pltComparativoMensual.Plot.Add.Bars(barsVentas);
        bcVentas.LegendText = "Ventas";

        var tickGen = new ScottPlot.TickGenerators.NumericManual();
        for (int i = 0; i < n; i++)
            tickGen.AddMajor(i * 2.5 + 0.5, $"{new DateTime(meses[i].Anio, meses[i].Mes, 1):MMM/yy}");
        pltComparativoMensual.Plot.Axes.Bottom.TickGenerator = tickGen;
        pltComparativoMensual.Plot.Axes.Left.Label.Text = "Miles ARS";
        pltComparativoMensual.Plot.ShowLegend();
        ConfigurarTemaOscuro(pltComparativoMensual.Plot);
        pltComparativoMensual.Refresh();
    }

    private void PoblarGraficoDistribucion(DashboardDto dashboard)
    {
        pltDistribucionMonedas.Plot.Clear();
        var data = dashboard.DistribucionMonedas.Take(6).ToList();
        if (!data.Any()) { MostrarSinDatos(pltDistribucionMonedas.Plot); pltDistribucionMonedas.Refresh(); return; }

        var pie = pltDistribucionMonedas.Plot.Add.Pie(data.Select(d => (double)d.VolumenTotal).ToArray());
        var colores = new[] { "#13a4ec", "#0d79b3", "#1e3441", "#22c55e", "#eab308", "#94a3b8" };
        for (int i = 0; i < pie.Slices.Count; i++)
        {
            pie.Slices[i].Label     = data[i].Moneda;
            pie.Slices[i].FillColor = ScottPlot.Color.FromHex(colores[i % colores.Length]);
        }
        pltDistribucionMonedas.Plot.ShowLegend();
        ConfigurarTemaOscuro(pltDistribucionMonedas.Plot);
        pltDistribucionMonedas.Refresh();
    }

    private async void BtnCompra_Click(object? sender, RoutedEventArgs e) => await AbrirCompraWindow();
    private async void BtnVenta_Click(object? sender, RoutedEventArgs e) => await AbrirVentaWindow();
    private async void BtnCreditoDebito_Click(object? sender, RoutedEventArgs e) => await AbrirCreditoDebitoWindow();
    private async void BtnArqueo_Click(object? sender, RoutedEventArgs e) => await AbrirArqueoWindow();
    private async void BtnMovimientos_Click(object? sender, RoutedEventArgs e) => await AbrirMovimientosWindow();

    private async void BtnCierreCaja_Click(object? sender, RoutedEventArgs e)
    {
        var cierreWindow = new CierreCajaWindow();
        await cierreWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
        CargarUltimasOperaciones();
        VerificarDiaCerradoAsync();
    }

    private async void BtnMiCuenta_Click(object? sender, RoutedEventArgs e)
    {
        await new MiCuentaWindow().ShowDialog(this);
    }

    private async void BtnReportes_Click(object? sender, RoutedEventArgs e) => await AbrirReportesWindow();
    private async void BtnConfiguracion_Click(object? sender, RoutedEventArgs e) => await AbrirConfiguracionWindow();
    private async void BtnNuevaCuenta_Click(object? sender, RoutedEventArgs e) => await AbrirNuevaCuentaWindow();

    private async Task AbrirNuevaCuentaWindow() { var w = new NuevaCuentaWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos(); }
    private async Task AbrirEdicionCuentaWindow(int cuentaId) { var w = new NuevaCuentaWindow(cuentaId); await w.ShowDialog(this); _viewModel?.RefrescarDatos(); }
    private async Task AbrirDetalleCuentaWindow(int cuentaId) { var w = new DetalleCuentaWindow(cuentaId); await w.ShowDialog(this); }

    private async void VerificarDiaCerradoAsync()
    {
        try
        {
            var cierre = await _apiClient.ObtenerCierreHoyAsync();
            _diaCerrado = cierre?.Cerrado == true;
        }
        catch { _diaCerrado = false; }

        if (_diaCerrado)
        {
            btnToolbarCompra.IsEnabled = false;
            btnToolbarVenta.IsEnabled = false;
            btnToolbarCreditoDebito.IsEnabled = false;
            btnCompra.IsEnabled = false;
            btnVenta.IsEnabled = false;
            btnCreditoDebito.IsEnabled = false;
            borderDiaCerrado.IsVisible = true;
            Services.NotificationService.Warning("Día cerrado", "Las operaciones del día están bloqueadas.");
        }
    }

    private void MostrarMensajeEnUI(string titulo, string mensaje)
    {
        if (titulo == "Error") Services.NotificationService.Error(titulo, mensaje);
        else Services.NotificationService.Success(titulo, mensaje);
    }

    private async void BtnExportar_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Cuentas == null || !_viewModel.Cuentas.Any()) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Exportar Cuentas", SuggestedFileName = $"cuentas_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
        if (file != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID,Cuenta,Tipo,Saldos");
            foreach (var c in _viewModel.Cuentas) sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},\"{c.SaldosResumen}\"");
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(sb.ToString());
        }
    }

    private async Task AbrirCompraWindow()
    {
        if (_diaCerrado) { Services.NotificationService.Warning("Día cerrado", "No se pueden registrar operaciones."); return; }
        var w = new CompraWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos();
    }
    private async Task AbrirVentaWindow()
    {
        if (_diaCerrado) { Services.NotificationService.Warning("Día cerrado", "No se pueden registrar operaciones."); return; }
        var w = new VentaWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos();
    }
    private async Task AbrirCreditoDebitoWindow()
    {
        if (_diaCerrado) { Services.NotificationService.Warning("Día cerrado", "No se pueden registrar operaciones."); return; }
        var w = new CreditoDebitoWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos();
    }
    private async Task AbrirArqueoWindow() { var w = new ArqueoWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos(); }
    private async Task AbrirMovimientosWindow() { var w = new DetalleMovimientosWindow(); await w.ShowDialog(this); }
    private async Task AbrirReportesWindow() { var w = new ReportesWindow(); await w.ShowDialog(this); }
    private async Task AbrirConfiguracionWindow() { var w = new ConfiguracionWindow(); await w.ShowDialog(this); }

    // ── Historial de notificaciones ──────────────────────────────────────────

    public void RestaurarNotificationPanel()
    {
        Services.NotificationService.Initialize(notificationPanel);
    }

    private void BtnHistorial_Click(object? sender, RoutedEventArgs e)
    {
        _historialVisible = !_historialVisible;
        panelHistorial.IsVisible = _historialVisible;

        if (_historialVisible)
        {
            _notificacionesNoVistas = 0;
            ActualizarBadgeHistorial();
            ActualizarListaHistorial();
        }
    }

    private void BtnLimpiarHistorial_Click(object? sender, RoutedEventArgs e)
    {
        Services.NotificationService.LimpiarHistorial();
        ActualizarListaHistorial();
    }

    private void ActualizarBadgeHistorial()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_historialVisible)
                _notificacionesNoVistas++;

            var count = _notificacionesNoVistas;
            badgeHistorial.IsVisible = count > 0;
            txtBadgeCount.Text = count > 9 ? "9+" : count.ToString();
        });
    }

    private void ActualizarListaHistorial()
    {
        var items = Services.NotificationService.Historial
            .Select(n => new HistorialNotificacionItem
            {
                Title     = n.Title,
                Message   = n.Message,
                Timestamp = n.Timestamp,
                Type      = n.Type
            }).ToList();

        icHistorial.ItemsSource = items;
        txtSinNotificaciones.IsVisible = items.Count == 0;
    }

    private void VentanaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_historialVisible) return;

        var pos = e.GetPosition(panelHistorial);
        var bounds = panelHistorial.Bounds;
        bool dentroPanel = pos.X >= 0 && pos.Y >= 0 && pos.X <= bounds.Width && pos.Y <= bounds.Height;

        if (!dentroPanel)
        {
            _historialVisible = false;
            panelHistorial.IsVisible = false;
        }
    }
}
