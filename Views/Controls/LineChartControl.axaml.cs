using Avalonia.Controls;
using ScottPlot;
using SistemaCambio.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views.Controls
{
    public partial class LineChartControl : UserControl
    {
        public LineChartControl()
        {
            InitializeComponent();
            ConfigurarTemaOscuro();
        }

        private void ConfigurarTemaOscuro()
        {
            var plot = plotView.Plot;
            
            // Fondo oscuro
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0d1117");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#0d1117");
            
            // Ejes
            plot.Axes.Color(ScottPlot.Color.FromHex("#8b949e"));
            
            // Grid
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#30363d");
        }

        public void CargarDatos(List<OperacionPorDia> datos, string titulo = "Operaciones Diarias")
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

            // Preparar datos
            var fechas = datos.Select(d => d.Fecha.ToOADate()).ToArray();
            var compras = datos.Select(d => (double)d.CantidadCompras).ToArray();
            var ventas = datos.Select(d => (double)d.CantidadVentas).ToArray();

            // Línea de compras (verde)
            var scatterCompras = plot.Add.Scatter(fechas, compras);
            scatterCompras.LegendText = "Compras";
            scatterCompras.Color = ScottPlot.Color.FromHex("#238636");
            scatterCompras.LineWidth = 2;
            scatterCompras.MarkerSize = 6;

            // Línea de ventas (rojo)
            var scatterVentas = plot.Add.Scatter(fechas, ventas);
            scatterVentas.LegendText = "Ventas";
            scatterVentas.Color = ScottPlot.Color.FromHex("#da3633");
            scatterVentas.LineWidth = 2;
            scatterVentas.MarkerSize = 6;

            // Configurar eje X como fechas
            plot.Axes.DateTimeTicksBottom();
            
            // Leyenda
            plot.ShowLegend(Alignment.UpperRight);
            plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#161b22");
            plot.Legend.FontColor = ScottPlot.Color.FromHex("#e6edf3");
            plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#30363d");

            plot.Axes.AutoScale();
            plotView.Refresh();
        }

        private void MostrarMensajeSinDatos()
        {
            var plot = plotView.Plot;
            
            var txt = plot.Add.Text("Sin datos para mostrar", 0, 0);
            txt.LabelFontSize = 14;
            txt.LabelFontColor = ScottPlot.Color.FromHex("#8b949e");
            txt.LabelAlignment = Alignment.MiddleCenter;
            
            plotView.Refresh();
        }
    }
}
