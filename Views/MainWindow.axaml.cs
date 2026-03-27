using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
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

    public MainWindow()
    {
        _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();

        InitializeComponent();
        Services.NotificationService.Initialize(notificationPanel);

        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                _viewModel = vm;
                _viewModel.SolicitarAbrirVentana += AbrirVentanaPorNombre;
                _viewModel.SolicitarDetalleCuenta += async (id) => await AbrirDetalleCuentaWindow(id);
                _viewModel.SolicitarEdicionCuenta += async (id) => await AbrirEdicionCuentaWindow(id);
                _viewModel.MostrarMensajeEvent += MostrarMensajeEnUI;
                _viewModel.MostrarConfirmacionEvent += MostrarConfirmacionEnUI;
                _viewModel.DashboardCargado += dashboard =>
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        txtTotalCuentas.Text   = dashboard.TotalOperacionesHoy.ToString();
                        txtOperacionesHoy.Text = (dashboard.TotalComprasHoy + dashboard.TotalVentasHoy).ToString();
                        txtSaldoUSD.Text = $"${dashboard.SaldosCaja.Where(s => s.Moneda == "USD").Sum(s => s.Saldo):N2}";
                        txtSaldoARS.Text = $"${dashboard.SaldosCaja.Where(s => s.Moneda == "ARS").Sum(s => s.Saldo):N2}";
                        PoblarGraficoSaldos(dashboard);
                        PoblarGraficoPie(dashboard);
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
        catch (Exception ex) { Console.WriteLine($"Error cargando operaciones: {ex.Message}"); }
    }

    private void PoblarGraficoSaldos(CasaCambio.Shared.DTOs.DashboardDto dashboard)
    {
        pltBalanceEvolution.Plot.Clear();
        var monedas = dashboard.SaldosCaja
            .Where(s => s.Saldo != 0)
            .OrderByDescending(s => Math.Abs(s.Saldo))
            .Take(8).ToList();
        if (!monedas.Any()) return;

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
        pltBalanceEvolution.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#14232b");
        pltBalanceEvolution.Plot.DataBackground.Color   = ScottPlot.Color.FromHex("#0c151a");
        pltBalanceEvolution.Plot.Axes.Color(ScottPlot.Color.FromHex("#94a3b8"));
        pltBalanceEvolution.Refresh();
    }

    private void PoblarGraficoPie(CasaCambio.Shared.DTOs.DashboardDto dashboard)
    {
        pltCurrencyDistribution.Plot.Clear();
        var positivos = dashboard.SaldosCaja
            .Where(s => s.Saldo > 0)
            .OrderByDescending(s => s.Saldo)
            .Take(6).ToList();
        if (!positivos.Any()) return;

        var pie = pltCurrencyDistribution.Plot.Add.Pie(
            positivos.Select(s => (double)s.Saldo).ToArray());
        var colores = new[] { "#13a4ec", "#0d79b3", "#1e3441", "#22c55e", "#eab308", "#94a3b8" };
        for (int i = 0; i < pie.Slices.Count; i++)
        {
            pie.Slices[i].Label     = positivos[i].Moneda;
            pie.Slices[i].FillColor = ScottPlot.Color.FromHex(colores[i % colores.Length]);
        }
        pltCurrencyDistribution.Plot.ShowLegend();
        pltCurrencyDistribution.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#14232b");
        pltCurrencyDistribution.Plot.DataBackground.Color   = ScottPlot.Color.FromHex("#0c151a");
        pltCurrencyDistribution.Plot.Axes.Color(ScottPlot.Color.FromHex("#94a3b8"));
        pltCurrencyDistribution.Refresh();
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

    private async Task<bool> MostrarConfirmacionEnUI(string titulo, string mensaje)
    {
        var dialog = new Window { Title = titulo, Width = 450, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161b22")) };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3")) });
        var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
        bool resultado = false;
        var btnSi = new Button { Content = "Si, eliminar cuenta", Width = 160, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#da3633")), Foreground = Avalonia.Media.Brushes.White };
        var btnNo = new Button { Content = "Cancelar", Width = 100, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, Background = Avalonia.Media.Brushes.Transparent, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3")) };
        btnSi.Click += (s, ev) => { resultado = true; dialog.Close(); };
        btnNo.Click += (s, ev) => { resultado = false; dialog.Close(); };
        btnPanel.Children.Add(btnSi); btnPanel.Children.Add(btnNo); panel.Children.Add(btnPanel); dialog.Content = panel;
        await dialog.ShowDialog(this);
        return resultado;
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
}
