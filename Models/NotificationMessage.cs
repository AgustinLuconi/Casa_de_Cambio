using System;

namespace SistemaCambio.Models
{
    public enum NotificationType
    {
        Success,    // Verde - Operación exitosa
        Error,      // Rojo - Error
        Warning,    // Naranja - Advertencia
        Info        // Azul - Información
    }

    public class NotificationMessage
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; } = NotificationType.Info;
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(3);
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static NotificationMessage Success(string title, string message = "", int durationSeconds = 3)
        {
            return new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Success,
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };
        }

        public static NotificationMessage Error(string title, string message = "", int durationSeconds = 7)
        {
            return new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Error,
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };
        }

        public static NotificationMessage Warning(string title, string message = "", int durationSeconds = 5)
        {
            return new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Warning,
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };
        }

        public static NotificationMessage Info(string title, string message = "", int durationSeconds = 3)
        {
            return new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Info,
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };
        }
    }
}
