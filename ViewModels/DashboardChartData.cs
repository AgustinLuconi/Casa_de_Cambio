using System;
using System.Collections.Generic;

namespace SistemaCambio.ViewModels
{
    /// <summary>
    /// Datos para gráficos del dashboard
    /// </summary>
    public class DashboardChartData
    {
        public List<OperacionPorDia> OperacionesDiarias { get; set; } = new();
        public List<OperacionPorMoneda> OperacionesPorMoneda { get; set; } = new();
        public List<ComparativoMensual> ComparativoMeses { get; set; } = new();
    }

    public class OperacionPorDia
    {
        public DateTime Fecha { get; set; }
        public int CantidadCompras { get; set; }
        public int CantidadVentas { get; set; }
        public decimal MontoCompras { get; set; }
        public decimal MontoVentas { get; set; }
    }

    public class OperacionPorMoneda
    {
        public string Moneda { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal MontoTotal { get; set; }
    }

    public class ComparativoMensual
    {
        public string Mes { get; set; } = "";
        public decimal TotalCompras { get; set; }
        public decimal TotalVentas { get; set; }
        public decimal Ganancia { get; set; }
    }
}
