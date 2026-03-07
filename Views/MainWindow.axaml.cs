using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
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
    private readonly IDashboardService _dashboardService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MainWindow()
    {
        _dashboardService = App.Services.GetRequiredService<IDashboardService>();
        _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        InitializeComponent();
        
        // Inicializar servicio de notificaciones
        Services.NotificationService.Initialize(notificationPanel);
        
        // Suscribirse al evento del ViewModel
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
            }
        };
    }

    private async void AbrirVentanaPorNombre(string nombreVentana)
    {
        switch (nombreVentana)
        {
            case "Dashboard":
                BtnDashboard_Click(null, null);
                break;
            case "Cuentas":
                BtnCuentas_Click(null, null);
                break;
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
        dashboardPanel.IsVisible = true;
        cuentasPanel.IsVisible = false;
        txtMainTitle.Text = "Dashboard";
        
        btnDashboard.Classes.Set("SidebarButtonActive", true);
        btnDashboard.Classes.Set("SidebarButton", false);
        btnCuentas.Classes.Set("SidebarButtonActive", false);
        btnCuentas.Classes.Set("SidebarButton", true);
        
        CargarEstadisticas();
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

    private async void CargarEstadisticas()
    {
        try
        {
            using var db = _contextFactory.CreateDbContext();
            
            var totalCuentas = await db.Cuentas.CountAsync();
            txtTotalCuentas.Text = totalCuentas.ToString();
            
            var operacionesHoy = db.Operaciones
                .ToList()
                .Count(o => o.Fecha.Date == DateTime.UtcNow.Date);
            txtOperacionesHoy.Text = operacionesHoy.ToString();
            
            var saldoUSD = await db.SaldosCuenta
                .Where(s => s.Moneda == "USD")
                .SumAsync(s => s.Saldo);
            txtSaldoUSD.Text = $"${saldoUSD:N2}";
            
            var saldoARS = await db.SaldosCuenta
                .Where(s => s.Moneda == "ARS")
                .SumAsync(s => s.Saldo);
            txtSaldoARS.Text = $"${saldoARS:N2}";
            
            var ultimasOps = await db.Operaciones
                .Include(o => o.Cliente)
                .OrderByDescending(o => o.Fecha)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();
            dgUltimasOperaciones.ItemsSource = ultimasOps;

            await CargarGraficosAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cargando estadísticas: {ex.Message}");
        }
    }

    private async Task CargarGraficosAsync()
    {
        try
        {
            // Default: last 30 days
            var fechaDesde = DateTime.UtcNow.Date.AddDays(-30);
            var fechaHasta = DateTime.UtcNow.Date.AddDays(1);

            var operacionesDiarias = await _dashboardService.ObtenerOperacionesDiariasAsync(fechaDesde, fechaHasta);
            var porMoneda = await _dashboardService.ObtenerOperacionesPorMonedaAsync(fechaDesde, fechaHasta);

            // 1. Plot Balance Evolution (Line Chart for Compras and Ventas)
            var pltEvo = pltBalanceEvolution.Plot;
            pltEvo.Clear();
            pltEvo.FigureBackground.Color = ScottPlot.Color.FromHex("#141924");
            pltEvo.DataBackground.Color = ScottPlot.Color.FromHex("#141924");
            pltEvo.Axes.Color(ScottPlot.Color.FromHex("#B0B5C9"));

            if (operacionesDiarias.Any())
            {
                double[] xs = operacionesDiarias.Select(o => o.Fecha.ToOADate()).ToArray();
                double[] comprasY = operacionesDiarias.Select(o => (double)o.MontoCompras).ToArray();
                double[] ventasY = operacionesDiarias.Select(o => (double)o.MontoVentas).ToArray();

                var comprasLine = pltEvo.Add.Scatter(xs, comprasY);
                comprasLine.LegendText = "Compras";
                comprasLine.Color = ScottPlot.Color.FromHex("#4ade80");
                comprasLine.LineWidth = 3;
                comprasLine.MarkerSize = 6;

                var ventasLine = pltEvo.Add.Scatter(xs, ventasY);
                ventasLine.LegendText = "Ventas";
                ventasLine.Color = ScottPlot.Color.FromHex("#13a4ec");
                ventasLine.LineWidth = 3;
                ventasLine.MarkerSize = 6;

                pltEvo.Axes.DateTimeTicksBottom();
                pltEvo.Legend.IsVisible = true;
                pltEvo.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1AFFFFFF");
                pltEvo.Legend.FontColor = ScottPlot.Color.FromHex("#FFFFFF");
                pltEvo.Legend.OutlineColor = ScottPlot.Color.FromHex("#33FFFFFF");
            }
            pltBalanceEvolution.Refresh();

            // 2. Plot Currency Distribution (Pie Chart)
            var pltPie = pltCurrencyDistribution.Plot;
            pltPie.Clear();
            pltPie.FigureBackground.Color = ScottPlot.Color.FromHex("#141924");
            pltPie.DataBackground.Color = ScottPlot.Color.FromHex("#141924");
            
            var validSlices = porMoneda.Where(x => x.MontoTotal > 0).ToList();
            if (validSlices.Any())
            {
                var slices = new List<ScottPlot.PieSlice>();
                var palette = new[] { "#13a4ec", "#4ade80", "#f59e0b", "#ec4899", "#8b5cf6" };
                
                for (int i = 0; i < validSlices.Count; i++)
                {
                    slices.Add(new ScottPlot.PieSlice()
                    {
                        Value = (double)validSlices[i].MontoTotal,
                        Label = validSlices[i].Moneda,
                        FillColor = ScottPlot.Color.FromHex(palette[i % palette.Length])
                    });
                }
                
                var pie = pltPie.Add.Pie(slices);
                pie.ExplodeFraction = 0.05;
                
                pltPie.Legend.IsVisible = true;
                pltPie.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1AFFFFFF");
                pltPie.Legend.FontColor = ScottPlot.Color.FromHex("#FFFFFF");
                pltPie.Legend.OutlineColor = ScottPlot.Color.FromHex("#33FFFFFF");
            }
            pltCurrencyDistribution.Refresh();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cargando gráficos: {ex.Message}");
        }
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

    private async void BtnCierreCaja_Click(object? sender, RoutedEventArgs e)
    {
        var cierreWindow = new CierreCajaWindow();
        await cierreWindow.ShowDialog(this);
        CargarEstadisticas();
    }

    private async void BtnReportes_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirReportesWindow();
    }

    private async void BtnConfiguracion_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirConfiguracionWindow();
    }

    private async void BtnNuevaCuenta_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirNuevaCuentaWindow();
    }

    private async Task AbrirNuevaCuentaWindow()
    {
        var nuevaCuentaWindow = new NuevaCuentaWindow();
        await nuevaCuentaWindow.ShowDialog(this);
        
        _viewModel?.RefrescarDatos();
    }

    private async Task AbrirEdicionCuentaWindow(int cuentaId)
    {
        var editarCuentaWindow = new NuevaCuentaWindow(cuentaId);
        await editarCuentaWindow.ShowDialog(this);
        
        _viewModel?.RefrescarDatos();
    }

    private async Task AbrirDetalleCuentaWindow(int cuentaId)
    {
        var detalleCuentaWindow = new DetalleCuentaWindow(cuentaId);
        await detalleCuentaWindow.ShowDialog(this);
    }

    private void MostrarMensajeEnUI(string titulo, string mensaje)
    {
        if (titulo == "Error")
            Services.NotificationService.Error(titulo, mensaje);
        else
            Services.NotificationService.Success(titulo, mensaje);
    }

    private async Task<bool> MostrarConfirmacionEnUI(string titulo, string mensaje)
    {
        var dialog = new Window
        {
            Title = titulo,
            Width = 450,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161b22"))
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock 
        { 
            Text = mensaje, 
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3"))
        });

        var btnPanel = new StackPanel 
        { 
            Orientation = Avalonia.Layout.Orientation.Horizontal, 
            Spacing = 10, 
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        bool resultado = false;
        var btnSi = new Avalonia.Controls.Button 
        { 
            Content = "Sí, eliminar cuenta", 
            Width = 160,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#da3633")),
            Foreground = Avalonia.Media.Brushes.White
        };
        var btnNo = new Avalonia.Controls.Button 
        { 
            Content = "Cancelar", 
            Width = 100,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3"))
        };

        btnSi.Click += (s, ev) => { resultado = true; dialog.Close(); };
        btnNo.Click += (s, ev) => { resultado = false; dialog.Close(); };

        btnPanel.Children.Add(btnSi);
        btnPanel.Children.Add(btnNo);
        panel.Children.Add(btnPanel);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        return resultado;
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
            
            using var csvDb = _contextFactory.CreateDbContext();
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