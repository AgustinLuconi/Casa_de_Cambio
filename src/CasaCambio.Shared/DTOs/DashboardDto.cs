namespace CasaCambio.Shared.DTOs;

public class DashboardDto
{
    public int TotalOperacionesHoy { get; set; }
    public int TotalComprasHoy { get; set; }
    public int TotalVentasHoy { get; set; }
    public decimal VolumenComprasARS { get; set; }
    public decimal VolumenVentasARS { get; set; }
    public List<SaldoCuentaDto> SaldosCaja { get; set; } = new();
    public List<CotizacionDto> CotizacionesHoy { get; set; } = new();
}
