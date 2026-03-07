using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SistemaCambio.Views
{
    public partial class ReportesWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ReportesWindow()
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

            InitializeComponent();
            dpDesdeOp.SelectedDate = new DateTimeOffset(DateTime.Today.AddDays(-30));
            dpHastaOp.SelectedDate = new DateTimeOffset(DateTime.Today);
        }

        private void BtnGenerarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            using var db = _contextFactory.CreateDbContext();
            
            var fechaDesde = dpDesdeOp.SelectedDate?.Date ?? DateTime.Today.AddDays(-30);
            var fechaHasta = (dpHastaOp.SelectedDate?.Date ?? DateTime.Today).AddDays(1);

            var operaciones = db.Operaciones
                .ToList()
                .Where(o => o.Fecha.Date >= fechaDesde && o.Fecha.Date < fechaHasta)
                .OrderByDescending(o => o.Fecha)
                .ToList();

            dgOperaciones.ItemsSource = operaciones;
        }

        private void BtnGenerarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            using var db = _contextFactory.CreateDbContext();
            
            var itemTipo = cmbTipoSaldos.SelectedItem as ComboBoxItem;
            string tipo = itemTipo?.Content?.ToString() ?? "Todos";

            var query = db.Cuentas.Include(c => c.Saldos).AsQueryable();
            
            if (tipo != "Todos")
            {
                query = query.Where(c => c.Tipo == tipo);
            }

            var cuentas = query.OrderBy(c => c.Nombre).ToList();
            dgSaldos.ItemsSource = cuentas;
        }

        private async void BtnExportarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            var operaciones = dgOperaciones.ItemsSource as IEnumerable<Operacion>;
            if (operaciones == null || !operaciones.Any()) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar Reporte de Operaciones",
                SuggestedFileName = $"operaciones_{DateTime.Now:yyyyMMdd}.csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
            });

            if (file != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Fecha,Tipo,MontoOrigen,MontoDestino,Cotizacion,Observaciones");
                
                foreach (var op in operaciones)
                {
                    sb.AppendLine($"{op.Id},{op.Fecha:yyyy-MM-dd HH:mm},{op.TipoOperacion},{op.MontoTotalOrigen},{op.MontoTotalDestino},{op.CotizacionAplicada},\"{op.Observaciones}\"");
                }

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
        }

        private async void BtnExportarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            var cuentas = dgSaldos.ItemsSource as IEnumerable<Cuenta>;
            if (cuentas == null || !cuentas.Any()) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar Reporte de Saldos",
                SuggestedFileName = $"saldos_{DateTime.Now:yyyyMMdd}.csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
            });

            if (file != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Cuenta,Tipo,Moneda,Saldo");
                
                foreach (var c in cuentas)
                {
                    if (c.Saldos.Any())
                    {
                        foreach (var s in c.Saldos)
                        {
                            sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},{s.Moneda},{s.Saldo}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},,0");
                    }
                }

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
