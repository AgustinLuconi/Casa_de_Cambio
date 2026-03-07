using Avalonia.Controls;
using Avalonia.Threading;
using SistemaCambio.Models;
using System.Collections.Generic;

namespace SistemaCambio.Views.Controls
{
    public partial class NotificationPanel : UserControl
    {
        private readonly List<NotificationToast> _activeNotifications = new();
        private const int MAX_VISIBLE = 5;

        public NotificationPanel()
        {
            InitializeComponent();
        }

        public void Show(NotificationMessage notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Limitar cantidad visible
                if (_activeNotifications.Count >= MAX_VISIBLE)
                {
                    // Cerrar la más antigua
                    var oldest = _activeNotifications[0];
                    RemoveNotification(oldest);
                }

                // Crear nuevo toast
                var toast = new NotificationToast();
                toast.Closed += (s, e) => RemoveNotification(toast);

                // Agregar al panel (más recientes arriba)
                stackNotifications.Children.Insert(0, toast);
                _activeNotifications.Add(toast);

                // Mostrar
                toast.Show(notification);
            });
        }

        private void RemoveNotification(NotificationToast toast)
        {
            Dispatcher.UIThread.Post(() =>
            {
                stackNotifications.Children.Remove(toast);
                _activeNotifications.Remove(toast);
            });
        }

        public void Clear()
        {
            Dispatcher.UIThread.Post(() =>
            {
                stackNotifications.Children.Clear();
                _activeNotifications.Clear();
            });
        }
    }
}
