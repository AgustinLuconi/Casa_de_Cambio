using System;

namespace SistemaCambio.Services
{
    public static class AppConstants
    {
        public const decimal MontoAltoARS = 5_000_000m;
        public const decimal CotizacionDiffPctUmbral = 5m;
        public static readonly TimeSpan IntervaloVerificacionConectividad = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan IntervaloSincronizacion = TimeSpan.FromSeconds(30);
        public const int SyncBatchSize = 10;
        public const int SyncMaxRetries = 5;
        public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);
    }
}
