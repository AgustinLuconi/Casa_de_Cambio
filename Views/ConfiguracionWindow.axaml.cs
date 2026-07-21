using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;
using CasaCambio.Shared.DTOs;
using System;

namespace SistemaCambio.Views
{
    public partial class ConfiguracionWindow : Window
    {
        private readonly ConfiguracionViewModel _viewModel;

        public ConfiguracionWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            dpFechaCotizacion.SelectedDate = new DateTimeOffset(DateTime.Today);

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var dialogService = new WindowDialogService(this);
            _viewModel = new ConfiguracionViewModel(apiClient, dialogService);
            DataContext = _viewModel;
        }

        private bool _isDarkMode = true;

        private void ToggleTema_Click(object? sender, PointerPressedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

            if (_isDarkMode)
            {
                toggleTemaKnob.HorizontalAlignment = HorizontalAlignment.Left;
                toggleTemaKnob.Margin = new Thickness(3, 0, 0, 0);
                iconTema.Kind = MaterialIconKind.WeatherNight;
                iconTema.Foreground = Avalonia.Media.Brush.Parse("#1a6fa8");
            }
            else
            {
                toggleTemaKnob.HorizontalAlignment = HorizontalAlignment.Right;
                toggleTemaKnob.Margin = new Thickness(0, 0, 3, 0);
                iconTema.Kind = MaterialIconKind.WeatherSunny;
                iconTema.Foreground = Avalonia.Media.Brush.Parse("#d97706");
            }
        }

        private async void BtnEliminarMoneda_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is MonedaDto moneda)
                await _viewModel.EliminarMonedaAsync(moneda);
        }

        private void BtnCerrar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
    }
}
