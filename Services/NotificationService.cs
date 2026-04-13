using SistemaCambio.Models;
using SistemaCambio.Views.Controls;
using System;
using System.Collections.Generic;

namespace SistemaCambio.Services
{
    /// <summary>
    /// Servicio para mostrar notificaciones tipo toast en la aplicación.
    /// Llamar Initialize() una vez al inicio desde MainWindow.
    /// </summary>
    public static class NotificationService
    {
        private static NotificationPanel? _panel;

        private static readonly List<NotificationMessage> _historial = new();
        private const int MaxHistorial = 50;

        public static IReadOnlyList<NotificationMessage> Historial => _historial.AsReadOnly();
        public static event Action? HistorialActualizado;

        /// <summary>
        /// Inicializar con el panel de la ventana principal
        /// </summary>
        public static void Initialize(NotificationPanel panel)
        {
            _panel = panel;
        }

        /// <summary>
        /// Mostrar notificación de éxito
        /// </summary>
        public static void Success(string title, string message = "")
        {
            Show(NotificationMessage.Success(title, message));
        }

        /// <summary>
        /// Mostrar notificación de error
        /// </summary>
        public static void Error(string title, string message = "")
        {
            Show(NotificationMessage.Error(title, message));
        }

        /// <summary>
        /// Mostrar notificación de advertencia
        /// </summary>
        public static void Warning(string title, string message = "")
        {
            Show(NotificationMessage.Warning(title, message));
        }

        /// <summary>
        /// Mostrar notificación de información
        /// </summary>
        public static void Info(string title, string message = "")
        {
            Show(NotificationMessage.Info(title, message));
        }

        /// <summary>
        /// Mostrar notificación personalizada
        /// </summary>
        public static void Show(NotificationMessage notification)
        {
            _historial.Insert(0, notification);
            if (_historial.Count > MaxHistorial)
                _historial.RemoveAt(_historial.Count - 1);
            HistorialActualizado?.Invoke();

            if (_panel == null)
            {
                Console.WriteLine($"[Notification] {notification.Type}: {notification.Title}");
                return;
            }

            _panel.Show(notification);
        }

        /// <summary>
        /// Limpiar todas las notificaciones visibles
        /// </summary>
        public static void Clear()
        {
            _panel?.Clear();
        }

        public static void LimpiarHistorial()
        {
            _historial.Clear();
            HistorialActualizado?.Invoke();
        }

        // ============ Métodos convenientes para casos comunes ============

        public static void OperacionGuardada(string tipoOperacion, int operacionId)
        {
            Success(
                $"{tipoOperacion} registrada",
                $"Operación #{operacionId}"
            );
        }

        public static void ArqueoCompletado(decimal diferencia)
        {
            if (diferencia == 0)
            {
                Success("Arqueo completado", "Caja cuadra perfectamente");
            }
            else if (Math.Abs(diferencia) <= 100)
            {
                Warning("Arqueo completado", $"Diferencia menor: ${diferencia:N2}");
            }
            else
            {
                Warning("Arqueo completado", $"⚠️ Diferencia: ${diferencia:N2}");
            }
        }

        public static void SaldoInsuficiente(string nombreCuenta, decimal disponible, decimal requerido)
        {
            Error(
                "Saldo insuficiente",
                $"{nombreCuenta}: Disponible ${disponible:N2}"
            );
        }

        public static void DatosIncompletos()
        {
            Warning("Datos incompletos", "Complete todos los campos requeridos");
        }

        public static void CierreCajaCompletado()
        {
            Success("Cierre de caja", "Día cerrado exitosamente");
        }

        public static void ExportacionCompletada(string nombreArchivo)
        {
            Success("Exportación completada", nombreArchivo);
        }
    }
}
