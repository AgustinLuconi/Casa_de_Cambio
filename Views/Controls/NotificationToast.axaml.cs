using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Material.Icons;
using SistemaCambio.Models;
using System;

namespace SistemaCambio.Views.Controls
{
    public partial class NotificationToast : UserControl
    {
        public event EventHandler? Closed;
        private DispatcherTimer? _timer;

        public NotificationToast()
        {
            InitializeComponent();
        }

        public void Show(NotificationMessage notification)
        {
            // Configurar contenido
            txtTitle.Text = notification.Title;
            
            if (!string.IsNullOrEmpty(notification.Message))
            {
                txtMessage.Text = notification.Message;
                txtMessage.IsVisible = true;
            }

            // Configurar colores según tipo
            ConfigurarEstilo(notification.Type);

            // Animación de entrada (fade in)
            this.Opacity = 0;
            AnimarEntrada();

            // Timer para auto-cerrar
            _timer = new DispatcherTimer
            {
                Interval = notification.Duration
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                Cerrar();
            };
            _timer.Start();
        }

        private void ConfigurarEstilo(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    icon.Kind = MaterialIconKind.CheckCircle;
                    icon.Foreground = new SolidColorBrush(Color.Parse("#238636"));
                    iconBackground.Background = new SolidColorBrush(Color.Parse("#23863640"));
                    borderContainer.BorderBrush = new SolidColorBrush(Color.Parse("#238636"));
                    break;

                case NotificationType.Error:
                    icon.Kind = MaterialIconKind.CloseCircle;
                    icon.Foreground = new SolidColorBrush(Color.Parse("#da3633"));
                    iconBackground.Background = new SolidColorBrush(Color.Parse("#da363340"));
                    borderContainer.BorderBrush = new SolidColorBrush(Color.Parse("#da3633"));
                    break;

                case NotificationType.Warning:
                    icon.Kind = MaterialIconKind.Alert;
                    icon.Foreground = new SolidColorBrush(Color.Parse("#f0883e"));
                    iconBackground.Background = new SolidColorBrush(Color.Parse("#f0883e40"));
                    borderContainer.BorderBrush = new SolidColorBrush(Color.Parse("#f0883e"));
                    break;

                case NotificationType.Info:
                    icon.Kind = MaterialIconKind.Information;
                    icon.Foreground = new SolidColorBrush(Color.Parse("#1f6feb"));
                    iconBackground.Background = new SolidColorBrush(Color.Parse("#1f6feb40"));
                    borderContainer.BorderBrush = new SolidColorBrush(Color.Parse("#1f6feb"));
                    break;
            }
        }

        private async void AnimarEntrada()
        {
            // Simple fade in
            for (double i = 0; i <= 1; i += 0.1)
            {
                this.Opacity = i;
                await System.Threading.Tasks.Task.Delay(20);
            }
            this.Opacity = 1;
        }

        private async void Cerrar()
        {
            // Simple fade out
            for (double i = 1; i >= 0; i -= 0.1)
            {
                this.Opacity = i;
                await System.Threading.Tasks.Task.Delay(15);
            }
            this.Opacity = 0;

            // Notificar que se cerró
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Cerrar();
        }
    }
}
