using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views
{
    public partial class PosicionDiariaWindow : Window
    {
        public PosicionDiariaWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            DataContext = new PosicionDiariaViewModel(apiClient);
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is not TextBox tb) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (MontoHelper.Parsear(tb.Text) == 0)
                    tb.Clear();
                else
                    tb.SelectAll();
            });
        }

        public void TextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
                tb.Text = "0";
        }
    }
}
