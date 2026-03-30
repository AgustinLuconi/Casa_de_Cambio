using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;

namespace SistemaCambio.Views.Helpers
{
    public static class DialogHelper
    {
        public static async Task<bool> ConfirmarAsync(Window owner, string titulo, string mensaje,
            string textoBtnConfirmar = "Continuar", string textoBtnCancelar = "Cancelar",
            bool destructivo = false)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#161b22"))
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = mensaje,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                Foreground = new SolidColorBrush(Color.Parse("#e6edf3"))
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            bool continuar = false;

            var btnConfirmar = new Button
            {
                Content = textoBtnConfirmar,
                Background = new SolidColorBrush(Color.Parse(destructivo ? "#da3633" : "#238636")),
                Foreground = new SolidColorBrush(Colors.White),
                CornerRadius = new Avalonia.CornerRadius(6)
            };

            var btnCancelar = new Button
            {
                Content = textoBtnCancelar,
                CornerRadius = new Avalonia.CornerRadius(6)
            };

            btnConfirmar.Click += (s, ev) => { continuar = true; dialog.Close(); };
            btnCancelar.Click += (s, ev) => dialog.Close();

            btnPanel.Children.Add(btnConfirmar);
            btnPanel.Children.Add(btnCancelar);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(owner);
            return continuar;
        }

        public static async Task MensajeAsync(Window owner, string titulo, string mensaje)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#161b22"))
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = mensaje,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                Foreground = new SolidColorBrush(Color.Parse("#e6edf3"))
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnOk = new Button
            {
                Content = "OK",
                Background = new SolidColorBrush(Color.Parse("#238636")),
                Foreground = new SolidColorBrush(Colors.White),
                CornerRadius = new Avalonia.CornerRadius(6),
                Width = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            btnOk.Click += (s, ev) => dialog.Close();

            btnPanel.Children.Add(btnOk);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(owner);
        }
    }
}
