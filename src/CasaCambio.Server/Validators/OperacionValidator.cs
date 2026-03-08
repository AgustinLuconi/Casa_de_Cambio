using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;

namespace CasaCambio.Server.Validators;

public class OperacionValidator
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public OperacionValidator(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    public ValidationResult ValidarOperacion(string tipoOperacion, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion)
    {
        var result = new ValidationResult();
        using var db = _contextFactory.CreateDbContext();
        var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
        var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
        if (cuentaOrigen == null) { result.AddError("Cuenta origen no existe", $"ID: {cuentaOrigenId}"); return result; }
        if (cuentaDestino == null) { result.AddError("Cuenta destino no existe", $"ID: {cuentaDestinoId}"); return result; }
        if (montoOrigen <= 0) result.AddError("Monto origen debe ser mayor a cero");
        if (montoDestino <= 0) result.AddError("Monto destino debe ser mayor a cero");
        if (cotizacion <= 0) result.AddError("Cotizacion debe ser mayor a cero");
        if (cuentaOrigenId == cuentaDestinoId && monedaOrigen == monedaDestino) result.AddError("No se puede operar la misma moneda en la misma cuenta");
        var saldoOrigen = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaOrigenId && s.Moneda == monedaOrigen);
        decimal saldoDisponible = saldoOrigen?.Saldo ?? 0;
        if (saldoDisponible < montoOrigen) result.AddError($"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen})", $"Disponible: {saldoDisponible:N2} | Requerido: {montoOrigen:N2}");
        return result;
    }

    public ValidationResult ValidarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito)
    {
        var result = new ValidationResult();
        using var db = _contextFactory.CreateDbContext();
        var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
        var cuentaDebito = db.Cuentas.Find(cuentaDebitoId);
        if (cuentaCredito == null) { result.AddError("Cuenta credito no existe"); return result; }
        if (cuentaDebito == null) { result.AddError("Cuenta debito no existe"); return result; }
        if (montoCredito <= 0) result.AddError("Monto credito debe ser mayor a cero");
        if (montoDebito <= 0) result.AddError("Monto debito debe ser mayor a cero");
        if (cuentaCreditoId == cuentaDebitoId && monedaCredito == monedaDebito) result.AddError("No se puede hacer credito/debito en la misma cuenta y moneda");
        var saldoDebito = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaDebitoId && s.Moneda == monedaDebito);
        if ((saldoDebito?.Saldo ?? 0) < montoDebito) result.AddError($"Saldo insuficiente en '{cuentaDebito.Nombre}' ({monedaDebito})");
        return result;
    }

    public ValidationResult ValidarOperacionInterbancaria(int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino)
    {
        var result = new ValidationResult();
        using var db = _contextFactory.CreateDbContext();
        var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
        var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
        if (cuentaOrigen == null) result.AddError("Cuenta origen no existe");
        if (cuentaDestino == null) result.AddError("Cuenta destino no existe");
        if (result.HasErrors) return result;
        if (montoOrigen <= 0) result.AddError("Monto origen debe ser mayor a cero");
        if (montoDestino <= 0) result.AddError("Monto destino debe ser mayor a cero");
        if (cuentaOrigenId == cuentaDestinoId) result.AddError("La cuenta origen y destino no pueden ser la misma");
        if (monedaOrigen == monedaDestino) result.AddError("Arbitraje requiere monedas diferentes");
        var saldoOrigen = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaOrigenId && s.Moneda == monedaOrigen);
        if ((saldoOrigen?.Saldo ?? 0) < montoOrigen) result.AddError($"Saldo insuficiente en '{cuentaOrigen!.Nombre}' ({monedaOrigen})");
        var saldoDestino = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaDestinoId && s.Moneda == monedaDestino);
        if ((saldoDestino?.Saldo ?? 0) < montoDestino) result.AddError($"Saldo insuficiente en '{cuentaDestino!.Nombre}' ({monedaDestino})");
        return result;
    }
}
