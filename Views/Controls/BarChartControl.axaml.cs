using Avalonia.Controls;
using ScottPlot;
using SistemaCambio.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views.Controls
{
    public partial class BarChartControl : UserControl
    {
        public BarChartControl()
        {
            InitializeComponent();
            ConfigurarTemaOscuro();
        }

        private void ConfigurarTemaOscuro()
        {
            var plot = plotView.Plot;
            
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0d1117");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#0d1117");
            plot.Axes.Color(ScottPlot.Color.FromHex("#8b949e"));
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#30363d");
        }

        public void CargarDatos(List<OperacionPorMoneda> datos, string titulo = "Operaciones por Moneda")
        {
            txtTitulo.Text = titulo;

            var plot = plotView.Plot;
            plot.Clear();
            ConfigurarTemaOscuro();

            if (datos == null || !datos.Any())
            {
                MostrarMensajeSinDatos();
                return;
            }

            // Colores para las barras
            var colores = new[]
            {
                ScottPlot.Color.FromHex("#238636"), // Verde
                ScottPlot.Color.FromHex("#1f6feb"), // Azul
                ScottPlot.Color.FromHex("#a371f7"), // Morado
                ScottPlot.Color.FromHex("#f0883e"), // Naranja
                ScottPlot.Color.FromHex("#da3633")  // Rojo
            };

            // Crear barras
            var bars = new List<ScottPlot.Bar>();
            for (int i = 0; i < datos.Count; i++)
            {
                bars.Add(new ScottPlot.Bar
                {
                    Position = i,
                    Value = datos[i].Cantidad,
                    FillColor = colores[i % colores.Length]
                });
            }

            var barPlot = plot.Add.Bars(bars.ToArray());

            // Configurar etiquetas del eje X
            var ticks = datos.Select((d, i) => new Tick(i, d.Moneda)).ToArray();
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            plot.Axes.Bottom.MajorTickStyle.Length = 0;

            plot.Axes.AutoScale();
            plot.Axes.SetLimitsY(0, datos.Max(d => d.Cantidad) * 1.2);

            plotView.Refresh();
        }

        public void CargarComparativoMensual(List<ComparativoMensual> datos)
        {
            txtTitulo.Text = "Últimos 6 Meses";

            var plot = plotView.Plot;
            plot.Clear();
            ConfigurarTemaOscuro();

            if (datos == null || !datos.Any())
            {
                MostrarMensajeSinDatos();
                return;
            }

            // Crear barras agrupadas (compras y ventas)
            var bars = new List<ScottPlot.Bar>();
            
            for (int i = 0; i < datos.Count; i++)
            {
                // Barra de compras
                bars.Add(new ScottPlot.Bar
                {
                    Position = i * 2,
                    Value = (double)datos[i].TotalCompras / 1000, // En miles
                    FillColor = ScottPlot.Color.FromHex("#238636")
                });

                // Barra de ventas
                bars.Add(new ScottPlot.Bar
                {
                    Position = i * 2 + 0.8,
                    Value = (double)datos[i].TotalVentas / 1000, // En miles
                    FillColor = ScottPlot.Color.FromHex("#da3633")
                });
            }

            plot.Add.Bars(bars.ToArray());

            // Etiquetas de meses
            var ticks = datos.Select((d, i) => new Tick(i * 2 + 0.4, d.Mes)).ToArray();
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            plot.Axes.Bottom.MajorTickStyle.Length = 0;

            // Leyenda manual
            plot.Legend.IsVisible = true;
            plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#161b22");
            plot.Legend.FontColor = ScottPlot.Color.FromHex("#e6edf3");

            plot.Axes.AutoScale();
            plotView.Refresh();
        }

        private void MostrarMensajeSinDatos()
        {
            var plot = plotView.Plot;
            
            var txt = plot.Add.Text("Sin datos", 0, 0);
            txt.LabelFontSize = 14;
            txt.LabelFontColor = ScottPlot.Color.FromHex("#8b949e");
            
            plotView.Refresh();
        }
    }
}
