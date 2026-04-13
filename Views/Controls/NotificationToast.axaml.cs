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
            this.RenderTransform = new TranslateTransform(80, 0);
            this.Opacity = 0;
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
            const int steps = 12;
            const int delayMs = 16; // ~60fps

            for (int i = 0; i <= steps; i++)
            {
                double progress = (double)i / steps;
                double eased = 1 - Math.Pow(1 - progress, 3); // ease-out cubic

                this.Opacity = eased;
                if (this.RenderTransform is TranslateTransform t)
                    t.X = 80 * (1 - eased);

                await System.Threading.Tasks.Task.Delay(delayMs);
            }

            this.Opacity = 1;
            if (this.RenderTransform is TranslateTransform tf)
                tf.X = 0;
        }

        private async void Cerrar()
        {
            const int steps = 10;
            const int delayMs = 15;

            for (int i = steps; i >= 0; i--)
            {
                double progress = (double)i / steps;
                double eased = Math.Pow(progress, 2); // ease-in cuadratic

                this.Opacity = eased;
                if (this.RenderTransform is TranslateTransform t)
                    t.X = 80 * (1 - eased);

                await System.Threading.Tasks.Task.Delay(delayMs);
            }

            this.Opacity = 0;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Cerrar();
        }
    }
}
