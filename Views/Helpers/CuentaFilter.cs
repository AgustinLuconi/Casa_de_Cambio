using System.Linq;
using CasaCambio.Shared.DTOs;

namespace SistemaCambio.Views.Helpers;

public static class CuentaFilter
{
    public static bool PuedeOperarEnMoneda(CuentaDto cuenta, string moneda)
    {
        if (cuenta.Saldos.Any(s => s.Moneda == moneda)) return true;
        if (cuenta.Tipo == "Cliente") return true;
        if (cuenta.Tipo == "Proveedor") return true;
        return false;
    }

    public static string DescribirSaldo(CuentaDto cuenta, string moneda)
    {
        var saldo = cuenta.Saldos.FirstOrDefault(s => s.Moneda == moneda);
        if (saldo != null)
        {
            if (cuenta.Tipo == "Cliente" && saldo.LimiteDeudaPersonalizado > 0)
                return $"Saldo: {saldo.Saldo:N2} {moneda} — límite de deuda: {saldo.LimiteDeudaPersonalizado:N2}";
            return $"Saldo: {saldo.Saldo:N2} {moneda}";
        }

        if (cuenta.Tipo == "Cliente")
        {
            // Legacy: límite escalar de la cuenta (cuentas pre-refactor)
            if (cuenta.LimiteDeuda.HasValue && cuenta.LimiteDeuda.Value > 0)
                return $"Sin saldo en {moneda} — puede tomar deuda hasta {cuenta.LimiteDeuda.Value:N2}";
            return $"Sin saldo en {moneda} — usa límite general de la divisa";
        }

        return $"Sin saldo en {moneda}";
    }
}
