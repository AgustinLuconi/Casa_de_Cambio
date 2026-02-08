using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SistemaCambio.Models;
using SistemaCambio.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SistemaCambio.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        // Suscribirse al evento del ViewModel
        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                _viewModel = vm;
                _viewModel.SolicitarAbrirVentana += AbrirVentanaPorNombre;
            }
        };
    }

    private async void AbrirVentanaPorNombre(string nombreVentana)
    {
        switch (nombreVentana)
        {
            case "Compra":
                await AbrirCompraWindow();
                break;
            case "Venta":
                await AbrirVentaWindow();
                break;
            case "CreditoDebito":
                await AbrirCreditoDebitoWindow();
                break;
            case "Arqueo":
                await AbrirArqueoWindow();
                break;
            case "Movimientos":
                await AbrirMovimientosWindow();
                break;
            case "NuevaCuenta":
                await AbrirNuevaCuentaWindow();
                break;
            case "Reportes":
                await AbrirReportesWindow();
                break;
            case "Configuracion":
                await AbrirConfiguracionWindow();
                break;
        }
    }

    // Handlers del Sidebar
    private void BtnDashboard_Click(object? sender, RoutedEventArgs e)
    {
        // Mostrar Dashboard y ocultar Cuentas
        dashboardPanel.IsVisible = true;
        cuentasPanel.IsVisible = false;
        txtMainTitle.Text = "Dashboard";
        
        // Actualizar estados de botones
        btnDashboard.Classes.Set("SidebarButtonActive", true);
        btnDashboard.Classes.Set("SidebarButton", false);
        btnCuentas.Classes.Set("SidebarButtonActive", false);
        btnCuentas.Classes.Set("SidebarButton", true);
        
        CargarEstadisticas();
    }

    private void BtnCuentas_Click(object? sender, RoutedEventArgs e)
    {
        // Mostrar Cuentas y ocultar Dashboard
        dashboardPanel.IsVisible = false;
        cuentasPanel.IsVisible = true;
        txtMainTitle.Text = "Cuentas Overview";
        
        // Actualizar estados de botones
        btnCuentas.Classes.Set("SidebarButtonActive", true);
        btnCuentas.Classes.Set("SidebarButton", false);
        btnDashboard.Classes.Set("SidebarButtonActive", false);
        btnDashboard.Classes.Set("SidebarButton", true);
        
        _viewModel?.RefrescarDatos();
    }

    private void CargarEstadisticas()
    {
        using var db = new AppDbContext();
        
        // Total de cuentas
        txtTotalCuentas.Text = db.Cuentas.Count().ToString();
        
        // Operaciones de hoy
        var hoy = DateTime.Today;
        var operacionesHoy = db.Operaciones.ToList().Count(o => o.Fecha.Date == hoy);
        txtOperacionesHoy.Text = operacionesHoy.ToString();
        
        // Saldo USD
        var saldoUSD = db.Cuentas.Where(c => c.Moneda == "USD").Sum(c => c.Saldo);
        txtSaldoUSD.Text = $"${saldoUSD:N2}";
        
        // Saldo ARS
        var saldoARS = db.Cuentas.Where(c => c.Moneda == "ARS").Sum(c => c.Saldo);
        txtSaldoARS.Text = $"${saldoARS:N2}";
        
        // Últimas operaciones
        var ultimasOps = db.Operaciones.OrderByDescending(o => o.Fecha).Take(10).ToList();
        dgUltimasOperaciones.ItemsSource = ultimasOps;
    }

    private async void BtnCompra_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirCompraWindow();
    }

    private async void BtnVenta_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirVentaWindow();
    }

    private async void BtnCreditoDebito_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirCreditoDebitoWindow();
    }

    private async void BtnArqueo_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirArqueoWindow();
    }

    private async void BtnMovimientos_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirMovimientosWindow();
    }

    private async void BtnReportes_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirReportesWindow();
    }

    private async void BtnConfiguracion_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirConfiguracionWindow();
    }

    // Header buttons
    private async void BtnNuevaCuenta_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirNuevaCuentaWindow();
    }

    private async void BtnExportar_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Cuentas == null || !_viewModel.Cuentas.Any()) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar Cuentas",
            SuggestedFileName = $"cuentas_{DateTime.Now:yyyyMMdd}.csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });

        if (file != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID,Cuenta,Tipo,Moneda,Saldo");
            
            foreach (var c in _viewModel.Cuentas)
            {
                sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},{c.Moneda},{c.Saldo}");
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(sb.ToString());
        }
    }

    // Métodos para abrir ventanas
    private async System.Threading.Tasks.Task AbrirCompraWindow()
    {
        var compraWindow = new CompraWindow();
        await compraWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
    }

    private async System.Threading.Tasks.Task AbrirVentaWindow()
    {
        var ventaWindow = new VentaWindow();
        await ventaWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
    }

    private async System.Threading.Tasks.Task AbrirCreditoDebitoWindow()
    {
        var creditoDebitoWindow = new CreditoDebitoWindow();
        await creditoDebitoWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
    }

    private async System.Threading.Tasks.Task AbrirArqueoWindow()
    {
        var arqueoWindow = new ArqueoWindow();
        await arqueoWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
    }

    private async System.Threading.Tasks.Task AbrirMovimientosWindow()
    {
        var movimientosWindow = new DetalleMovimientosWindow();
        await movimientosWindow.ShowDialog(this);
    }

    private async System.Threading.Tasks.Task AbrirNuevaCuentaWindow()
    {
        var nuevaCuentaWindow = new NuevaCuentaWindow();
        await nuevaCuentaWindow.ShowDialog(this);
        _viewModel?.RefrescarDatos();
    }

    private async System.Threading.Tasks.Task AbrirReportesWindow()
    {
        var reportesWindow = new ReportesWindow();
        await reportesWindow.ShowDialog(this);
    }

    private async System.Threading.Tasks.Task AbrirConfiguracionWindow()
    {
        var configWindow = new ConfiguracionWindow();
        await configWindow.ShowDialog(this);
    }
}