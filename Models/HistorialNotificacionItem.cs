using System;
using Avalonia.Media;

namespace SistemaCambio.Models
{
    public class HistorialNotificacionItem
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public NotificationType Type { get; set; }
        public bool TieneMessage => !string.IsNullOrEmpty(Message);

        public IBrush ColorIndicador => Type switch
        {
            NotificationType.Success => new SolidColorBrush(Color.Parse("#16A34A")),
            NotificationType.Error   => new SolidColorBrush(Color.Parse("#DC2626")),
            NotificationType.Warning => new SolidColorBrush(Color.Parse("#D97706")),
            NotificationType.Info    => new SolidColorBrush(Color.Parse("#3B82F6")),
            _                        => new SolidColorBrush(Color.Parse("#64748B"))
        };
    }
}
