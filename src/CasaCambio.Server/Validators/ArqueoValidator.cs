using CasaCambio.Server.Models;

namespace CasaCambio.Server.Validators;

public class ArqueoValidator
{
    public ValidationResult ValidarArqueo(string cuentaNombre, string moneda, decimal saldoSistema, decimal montoContado)
    {
        var result = new ValidationResult();
        if (montoContado < 0) { result.AddError("El monto contado no puede ser negativo"); return result; }
        var diferencia = montoContado - saldoSistema;
        if (Math.Abs(diferencia) == 0) { result.AddInfo("Caja cuadra perfectamente"); return result; }
        string tipoStr = diferencia > 0 ? "Sobrante" : "Faltante";
        decimal diferenciaAbs = Math.Abs(diferencia);
        if (diferenciaAbs <= 500) result.AddWarning($"Diferencia detectada: {tipoStr} ${diferenciaAbs:N2} en {cuentaNombre} ({moneda})");
        else result.AddError($"Diferencia significativa: {tipoStr} ${diferenciaAbs:N2} en {cuentaNombre} ({moneda})");
        return result;
    }
}
