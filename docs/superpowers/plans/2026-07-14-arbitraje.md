# Compra/Venta (Arbitraje) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nueva pantalla "Compra/Venta" (Arbitraje) que ejecuta una Compra y una Venta de monedas extranjeras en una sola operación atómica, donde el monto en Pesos de ambas patas debe coincidir exactamente (se cancelan entre sí — el único efecto neto real es el cambio de posición en moneda extranjera).

**Architecture:** Nuevo método `OperacionService.GuardarArbitraje(...)` que crea 2 filas `Operacion` (TipoOperacion="Compra" y "Venta") en una única transacción de base de datos, vinculadas por un nuevo campo `Operacion.OperacionParejaId`. `AnularOperacion` se extiende para anular ambas patas en cascada. En el desktop, primera pantalla de operación con `ViewModel` dedicado (CommunityToolkit.Mvvm) en vez del patrón code-behind que usan Compra/Venta/Crédito-Débito existentes — decisión explícita del usuario para esta pantalla.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core + Npgsql, Avalonia UI, CommunityToolkit.Mvvm.

## Global Constraints

- Desktop (`SistemaCambio.csproj`) NO tiene `ImplicitUsings` habilitado — agregar `using System;` etc. explícitos en cualquier archivo nuevo del desktop. `ViewModels/` SÍ puede usar CommunityToolkit.Mvvm libremente (ya es dependencia del proyecto).
- Server y Shared SÍ tienen implicit usings.
- Los `ViewModel` nunca referencian tipos de Avalonia (`CasaCambio.Shared.DTOs`/tipos planos únicamente) — la configuración de `AutoCompleteBox` (vía `CuentaAutoComplete.Configurar`) vive en el code-behind de la vista, no en el ViewModel.
- Todos los montos se redondean con `Math.Round(..., 2, MidpointRounding.AwayFromZero)`; cotizaciones con `Math.Round(..., 5, MidpointRounding.AwayFromZero)` — mismo criterio que el resto del sistema.
- La cuenta ARS pivote (`CuentaPesosId`) NO tiene selector visible en la UI (no aparece en la captura de referencia) — se resuelve automáticamente igual que `CuentaAutoComplete.PrimeraCajaEfectivo(cuentas, "ARS", tags)` ya usa `CompraWindow`.
- **Hallazgo de esta planificación (no estaba en el diseño aprobado):** dado que `PesosCompra == PesosVenta` es una restricción exigida, el efecto neto sobre la cuenta ARS pivote es SIEMPRE cero (se debita y se acredita el mismo monto). Por lo tanto, la cuenta ARS pivote **no necesita** chequeo de saldo/límite de deuda — solo la cuenta que entrega moneda extranjera en la Venta (`CuentaDebitaVentaId`) necesita ese chequeo, porque es la única salida real de valor que no se cancela en el mismo momento contable.
- Antes de aplicar cualquier migración EF a Supabase: generar con `dotnet ef migrations add`, LEER el archivo generado, verificar que no tenga ningún `defaultValue` incorrecto (ya pasó dos veces esta sesión), aplicar el SQL directamente contra Supabase vía MCP `execute_sql` (project_id `vtyaunxljytbxbgyhmaz`) y registrar la migración en `__EFMigrationsHistory` (mismo mecanismo ya usado en esta sesión — `dotnet ef database update` no tiene conexión local funcional).
- Todo commit debe pasar `dotnet build Sistema_Casa_Cambio.sln` (0 errores) y `dotnet test` en ambos proyectos de test antes de darse por terminado.

---

### Task 1: Campo OperacionParejaId + migración

**Files:**
- Modify: `src/CasaCambio.Server/Models/Operacion.cs`
- Modify: `src/CasaCambio.Server/Data/AppDbContext.cs`
- Create: `src/CasaCambio.Server/Migrations/<timestamp>_AgregarOperacionParejaId.cs` (generado por EF)

**Interfaces:**
- Produce: `Operacion.OperacionParejaId` (int?, FK a sí misma), disponible para Task 2 y Task 3.

- [ ] **Step 1: Agregar el campo al modelo**

En `src/CasaCambio.Server/Models/Operacion.cs`, agregar después de `OperacionOriginal`:

```csharp
[Column("operacion_pareja_id")] public int? OperacionParejaId { get; set; }
[ForeignKey("OperacionParejaId")] public Operacion? OperacionPareja { get; set; }
```

El archivo completo debe quedar así:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("operaciones")]
public class Operacion
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("fecha")] public DateTime Fecha { get; set; } = DateTime.Now;
    [Column("tipo_operacion")] public string TipoOperacion { get; set; } = "Compra";
    [Column("monto_total_origen")] public decimal MontoTotalOrigen { get; set; }
    [Column("monto_total_destino")] public decimal MontoTotalDestino { get; set; }
    [Column("cotizacion_aplicada")] public decimal CotizacionAplicada { get; set; }
    [Column("observaciones")] public string Observaciones { get; set; } = "";
    [Column("idempotency_key")] public string? IdempotencyKey { get; set; }
    [Column("anulada")] public bool Anulada { get; set; } = false;
    [Column("operacion_original_id")] public int? OperacionOriginalId { get; set; }
    [ForeignKey("OperacionOriginalId")] public Operacion? OperacionOriginal { get; set; }
    [Column("operacion_pareja_id")] public int? OperacionParejaId { get; set; }
    [ForeignKey("OperacionParejaId")] public Operacion? OperacionPareja { get; set; }
    public List<Movimiento> Movimientos { get; set; } = new();
}
```

- [ ] **Step 2: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Server/CasaCambio.Server.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 3: Generar la migración**

Run:
```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio && dotnet ef migrations add AgregarOperacionParejaId --project src/CasaCambio.Server/CasaCambio.Server.csproj --startup-project src/CasaCambio.Server/CasaCambio.Server.csproj
```
Expected: `Done. To undo this action, use 'ef migrations remove'` sin warning de pérdida de datos.

- [ ] **Step 4: Verificar la migración generada**

Abrir `src/CasaCambio.Server/Migrations/<timestamp>_AgregarOperacionParejaId.cs`. Como el campo es `int?` (nullable), no debería tener ningún `defaultValue` problemático — debe verse similar a:

```csharp
migrationBuilder.AddColumn<int>(
    name: "operacion_pareja_id",
    table: "operaciones",
    type: "integer",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_operaciones_operacion_pareja_id",
    table: "operaciones",
    column: "operacion_pareja_id");

migrationBuilder.AddForeignKey(
    name: "FK_operaciones_operaciones_operacion_pareja_id",
    table: "operaciones",
    column: "operacion_pareja_id",
    principalTable: "operaciones",
    principalColumn: "id");
```

Si el `AddColumn` tiene un `defaultValue` que no sea la ausencia total del parámetro (columna nullable no necesita default), reportar como BLOCKED antes de continuar — sería un indicio de que algo se generó distinto a lo esperado.

- [ ] **Step 5: Aplicar el cambio de schema a Supabase**

Usar la herramienta MCP de Supabase (`execute_sql`, project_id `vtyaunxljytbxbgyhmaz`):

```sql
ALTER TABLE operaciones ADD COLUMN operacion_pareja_id integer NULL;
CREATE INDEX "IX_operaciones_operacion_pareja_id" ON operaciones (operacion_pareja_id);
ALTER TABLE operaciones ADD CONSTRAINT "FK_operaciones_operaciones_operacion_pareja_id"
    FOREIGN KEY (operacion_pareja_id) REFERENCES operaciones(id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
('<timestamp>_AgregarOperacionParejaId', '8.0.0')
ON CONFLICT DO NOTHING;
```
(reemplazar `<timestamp>` por el nombre exacto del archivo de migración generado en el Step 3).

- [ ] **Step 6: Verificar la columna en Supabase**

```sql
SELECT column_name FROM information_schema.columns WHERE table_name = 'operaciones' AND column_name = 'operacion_pareja_id';
```
Expected: una fila.

- [ ] **Step 7: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Server/Models/Operacion.cs src/CasaCambio.Server/Migrations/
git commit -m "feat: agregar OperacionParejaId para vincular operaciones atómicas (Arbitraje)"
```

---

### Task 2: GuardarArbitraje (creación atómica)

**Files:**
- Modify: `src/CasaCambio.Server/Services/IOperacionService.cs`
- Modify: `src/CasaCambio.Server/Services/OperacionService.cs`
- Create: `src/CasaCambio.Tests/ArbitrajeTests.cs`

**Interfaces:**
- Consumes: `Operacion.OperacionParejaId` (Task 1), `ObtenerLimiteDeuda`, `ValidarMonoMonedaEfectivo`, `ObtenerOCrearSaldo` (privados ya existentes en `OperacionService`).
- Produces: `ArbitrajeResult { Exitoso, Mensaje, OperacionIdCompra, OperacionIdVenta }`, método `IOperacionService.GuardarArbitraje(...)` — usado por Task 4 (endpoint) y Task 3 (anulación en cascada, para saber si una operación tiene pareja).

- [ ] **Step 1: Agregar ArbitrajeResult e interfaz**

En `src/CasaCambio.Server/Services/IOperacionService.cs`, reemplazar el contenido completo por:

```csharp
namespace CasaCambio.Server.Services;

public class OperacionResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionId { get; set; }
    public static OperacionResult Success(int id) => new() { Exitoso = true, OperacionId = id };
    public static OperacionResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public class ArbitrajeResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionIdCompra { get; set; }
    public int? OperacionIdVenta { get; set; }
    public static ArbitrajeResult Success(int idCompra, int idVenta) => new() { Exitoso = true, OperacionIdCompra = idCompra, OperacionIdVenta = idVenta };
    public static ArbitrajeResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public interface IOperacionService
{
    OperacionResult GuardarOperacion(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    OperacionResult GuardarOperacionInterbancaria(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    OperacionResult GuardarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    ArbitrajeResult GuardarArbitraje(string monedaCompra, int cuentaAcreditaCompraId, decimal montoExtranjeroCompra, decimal cotizacionCompra, decimal pesosCompra, string monedaVenta, int cuentaDebitaVentaId, decimal montoExtranjeroVenta, decimal cotizacionVenta, decimal pesosVenta, int cuentaPesosId, string tipoOperacion, string observaciones = "");
    OperacionResult AnularOperacion(int id);
}
```

- [ ] **Step 2: Escribir el test (falla porque el método no existe)**

Crear `src/CasaCambio.Tests/ArbitrajeTests.cs`:

```csharp
using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using Xunit;

namespace CasaCambio.Tests;

public class ArbitrajeTests
{
    private const int IdCajaArs = 1;
    private const int IdCajaEur = 2;

    private readonly IOperacionService _operacionService;
    private readonly TestDbContextFactory _factory;

    public ArbitrajeTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        var cierreCajaService = new CierreCajaService(_factory, auditService);
        var validator = new OperacionValidator(_factory);
        _operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = IdCajaArs, Nombre = "EFECTIVO ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = IdCajaEur, Nombre = "EFECTIVO EUR", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaArs, Moneda = "ARS", Saldo = 1000000m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaEur, Moneda = "EUR", Saldo = 20000m });
        db.SaveChanges();
    }

    private ArbitrajeResult Arbitrar(decimal montoCompra, decimal cotCompra, decimal montoVenta, decimal cotVenta) =>
        _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: montoCompra * cotCompra,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: montoVenta * cotVenta,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "10K X 1.22");

    [Fact]
    public void GuardarArbitraje_PesosDistintos_RetornaError()
    {
        // Compra: 10000 EUR * 1800 = 18,000,000. Venta: 12000 EUR * 1475 = 17,700,000 (no coincide)
        var resultado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 12000m, cotVenta: 1475m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Pesos", resultado.Mensaje);
    }

    [Fact]
    public void GuardarArbitraje_PesosIguales_Exitoso_ActualizaSaldosYVinculaOperaciones()
    {
        // Compra: 10000 EUR * 1800 = 18,000,000. Venta: 12200 EUR * 1475.40984 ≈ 18,000,000
        decimal montoCompra = 10000m, cotCompra = 1800m;
        decimal pesos = montoCompra * cotCompra;
        decimal montoVenta = 12200m, cotVenta = Math.Round(pesos / montoVenta, 5, MidpointRounding.AwayFromZero);

        var resultado = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: pesos,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: Math.Round(montoVenta * cotVenta, 2, MidpointRounding.AwayFromZero),
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "10K X 1.22");

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.OperacionIdCompra);
        Assert.NotNull(resultado.OperacionIdVenta);

        using var db = _factory.CreateDbContext();
        var opCompra = db.Operaciones.First(o => o.Id == resultado.OperacionIdCompra);
        var opVenta = db.Operaciones.First(o => o.Id == resultado.OperacionIdVenta);
        Assert.Equal("Compra", opCompra.TipoOperacion);
        Assert.Equal("Venta", opVenta.TipoOperacion);
        Assert.Equal(opVenta.Id, opCompra.OperacionParejaId);
        Assert.Equal(opCompra.Id, opVenta.OperacionParejaId);

        // EFECTIVO EUR: +10000 (compra) -12200 (venta) = 20000 - 2200 = 17800
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(17800m, saldoEur.Saldo);

        // EFECTIVO ARS: -pesos (compra) +pesos (venta) = neto 0, saldo sin cambios
        var saldoArs = db.SaldosCuenta.First(s => s.CuentaId == IdCajaArs && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldoArs.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_VentaSinSaldoSuficiente_RetornaError_NoGuardaNada()
    {
        // La caja EUR tiene 20000, intenta vender 99999 (más de lo que tiene, sin límite de deuda por ser Efectivo)
        var resultado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 99999m, cotVenta: 180.0018m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Saldo insuficiente", resultado.Mensaje);

        using var db = _factory.CreateDbContext();
        Assert.Empty(db.Operaciones);
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(20000m, saldoEur.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_CuentaPesosInexistente_RetornaError()
    {
        var resultado = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: 100m, cotizacionCompra: 1800m, pesosCompra: 180000m,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: 100m, cotizacionVenta: 1800m, pesosVenta: 180000m,
            cuentaPesosId: 999999, tipoOperacion: "CLIENTE");

        Assert.False(resultado.Exitoso);
    }
}
```

- [ ] **Step 3: Correr el test para verificar que falla**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~ArbitrajeTests" --nologo -v quiet`
Expected: FAIL (error de compilación, `GuardarArbitraje` no existe en `IOperacionService`).

- [ ] **Step 4: Implementar GuardarArbitraje**

En `src/CasaCambio.Server/Services/OperacionService.cs`, agregar el método (después de `GuardarOperacionInterbancaria`, antes del comentario de `ObtenerLimiteDeuda`):

```csharp
public ArbitrajeResult GuardarArbitraje(string monedaCompra, int cuentaAcreditaCompraId, decimal montoExtranjeroCompra, decimal cotizacionCompra, decimal pesosCompra, string monedaVenta, int cuentaDebitaVentaId, decimal montoExtranjeroVenta, decimal cotizacionVenta, decimal pesosVenta, int cuentaPesosId, string tipoOperacion, string observaciones = "")
{
    montoExtranjeroCompra = Math.Round(montoExtranjeroCompra, 2, MidpointRounding.AwayFromZero);
    cotizacionCompra = Math.Round(cotizacionCompra, 5, MidpointRounding.AwayFromZero);
    pesosCompra = Math.Round(pesosCompra, 2, MidpointRounding.AwayFromZero);
    montoExtranjeroVenta = Math.Round(montoExtranjeroVenta, 2, MidpointRounding.AwayFromZero);
    cotizacionVenta = Math.Round(cotizacionVenta, 5, MidpointRounding.AwayFromZero);
    pesosVenta = Math.Round(pesosVenta, 2, MidpointRounding.AwayFromZero);

    if (pesosCompra != pesosVenta)
        return ArbitrajeResult.Error($"El monto en Pesos de la Compra ({pesosCompra:N2}) debe ser igual al de la Venta ({pesosVenta:N2}).");

    using var db = _contextFactory.CreateDbContext();
    using var transaction = db.Database.BeginTransaction();
    try
    {
        if (_cierreCajaService.HayDiaCerrado()) return ArbitrajeResult.Error("El dia de hoy ya esta cerrado.");

        var cuentaAcredita = db.Cuentas.Find(cuentaAcreditaCompraId);
        var cuentaDebita = db.Cuentas.Find(cuentaDebitaVentaId);
        var cuentaPesos = db.Cuentas.Find(cuentaPesosId);
        if (cuentaAcredita == null) return ArbitrajeResult.Error("Cuenta de Compra no encontrada");
        if (cuentaDebita == null) return ArbitrajeResult.Error("Cuenta de Venta no encontrada");
        if (cuentaPesos == null) return ArbitrajeResult.Error("Cuenta de Pesos no encontrada");

        var errAcredita = ValidarMonoMonedaEfectivo(db, cuentaAcreditaCompraId, monedaCompra);
        if (errAcredita != null) return ArbitrajeResult.Error(errAcredita.Mensaje);
        var errDebita = ValidarMonoMonedaEfectivo(db, cuentaDebitaVentaId, monedaVenta);
        if (errDebita != null) return ArbitrajeResult.Error(errDebita.Mensaje);

        var saldoPesos = ObtenerOCrearSaldo(db, cuentaPesosId, "ARS");
        var saldoAcredita = ObtenerOCrearSaldo(db, cuentaAcreditaCompraId, monedaCompra);
        var saldoDebita = ObtenerOCrearSaldo(db, cuentaDebitaVentaId, monedaVenta);

        // Único chequeo de saldo/límite necesario: la cuenta que entrega moneda extranjera en la Venta.
        // La cuenta ARS pivote no se chequea: como PesosCompra==PesosVenta, su efecto neto es siempre cero.
        // La cuenta que acredita en la Compra tampoco: solo recibe, nunca puede quedar insuficiente.
        if (saldoDebita.Saldo < montoExtranjeroVenta)
        {
            decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaDebita, saldoDebita);
            if (limiteDeuda > 0)
            {
                decimal saldoProyectado = saldoDebita.Saldo - montoExtranjeroVenta;
                if (saldoProyectado < -limiteDeuda)
                    return ArbitrajeResult.Error($"La cuenta '{cuentaDebita.Nombre}' superaría su límite de deuda en {monedaVenta} ({limiteDeuda:N2}).\nSaldo actual: {saldoDebita.Saldo:N2}, Requerido: {montoExtranjeroVenta:N2}");
            }
            else
            {
                return ArbitrajeResult.Error($"Saldo insuficiente en '{cuentaDebita.Nombre}' ({monedaVenta}). Disponible: {saldoDebita.Saldo:N2}, Requerido: {montoExtranjeroVenta:N2}");
            }
        }

        var tipoOpTexto = string.IsNullOrWhiteSpace(tipoOperacion) ? "" : $"[{tipoOperacion}] ";
        var observacionesCompletas = $"{tipoOpTexto}{observaciones}".Trim();

        var operacionCompra = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Compra", MontoTotalOrigen = pesosCompra, MontoTotalDestino = montoExtranjeroCompra, CotizacionAplicada = cotizacionCompra, Observaciones = observacionesCompletas };
        db.Operaciones.Add(operacionCompra);
        db.Movimientos.Add(new Movimiento { Operacion = operacionCompra, CuentaId = cuentaPesosId, Moneda = "ARS", Monto = -pesosCompra, Fecha = DateTime.UtcNow });
        db.Movimientos.Add(new Movimiento { Operacion = operacionCompra, CuentaId = cuentaAcreditaCompraId, Moneda = monedaCompra, Monto = montoExtranjeroCompra, Fecha = DateTime.UtcNow });
        saldoPesos.Saldo -= pesosCompra;
        saldoAcredita.Saldo += montoExtranjeroCompra;

        var operacionVenta = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Venta", MontoTotalOrigen = montoExtranjeroVenta, MontoTotalDestino = pesosVenta, CotizacionAplicada = cotizacionVenta, Observaciones = observacionesCompletas };
        db.Operaciones.Add(operacionVenta);
        db.Movimientos.Add(new Movimiento { Operacion = operacionVenta, CuentaId = cuentaDebitaVentaId, Moneda = monedaVenta, Monto = -montoExtranjeroVenta, Fecha = DateTime.UtcNow });
        db.Movimientos.Add(new Movimiento { Operacion = operacionVenta, CuentaId = cuentaPesosId, Moneda = "ARS", Monto = pesosVenta, Fecha = DateTime.UtcNow });
        saldoDebita.Saldo -= montoExtranjeroVenta;
        saldoPesos.Saldo += pesosVenta;

        db.SaveChanges(); // Necesario para obtener los Ids antes de vincularlos entre sí

        operacionCompra.OperacionParejaId = operacionVenta.Id;
        operacionVenta.OperacionParejaId = operacionCompra.Id;
        db.SaveChanges();

        transaction.Commit();
        try { _auditService.Registrar("CREATE", "Operacion_Arbitraje", operacionCompra.Id, new { operacionCompra.Id, operacionVenta.Id, montoExtranjeroCompra, montoExtranjeroVenta, pesosCompra }); } catch { }
        return ArbitrajeResult.Success(operacionCompra.Id, operacionVenta.Id);
    }
    catch (Exception ex) { transaction.Rollback(); return ArbitrajeResult.Error($"Error al guardar arbitraje: {ex.InnerException?.Message ?? ex.Message}"); }
}
```

- [ ] **Step 5: Correr el test para verificar que pasa**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~ArbitrajeTests" --nologo -v quiet`
Expected: `Superado: 4, Con error: 0`

- [ ] **Step 6: Correr toda la suite de tests del servidor**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --nologo -v quiet`
Expected: todos pasan (82 anteriores + 4 nuevos = 86).

- [ ] **Step 7: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Server/Services/IOperacionService.cs src/CasaCambio.Server/Services/OperacionService.cs src/CasaCambio.Tests/ArbitrajeTests.cs
git commit -m "feat: GuardarArbitraje - Compra+Venta atómicas vinculadas por OperacionParejaId"
```

---

### Task 3: Anulación en cascada

**Files:**
- Modify: `src/CasaCambio.Server/Services/OperacionService.cs`
- Modify: `src/CasaCambio.Tests/ArbitrajeTests.cs`

**Interfaces:**
- Consumes: `Operacion.OperacionParejaId` (Task 1), `AnularOperacion(int id)` (existente).
- Produces: ninguna interfaz nueva — comportamiento extendido de `AnularOperacion` ya usado por `OperacionesController.Anular` y por Movimientos/UI existentes.

- [ ] **Step 1: Escribir el test (falla porque la cascada no existe)**

En `src/CasaCambio.Tests/ArbitrajeTests.cs`, agregar al final de la clase (antes del último `}`):

```csharp
    [Fact]
    public void AnularOperacion_ConPareja_AnulaAmbasEnCascada()
    {
        var creado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 10000m, cotVenta: 1800m);
        Assert.True(creado.Exitoso);

        var resultadoAnular = _operacionService.AnularOperacion(creado.OperacionIdCompra!.Value);

        Assert.True(resultadoAnular.Exitoso);
        using var db = _factory.CreateDbContext();
        var opCompra = db.Operaciones.First(o => o.Id == creado.OperacionIdCompra);
        var opVenta = db.Operaciones.First(o => o.Id == creado.OperacionIdVenta);
        Assert.True(opCompra.Anulada);
        Assert.True(opVenta.Anulada, "La Venta debe anularse automáticamente al anular su pareja (la Compra).");

        // 2 anulaciones nuevas además de las 2 operaciones originales = 4 filas en total
        Assert.Equal(4, db.Operaciones.Count());

        // Saldos vuelven a su estado original: EFECTIVO EUR sin cambio neto, EFECTIVO ARS sin cambio neto
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(20000m, saldoEur.Saldo);
        var saldoArs = db.SaldosCuenta.First(s => s.CuentaId == IdCajaArs && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldoArs.Saldo);
    }

    [Fact]
    public void AnularOperacion_ParejaYaAnulada_NoIntentaAnularlaDeNuevo()
    {
        var creado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 10000m, cotVenta: 1800m);
        Assert.True(creado.Exitoso);

        _operacionService.AnularOperacion(creado.OperacionIdCompra!.Value);
        // Intentar anular la Venta, que ya fue anulada en cascada — debe fallar con el mensaje existente, no duplicar la reversión.
        var segundoIntento = _operacionService.AnularOperacion(creado.OperacionIdVenta!.Value);

        Assert.False(segundoIntento.Exitoso);
        Assert.Contains("ya fue anulada", segundoIntento.Mensaje);
    }
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~AnularOperacion_ConPareja" --nologo -v quiet`
Expected: FAIL — `opVenta.Anulada` es `false` (la cascada no existe todavía).

- [ ] **Step 3: Implementar la cascada**

En `src/CasaCambio.Server/Services/OperacionService.cs`, reemplazar el método `AnularOperacion` completo:

```csharp
public OperacionResult AnularOperacion(int id)
{
    using var db = _contextFactory.CreateDbContext();
    using var transaction = db.Database.BeginTransaction();
    try
    {
        var resultado = AnularOperacionInterno(db, id);
        if (!resultado.Exitoso) { transaction.Rollback(); return resultado; }
        transaction.Commit();
        return resultado;
    }
    catch (Exception ex) { transaction.Rollback(); return OperacionResult.Error($"Error al anular: {ex.InnerException?.Message ?? ex.Message}"); }
}

private OperacionResult AnularOperacionInterno(AppDbContext db, int id)
{
    var original = db.Operaciones.Include(o => o.Movimientos).FirstOrDefault(o => o.Id == id);
    if (original == null) return OperacionResult.Error("Operación no encontrada.");
    if (original.Anulada) return OperacionResult.Error("La operación ya fue anulada.");
    if (original.OperacionOriginalId.HasValue) return OperacionResult.Error("No se puede anular una anulación.");
    if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El día de hoy ya está cerrado.");

    var anulacion = new Operacion
    {
        Fecha = DateTime.UtcNow,
        TipoOperacion = "Anulacion",
        MontoTotalOrigen = original.MontoTotalOrigen,
        MontoTotalDestino = original.MontoTotalDestino,
        CotizacionAplicada = original.CotizacionAplicada,
        Observaciones = $"ANULACIÓN DE OP-{id:D5}",
        OperacionOriginalId = id
    };
    db.Operaciones.Add(anulacion);

    foreach (var mov in original.Movimientos)
    {
        db.Movimientos.Add(new Movimiento
        {
            Operacion = anulacion,
            CuentaId = mov.CuentaId,
            Moneda = mov.Moneda,
            Monto = -mov.Monto,
            Fecha = DateTime.UtcNow
        });
        var saldo = ObtenerOCrearSaldo(db, mov.CuentaId, mov.Moneda);
        saldo.Saldo -= mov.Monto;
    }

    original.Anulada = true;
    db.SaveChanges();
    try { _auditService.Registrar("ANULAR", "Operacion", id, datosNuevos: new { anulacion_id = anulacion.Id }); } catch { }

    // Anulación en cascada: si esta operación tiene pareja (Arbitraje) y no está ya anulada, anularla también.
    if (original.OperacionParejaId.HasValue)
    {
        var pareja = db.Operaciones.FirstOrDefault(o => o.Id == original.OperacionParejaId.Value);
        if (pareja != null && !pareja.Anulada)
        {
            var resultadoPareja = AnularOperacionInterno(db, pareja.Id);
            if (!resultadoPareja.Exitoso) return resultadoPareja;
        }
    }

    return OperacionResult.Success(anulacion.Id);
}
```

Nota: `AnularOperacionInterno` recibe el `db`/`transaction` ya abiertos (no crea los suyos), a diferencia de los demás métodos `Guardar*` — así la anulación de ambas patas ocurre en una sola transacción, igual que la creación.

- [ ] **Step 4: Correr los tests de anulación para verificar que pasan**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~ArbitrajeTests|FullyQualifiedName~AnularOperacion" --nologo -v quiet`
Expected: todos pasan, incluyendo los 2 nuevos y los tests de anulación preexistentes (que no usan pareja y deben seguir funcionando igual).

- [ ] **Step 5: Correr toda la suite de tests del servidor**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --nologo -v quiet`
Expected: todos pasan (86 anteriores + 2 nuevos = 88).

- [ ] **Step 6: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Server/Services/OperacionService.cs src/CasaCambio.Tests/ArbitrajeTests.cs
git commit -m "feat: anular una pata del Arbitraje anula automáticamente su pareja"
```

---

### Task 4: DTOs compartidos + endpoint de servidor

**Files:**
- Create: `src/CasaCambio.Shared/Responses/ArbitrajeResponse.cs`
- Create: `src/CasaCambio.Shared/Requests/CrearArbitrajeRequest.cs`
- Modify: `src/CasaCambio.Server/Controllers/OperacionesController.cs`

**Interfaces:**
- Consumes: `IOperacionService.GuardarArbitraje(...)` (Task 2), `IPPPService.RegistrarCompra`/`RegistrarVenta` (ya existente, mismo patrón que usan los endpoints `compra`/`venta`).
- Produces: `ArbitrajeResponse { Exitoso, Mensaje, OperacionIdCompra, OperacionIdVenta }`, `CrearArbitrajeRequest`, endpoint `POST api/operaciones/arbitraje` — usado por Task 5 (cliente desktop).

- [ ] **Step 1: Crear ArbitrajeResponse**

Crear `src/CasaCambio.Shared/Responses/ArbitrajeResponse.cs`:

```csharp
namespace CasaCambio.Shared.Responses;

public class ArbitrajeResponse
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionIdCompra { get; set; }
    public int? OperacionIdVenta { get; set; }

    public static ArbitrajeResponse Success(int idCompra, int idVenta) => new() { Exitoso = true, OperacionIdCompra = idCompra, OperacionIdVenta = idVenta };
    public static ArbitrajeResponse Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}
```

- [ ] **Step 2: Crear CrearArbitrajeRequest**

Crear `src/CasaCambio.Shared/Requests/CrearArbitrajeRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearArbitrajeRequest
{
    [Required] public string MonedaCompra { get; set; } = "";
    [Required] public int CuentaAcreditaCompraId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoExtranjeroCompra { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal CotizacionCompra { get; set; }
    [Range(0.01, double.MaxValue)] public decimal PesosCompra { get; set; }

    [Required] public string MonedaVenta { get; set; } = "";
    [Required] public int CuentaDebitaVentaId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoExtranjeroVenta { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal CotizacionVenta { get; set; }
    [Range(0.01, double.MaxValue)] public decimal PesosVenta { get; set; }

    [Required] public int CuentaPesosId { get; set; }
    public string TipoOperacion { get; set; } = "CLIENTE";
    public string Observaciones { get; set; } = "";
}
```

- [ ] **Step 3: Agregar el endpoint al controller**

En `src/CasaCambio.Server/Controllers/OperacionesController.cs`, agregar después del método `CreditoDebito` (antes del comentario de `interbancaria`):

```csharp
[HttpPost("arbitraje")]
public IActionResult Arbitraje([FromBody] CrearArbitrajeRequest req)
{
    var result = _operacionService.GuardarArbitraje(
        req.MonedaCompra, req.CuentaAcreditaCompraId, req.MontoExtranjeroCompra, req.CotizacionCompra, req.PesosCompra,
        req.MonedaVenta, req.CuentaDebitaVentaId, req.MontoExtranjeroVenta, req.CotizacionVenta, req.PesosVenta,
        req.CuentaPesosId, req.TipoOperacion, req.Observaciones);
    if (result.Exitoso)
    {
        _pppService.RegistrarCompra(req.MonedaCompra, req.MontoExtranjeroCompra, req.PesosCompra);
        _pppService.RegistrarVenta(req.MonedaVenta, req.MontoExtranjeroVenta);
    }
    return Ok(result.Exitoso
        ? ArbitrajeResponse.Success(result.OperacionIdCompra!.Value, result.OperacionIdVenta!.Value)
        : ArbitrajeResponse.Error(result.Mensaje));
}
```

Agregar el `using CasaCambio.Shared.Responses;` ya está presente en el archivo (usado por `OperacionResponse`); no hace falta agregar nada más, `ArbitrajeResponse` vive en el mismo namespace.

- [ ] **Step 4: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Server/CasaCambio.Server.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 5: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Shared/Responses/ArbitrajeResponse.cs src/CasaCambio.Shared/Requests/CrearArbitrajeRequest.cs src/CasaCambio.Server/Controllers/OperacionesController.cs
git commit -m "feat: endpoint POST api/operaciones/arbitraje"
```

---

### Task 5: Cliente API desktop

**Files:**
- Modify: `src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs`
- Modify: `src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs`

**Interfaces:**
- Consumes: `CrearArbitrajeRequest`, `ArbitrajeResponse` (Task 4), endpoint `POST api/operaciones/arbitraje`.
- Produces: `ICasaCambioApiClient.CrearArbitrajeAsync(CrearArbitrajeRequest request) : Task<ArbitrajeResponse>` — usado por Task 6 (ViewModel).

- [ ] **Step 1: Agregar el método a la interfaz**

En `src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs`, agregar junto a `CrearInterbancarioAsync`:

```csharp
Task<ArbitrajeResponse> CrearArbitrajeAsync(CrearArbitrajeRequest request);
```

- [ ] **Step 2: Implementar el método**

En `src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs`, agregar junto a `CrearInterbancarioAsync`:

```csharp
public async Task<ArbitrajeResponse> CrearArbitrajeAsync(CrearArbitrajeRequest request)
    => await PostAuthenticatedAsync<ArbitrajeResponse>("api/operaciones/arbitraje", request);
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Client.Core/CasaCambio.Client.Core.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs
git commit -m "feat: cliente desktop para POST api/operaciones/arbitraje"
```

---

### Task 6: ArbitrajeViewModel

**Files:**
- Create: `ViewModels/ArbitrajeViewModel.cs`

**Interfaces:**
- Consumes: `ICasaCambioApiClient.CrearArbitrajeAsync` (Task 5), `ICasaCambioApiClient.ObtenerCuentasAsync`/`ObtenerMonedasAsync`/`ObtenerCotizacionesHoyAsync` (ya existentes), `CuentaMonedaTag` (definido en `Views/CompraWindow.axaml.cs`, namespace `SistemaCambio.Views`), `MontoHelper.Parsear` (`SistemaCambio.Services`).
- Produces: propiedades y comando que consume Task 7 (la vista): `MonedaCompraCodigo`, `MontoExtranjeroCompraTexto`, `CotizacionCompraTexto`, `PesosCompraTexto`, `CuentaAcreditaCompra` (CuentaMonedaTag?), y el mismo set con sufijo `Venta`; `TipoOperacion` (string, "CLIENTE"/"CASA"), `Observaciones` (string), `PuedeAceptar` (bool), `MensajeError` (string), `MostrarError` (bool); `CuentasCompra`/`CuentasVenta` (List\<CuentaMonedaTag\>, filtradas por la moneda elegida en cada lado); `MonedasDisponibles` (List\<MonedaDto\>); comando `AceptarCommand` (IAsyncRelayCommand); evento `OperacionGuardada` (Action<int, int>, ids Compra/Venta) y `SolicitarCierre` (Action) para que la vista cierre la ventana.

- [ ] **Step 1: Crear el ViewModel**

Crear `ViewModels/ArbitrajeViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Views;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.ViewModels
{
    public partial class ArbitrajeViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private List<CuentaDto> _todasLasCuentas = new();
        private bool _recalculandoCompra;
        private bool _recalculandoVenta;

        [ObservableProperty] private List<MonedaDto> _monedasDisponibles = new();
        [ObservableProperty] private MonedaDto? _monedaCompra;
        [ObservableProperty] private MonedaDto? _monedaVenta;

        [ObservableProperty] private List<CuentaMonedaTag> _cuentasCompra = new();
        [ObservableProperty] private List<CuentaMonedaTag> _cuentasVenta = new();
        [ObservableProperty] private CuentaMonedaTag? _cuentaAcreditaCompra;
        [ObservableProperty] private CuentaMonedaTag? _cuentaDebitaVenta;

        [ObservableProperty] private string _montoExtranjeroCompraTexto = "0.00";
        [ObservableProperty] private string _cotizacionCompraTexto = "0.00000";
        [ObservableProperty] private string _pesosCompraTexto = "0.00";

        [ObservableProperty] private string _montoExtranjeroVentaTexto = "0.00";
        [ObservableProperty] private string _cotizacionVentaTexto = "0.00000";
        [ObservableProperty] private string _pesosVentaTexto = "0.00";

        [ObservableProperty] private string _observaciones = "";
        [ObservableProperty] private string _tipoOperacion = "CLIENTE";
        public List<string> TiposOperacion { get; } = new() { "CLIENTE", "CASA" };

        [ObservableProperty] private bool _puedeAceptar;
        [ObservableProperty] private bool _mostrarError;
        [ObservableProperty] private string _mensajeError = "";

        public ICommand AceptarCommand { get; }

        public event Action<int, int>? OperacionGuardada;
        public event Action? SolicitarCierre;

        public ArbitrajeViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;
            AceptarCommand = new AsyncRelayCommand(AceptarAsync);
            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await Task.WhenAll(cuentasTask, monedasTask);

                _todasLasCuentas = cuentasTask.Result;
                MonedasDisponibles = monedasTask.Result.Where(m => m.Codigo != "ARS").OrderBy(m => m.Codigo).ToList();
                if (MonedasDisponibles.Count > 0)
                {
                    MonedaCompra = MonedasDisponibles[0];
                    MonedaVenta = MonedasDisponibles[0];
                }
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        partial void OnMonedaCompraChanged(MonedaDto? value)
        {
            if (value == null) { CuentasCompra = new(); CuentaAcreditaCompra = null; return; }
            CuentasCompra = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, value.Codigo);
            CuentaAcreditaCompra = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, value.Codigo, CuentasCompra);
            _ = CargarCotizacionCompraAsync(value.Codigo);
        }

        partial void OnMonedaVentaChanged(MonedaDto? value)
        {
            if (value == null) { CuentasVenta = new(); CuentaDebitaVenta = null; return; }
            CuentasVenta = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, value.Codigo);
            CuentaDebitaVenta = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, value.Codigo, CuentasVenta);
            _ = CargarCotizacionVentaAsync(value.Codigo);
        }

        private async Task CargarCotizacionCompraAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                CotizacionCompraTexto = (cot?.CotizacionCompra ?? 0m).ToString("N5");
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionCompraAsync", ex); }
        }

        private async Task CargarCotizacionVentaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                CotizacionVentaTexto = (cot?.CotizacionVenta ?? 0m).ToString("N5");
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionVentaAsync", ex); }
        }

        // ── Cálculo reactivo sin loops: un flag por sección evita que la asignación
        // automática de Pesos dispare un recálculo hacia MontoExtranjero/Cotización.

        partial void OnMontoExtranjeroCompraTextoChanged(string value) => RecalcularCompra();
        partial void OnCotizacionCompraTextoChanged(string value) => RecalcularCompra();
        partial void OnPesosCompraTextoChanged(string value) => ActualizarPuedeAceptar();

        partial void OnMontoExtranjeroVentaTextoChanged(string value) => RecalcularVenta();
        partial void OnCotizacionVentaTextoChanged(string value) => RecalcularVenta();
        partial void OnPesosVentaTextoChanged(string value) => ActualizarPuedeAceptar();

        private void RecalcularCompra()
        {
            if (_recalculandoCompra) return;
            decimal monto = MontoHelper.Parsear(MontoExtranjeroCompraTexto);
            decimal cotizacion = MontoHelper.Parsear(CotizacionCompraTexto);
            decimal pesos = Math.Round(monto * cotizacion, 2, MidpointRounding.AwayFromZero);
            _recalculandoCompra = true;
            PesosCompraTexto = pesos.ToString("N2");
            _recalculandoCompra = false;
        }

        private void RecalcularVenta()
        {
            if (_recalculandoVenta) return;
            decimal monto = MontoHelper.Parsear(MontoExtranjeroVentaTexto);
            decimal cotizacion = MontoHelper.Parsear(CotizacionVentaTexto);
            decimal pesos = Math.Round(monto * cotizacion, 2, MidpointRounding.AwayFromZero);
            _recalculandoVenta = true;
            PesosVentaTexto = pesos.ToString("N2");
            _recalculandoVenta = false;
        }

        private void ActualizarPuedeAceptar()
        {
            decimal pesosCompra = MontoHelper.Parsear(PesosCompraTexto);
            decimal pesosVenta = MontoHelper.Parsear(PesosVentaTexto);
            PuedeAceptar = pesosCompra > 0 && pesosVenta == pesosCompra;
        }

        private async Task AceptarAsync()
        {
            MostrarError = false;
            MensajeError = "";

            if (CuentaAcreditaCompra == null || CuentaDebitaVenta == null || MonedaCompra == null || MonedaVenta == null)
            {
                MensajeError = "Complete todos los campos requeridos.";
                MostrarError = true;
                return;
            }

            var cuentaPesos = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, "ARS",
                CuentaAutoComplete.ConstruirTags(_todasLasCuentas, "ARS"));
            if (cuentaPesos == null)
            {
                MensajeError = "No se encontró una caja de efectivo en ARS.";
                MostrarError = true;
                return;
            }

            var request = new CrearArbitrajeRequest
            {
                MonedaCompra = MonedaCompra.Codigo,
                CuentaAcreditaCompraId = CuentaAcreditaCompra.CuentaId,
                MontoExtranjeroCompra = MontoHelper.Parsear(MontoExtranjeroCompraTexto),
                CotizacionCompra = MontoHelper.Parsear(CotizacionCompraTexto),
                PesosCompra = MontoHelper.Parsear(PesosCompraTexto),

                MonedaVenta = MonedaVenta.Codigo,
                CuentaDebitaVentaId = CuentaDebitaVenta.CuentaId,
                MontoExtranjeroVenta = MontoHelper.Parsear(MontoExtranjeroVentaTexto),
                CotizacionVenta = MontoHelper.Parsear(CotizacionVentaTexto),
                PesosVenta = MontoHelper.Parsear(PesosVentaTexto),

                CuentaPesosId = cuentaPesos.CuentaId,
                TipoOperacion = TipoOperacion,
                Observaciones = Observaciones
            };

            try
            {
                var resultado = await _apiClient.CrearArbitrajeAsync(request);
                if (!resultado.Exitoso)
                {
                    MensajeError = resultado.Mensaje;
                    MostrarError = true;
                    return;
                }
                OperacionGuardada?.Invoke(resultado.OperacionIdCompra ?? 0, resultado.OperacionIdVenta ?? 0);
                SolicitarCierre?.Invoke();
            }
            catch (Exception ex)
            {
                MensajeError = ex.Message;
                MostrarError = true;
            }
        }
    }
}
```

**Nota:** el ViewModel llama a `CuentaAutoComplete.ConstruirTags`/`PrimeraCajaEfectivo` (`Views/Helpers/CuentaAutoComplete.cs`, namespace `SistemaCambio.Views.Helpers`, ya importado arriba). Esos dos métodos son seguros de usar desde el ViewModel porque solo operan sobre `CuentaDto`/`CuentaMonedaTag` — no referencian ningún tipo de Avalonia. El resto de la clase (`Configurar`, `Seleccionar`, `ObtenerSeleccion`, que sí tocan `AutoCompleteBox`) se sigue usando exclusivamente desde el code-behind de la vista (Task 7), nunca desde este ViewModel.

- [ ] **Step 2: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 3: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add ViewModels/ArbitrajeViewModel.cs
git commit -m "feat: ArbitrajeViewModel con cálculo reactivo de Pesos y validación PuedeAceptar"
```

---

### Task 7: ArbitrajeWindow (vista)

**Files:**
- Create: `Views/ArbitrajeWindow.axaml`
- Create: `Views/ArbitrajeWindow.axaml.cs`

**Interfaces:**
- Consumes: `ArbitrajeViewModel` (Task 6), `CuentaAutoComplete.Configurar/Seleccionar/ObtenerSeleccion` (`Views/Helpers/CuentaAutoComplete.cs`, ya existente), `NotificationService`.

- [ ] **Step 1: Crear el AXAML**

Crear `Views/ArbitrajeWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:SistemaCambio.Views.Controls"
        xmlns:vm="clr-namespace:SistemaCambio.ViewModels"
        x:Class="SistemaCambio.Views.ArbitrajeWindow"
        x:CompileBindings="False"
        Title="Compra / Venta"
        Width="640" MinWidth="560" Height="760" MinHeight="680"
        WindowStartupLocation="CenterOwner"
        CanResize="True"
        Background="{DynamicResource AppBackgroundBrush}">

    <Grid RowDefinitions="Auto, Auto, Auto, Auto" Margin="16">

        <controls:NotificationPanel x:Name="notificationPanel"
                                    Grid.RowSpan="4"
                                    VerticalAlignment="Top"
                                    HorizontalAlignment="Right"
                                    ZIndex="100"/>

        <!-- ── COMPRA ── -->
        <Border Grid.Row="0" Background="{DynamicResource CardBackgroundBrush}"
                BorderBrush="#1E88E5" BorderThickness="2" CornerRadius="8" Padding="16" Margin="0,0,0,12">
            <Grid ColumnDefinitions="110, *" RowDefinitions="Auto, Auto, Auto, Auto, Auto">
                <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="Compra" FontSize="16" FontWeight="Bold"
                           Foreground="#1E88E5" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="Moneda" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <ComboBox Grid.Row="1" Grid.Column="1" ItemsSource="{Binding MonedasDisponibles}"
                          SelectedItem="{Binding MonedaCompra}" DisplayMemberBinding="{Binding Codigo}"
                          HorizontalAlignment="Stretch" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="2" Grid.Column="0" Text="Mon. Extranjera" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <TextBox Grid.Row="2" Grid.Column="1" x:Name="txtMontoCompra" Text="{Binding MontoExtranjeroCompraTexto}"
                         TextAlignment="Right" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="3" Grid.Column="0" Text="Cotización" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <Grid Grid.Row="3" Grid.Column="1" ColumnDefinitions="*, *" Margin="0,0,0,10">
                    <TextBox Grid.Column="0" x:Name="txtCotizacionCompra" Text="{Binding CotizacionCompraTexto}"
                             TextAlignment="Right" Margin="0,0,8,0"/>
                    <TextBox Grid.Column="1" Text="{Binding PesosCompraTexto}" IsReadOnly="True" TextAlignment="Right"
                             Background="{DynamicResource SidebarBackgroundBrush}"/>
                </Grid>

                <TextBlock Grid.Row="4" Grid.Column="0" Text="Acredita en" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}"/>
                <AutoCompleteBox Grid.Row="4" Grid.Column="1" x:Name="cmbCuentaCompra"
                                  Watermark="Buscar cuenta..." MaxDropDownHeight="200"/>
            </Grid>
        </Border>

        <!-- ── VENTA ── -->
        <Border Grid.Row="1" Background="{DynamicResource CardBackgroundBrush}"
                BorderBrush="#E53935" BorderThickness="2" CornerRadius="8" Padding="16" Margin="0,0,0,12">
            <Grid ColumnDefinitions="110, *" RowDefinitions="Auto, Auto, Auto, Auto, Auto, Auto">
                <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="Venta" FontSize="16" FontWeight="Bold"
                           Foreground="#E53935" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="Moneda" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <ComboBox Grid.Row="1" Grid.Column="1" ItemsSource="{Binding MonedasDisponibles}"
                          SelectedItem="{Binding MonedaVenta}" DisplayMemberBinding="{Binding Codigo}"
                          HorizontalAlignment="Stretch" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="2" Grid.Column="0" Text="Mon. Extranjera" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <TextBox Grid.Row="2" Grid.Column="1" x:Name="txtMontoVenta" Text="{Binding MontoExtranjeroVentaTexto}"
                         TextAlignment="Right" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="3" Grid.Column="0" Text="Cotización" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <Grid Grid.Row="3" Grid.Column="1" ColumnDefinitions="*, *" Margin="0,0,0,10">
                    <TextBox Grid.Column="0" x:Name="txtCotizacionVenta" Text="{Binding CotizacionVentaTexto}"
                             TextAlignment="Right" Margin="0,0,8,0"/>
                    <TextBox Grid.Column="1" Text="{Binding PesosVentaTexto}" IsReadOnly="True" TextAlignment="Right"
                             Background="{DynamicResource SidebarBackgroundBrush}"/>
                </Grid>

                <TextBlock Grid.Row="4" Grid.Column="0" Text="Debita de" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}" Margin="0,0,0,10"/>
                <AutoCompleteBox Grid.Row="4" Grid.Column="1" x:Name="cmbCuentaVenta"
                                  Watermark="Buscar cuenta..." MaxDropDownHeight="200" Margin="0,0,0,10"/>

                <TextBlock Grid.Row="5" Grid.Column="0" Text="Observaciones" VerticalAlignment="Center"
                           Foreground="{DynamicResource SecondaryTextBrush}"/>
                <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding Observaciones}" Height="32"/>
            </Grid>
        </Border>

        <!-- ── Footer ── -->
        <Border Grid.Row="2" Background="{DynamicResource CardBackgroundBrush}"
                BorderBrush="{DynamicResource DangerBrush}" BorderThickness="1" CornerRadius="8"
                Padding="12,8" Margin="0,0,0,12" IsVisible="{Binding MostrarError}">
            <TextBlock Text="{Binding MensajeError}" Foreground="{DynamicResource DangerBrush}"
                       TextWrapping="Wrap" FontSize="12"/>
        </Border>

        <Grid Grid.Row="3" ColumnDefinitions="110, *, Auto" RowDefinitions="Auto">
            <TextBlock Grid.Column="0" Text="Tipo operación" VerticalAlignment="Center"
                       Foreground="{DynamicResource SecondaryTextBrush}"/>
            <ComboBox Grid.Column="1" ItemsSource="{Binding TiposOperacion}" SelectedItem="{Binding TipoOperacion}"
                      Width="160" HorizontalAlignment="Left"/>
            <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="10">
                <Button Content="Aceptar" Classes="Primary" Width="110" Height="36"
                        Command="{Binding AceptarCommand}" IsEnabled="{Binding PuedeAceptar}"
                        HorizontalContentAlignment="Center" VerticalContentAlignment="Center"/>
                <Button Content="Cancelar" Classes="Secondary" Width="110" Height="36"
                        Click="BtnCancelar_Click"
                        HorizontalContentAlignment="Center" VerticalContentAlignment="Center"/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 2: Crear el code-behind**

Crear `Views/ArbitrajeWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.Views
{
    public partial class ArbitrajeWindow : Window
    {
        public ArbitrajeWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            CuentaAutoComplete.Configurar(cmbCuentaCompra);
            CuentaAutoComplete.Configurar(cmbCuentaVenta);

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var viewModel = new ArbitrajeViewModel(apiClient);
            DataContext = viewModel;

            viewModel.OperacionGuardada += (idCompra, idVenta) =>
                NotificationService.Success("Arbitraje registrado", $"Compra OP-{idCompra:D5} / Venta OP-{idVenta:D5}");
            viewModel.SolicitarCierre += Close;

            // Sincroniza la selección de cuenta del ViewModel con los AutoCompleteBox
            // (no se puede bindear CuentaMonedaTag por TwoWay a Configurar/Seleccionar sin pasar por el helper).
            DataContextChanged += (_, _) =>
            {
                if (DataContext is not ArbitrajeViewModel vm) return;
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentasCompra))
                        cmbCuentaCompra.ItemsSource = vm.CuentasCompra;
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentaAcreditaCompra))
                        CuentaAutoComplete.Seleccionar(cmbCuentaCompra, vm.CuentaAcreditaCompra);
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentasVenta))
                        cmbCuentaVenta.ItemsSource = vm.CuentasVenta;
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentaDebitaVenta))
                        CuentaAutoComplete.Seleccionar(cmbCuentaVenta, vm.CuentaDebitaVenta);
                };
            };

            cmbCuentaCompra.LostFocus += (_, _) =>
            {
                if (DataContext is ArbitrajeViewModel vm)
                    vm.CuentaAcreditaCompra = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaCompra);
            };
            cmbCuentaVenta.LostFocus += (_, _) =>
            {
                if (DataContext is ArbitrajeViewModel vm)
                    vm.CuentaDebitaVenta = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaVenta);
            };
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add Views/ArbitrajeWindow.axaml Views/ArbitrajeWindow.axaml.cs
git commit -m "feat: ventana Compra/Venta (Arbitraje)"
```

---

### Task 8: Acceso desde el sidebar de MainWindow

**Files:**
- Modify: `Views/MainWindow.axaml`
- Modify: `Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `ArbitrajeWindow` (Task 7).

- [ ] **Step 1: Agregar el botón al sidebar**

En `Views/MainWindow.axaml`, agregar un botón nuevo justo después del botón "Venta" (`Command="{Binding AbrirVentaCommand}"`) y antes de "Crédito/Débito":

```xml
<Button Classes="SidebarButton" Click="BtnArbitraje_Click">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <icon:MaterialIcon Kind="SwapHorizontal" Width="18" Height="18"/>
        <TextBlock Text="Compra / Venta" VerticalAlignment="Center" FontSize="13"/>
    </StackPanel>
</Button>
```

- [ ] **Step 2: Agregar el handler y el método de apertura**

En `Views/MainWindow.axaml.cs`, junto a `BtnCompra_Click`/`AbrirCompraWindow`:

```csharp
private async void BtnArbitraje_Click(object? sender, RoutedEventArgs e) => await AbrirArbitrajeWindow();
```

Y junto a `AbrirCompraWindow`:

```csharp
private async Task AbrirArbitrajeWindow() { var w = new ArbitrajeWindow(); await w.ShowDialog(this); _viewModel?.RefrescarDatos(); }
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add Views/MainWindow.axaml Views/MainWindow.axaml.cs
git commit -m "feat: acceso a Compra/Venta (Arbitraje) desde el sidebar"
```

---

### Task 9: Verificación final y deploy

**Files:** ninguno (solo verificación y despliegue)

- [ ] **Step 1: Build completo de la solución**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/Sistema_Casa_Cambio.sln --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 2: Correr toda la suite de tests del servidor**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --nologo -v quiet`
Expected: todos pasan (88 esperados: 82 previos + 6 de esta feature).

- [ ] **Step 3: Correr toda la suite de tests del cliente**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Client.Tests/CasaCambio.Client.Tests.csproj --nologo -v quiet`
Expected: todos pasan (11, sin cambios en esta feature).

- [ ] **Step 4: Push a GitHub**

```bash
git -C /home/agustin/PROYECTOS/Sistema_Casa_Cambio push origin main
```

- [ ] **Step 5: Deploy del servidor (requiere confirmación explícita del usuario antes de ejecutar, es producción)**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src && ~/.fly/bin/fly deploy
```
Expected: log con `Machine ... is now in a good state` (no solo exit code — leer el texto completo del log).

- [ ] **Step 6: Verificar contra el servidor real**

```bash
curl -s -w "\nHTTP %{http_code}\n" https://casa-cambio-api.fly.dev/api/auth/health --max-time 20
```
Expected: `{"status":"healthy",...}` y `HTTP 200`.

- [ ] **Step 7: Probar el flujo end-to-end manualmente**

Abrir la app desktop, ir a "Compra / Venta" desde el sidebar, y verificar:
- El botón Aceptar está deshabilitado hasta que Pesos(Compra) == Pesos(Venta).
- Al guardar exitosamente, aparece la notificación con ambos códigos de operación.
- En Movimientos, ambas operaciones (Compra y Venta) aparecen vinculadas; anular una anula la otra automáticamente.
