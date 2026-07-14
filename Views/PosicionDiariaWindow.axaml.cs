using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaCambio.Views
{
    public partial class PosicionDiariaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private ISolidColorBrush? _successBrush;
        private ISolidColorBrush? _dangerBrush;
        private ISolidColorBrush? _neutralBrush;

        public ObservableCollection<PosicionDiariaItem> Items { get; } = new();

        public PosicionDiariaWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            dpDesde.SelectedDate = new DateTimeOffset(DateTime.Today);
            dpHasta.SelectedDate = new DateTimeOffset(DateTime.Today);
            _successBrush = (ISolidColorBrush)this.FindResource("SuccessBrush")!;
            _dangerBrush = (ISolidColorBrush)this.FindResource("DangerBrush")!;
            _neutralBrush = (ISolidColorBrush)this.FindResource("PrimaryTextBrush")!;
            dgPosicion.ItemsSource = Items;

            _ = BuscarAsync();
        }

        private async void BtnBuscar_Click(object? sender, RoutedEventArgs e) => await BuscarAsync();

        private async Task BuscarAsync()
        {
            var desde = dpDesde.SelectedDate?.DateTime ?? DateTime.Today;
            var hasta = dpHasta.SelectedDate?.DateTime ?? DateTime.Today;
            if (hasta.Date < desde.Date)
            {
                NotificationService.Warning("Rango inválido", "La fecha 'Hasta' no puede ser anterior a 'Desde'.");
                return;
            }

            try
            {
                var posiciones = await _apiClient.ObtenerPosicionDiariaAsync(desde, hasta);
                Items.Clear();
                foreach (var p in posiciones)
                {
                    Items.Add(new PosicionDiariaItem
                    {
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        TipoPase = p.TipoPase,
                        CapInicial = p.CapInicial,
                        CapFinal = p.CapFinal,
                        SuccessBrush = _successBrush,
                        DangerBrush = _dangerBrush,
                        NeutralBrush = _neutralBrush
                    });
                }
                txtSinDatos.IsVisible = Items.Count == 0;
            }
            catch (Exception ex) { NotificationService.Error("Error", ex.Message); }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }

    public partial class PosicionDiariaItem : ObservableObject
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string TipoPase { get; set; } = "D";
        public decimal CapInicial { get; set; }
        public decimal CapFinal { get; set; }

        public ISolidColorBrush? SuccessBrush { get; set; }
        public ISolidColorBrush? DangerBrush { get; set; }
        public ISolidColorBrush? NeutralBrush { get; set; }

        [ObservableProperty] private string _cotInicialTexto = "0.00000";
        [ObservableProperty] private string _cotFinalTexto = "0.00000";
        [ObservableProperty] private string _usdInicialFormatted = "0.00";
        [ObservableProperty] private string _usdFinalFormatted = "0.00";
        [ObservableProperty] private string _gananciaFormatted = "0.00";
        [ObservableProperty] private ISolidColorBrush? _gananciaColor;

        partial void OnCotInicialTextoChanged(string value) => Recalcular();
        partial void OnCotFinalTextoChanged(string value) => Recalcular();

        private void Recalcular()
        {
            decimal cotInicial = MontoHelper.Parsear(CotInicialTexto);
            decimal cotFinal = MontoHelper.Parsear(CotFinalTexto);
            decimal usdInicial = ConvertirAUsd(CapInicial, cotInicial, TipoPase);
            decimal usdFinal = ConvertirAUsd(CapFinal, cotFinal, TipoPase);
            decimal ganancia = usdFinal - usdInicial;

            UsdInicialFormatted = usdInicial.ToString("N2");
            UsdFinalFormatted = usdFinal.ToString("N2");
            GananciaFormatted = ganancia.ToString("N2");
            GananciaColor = ganancia > 0 ? SuccessBrush : (ganancia < 0 ? DangerBrush : NeutralBrush);
        }

        private static decimal ConvertirAUsd(decimal capital, decimal cotizacion, string tipoPase)
        {
            if (cotizacion == 0) return 0;
            return tipoPase == "M" ? capital * cotizacion : capital / cotizacion;
        }
    }
}
