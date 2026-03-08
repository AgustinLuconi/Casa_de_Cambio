# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Desktop application for a currency exchange house (Casa de Cambio), built with C# .NET 8, Avalonia UI, and PostgreSQL. Implements double-entry bookkeeping and weighted average cost (PPP) tracking.

## Commands

```bash
# Build
dotnet build

# Run the application
dotnet run --project SistemaCambio.csproj

# Run all tests
dotnet test Tests/SistemaCambio.Tests.csproj

# Run a specific test by name
dotnet test Tests/SistemaCambio.Tests.csproj --filter "FullyQualifiedName~TestName"

# EF Core migrations (run from repo root)
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture

The project follows strict MVVM with Avalonia UI. The full pattern is enforced by `SKILL.md` at the project root.

### Layers

- **Models/** — POCOs mapped to PostgreSQL tables via EF Core. All entity configuration is in `AppDbContext.cs`. The `Cuenta` (account) + `SaldoCuenta` (balance per currency) split allows multi-currency balances per account.
- **Services/** — Stateless business logic. All services implement an interface and are registered as **Singleton** in `ServiceCollectionExtensions.cs` because they only hold an `IDbContextFactory<AppDbContext>`. Validators live in `Services/Validators/`.
- **ViewModels/** — Use `[ObservableProperty]` from CommunityToolkit.Mvvm. Never reference Avalonia UI types. Communicate with views via events (`SolicitarAbrirVentana`, `MostrarMensajeEvent`, etc.) rather than direct window references.
- **Views/** — `.axaml` files with compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`). Code-behind only calls `InitializeComponent()`. Services are resolved from the global `App.Services` static property.

### DI Container

`App.axaml.cs` builds the service container at startup via `serviceCollection.ConfigurarServicios()`. The container is exposed as `App.Services` (static `IServiceProvider`). Views access services through `App.Services.GetRequiredService<T>()` since they are not DI-constructed.

### Database

PostgreSQL via Npgsql EF Core. Connection string is hardcoded in two places:
- `AppDbContext.OnConfiguring()` — used when `options` is not already configured (direct context instantiation)
- `ServiceCollectionExtensions.ConfigurarServicios()` — used for the DI-registered `IDbContextFactory`

In DEBUG mode, all SQL queries are logged to the console.

### Core Accounting Model

Every currency exchange creates **4 movimientos** (ledger entries) summing to zero — a double-entry constraint called "Numscript UMN" in the codebase:

1. Debit origin account (house loses currency A)
2. Credit "Mundo Exterior" in currency A (external/customer side receives A)
3. Debit "Mundo Exterior" in currency B (external side gives B)
4. Credit destination account (house gains currency B)

The `Mundo Exterior` account (type `"Externo"`) is auto-created on first use. Cross-currency internal transfers (`GuardarOperacionInterbancaria`) use the same 4-leg pattern but between two internal accounts.

### Key Services

| Service | Purpose |
|---|---|
| `OperacionService` | FX trades (Compra/Venta), Crédito/Débito, Interbancaria — all atomic with DB transactions |
| `PPPService` | Tracks weighted average cost (Precio Promedio Ponderado) per currency via `TenenciaMoneda` |
| `ArqueoService` | Cash count reconciliation — creates automatic adjustment entries for discrepancies |
| `CierreCajaService` | Day-close logic; `HayDiaCerrado()` is checked before every operation |
| `AuditService` | Appends to `AuditLog` table after every mutation |
| `DashboardService` | Read-only aggregations for charts |
| `QueryService` | Read-only queries for reports and history |

### Rounding Convention

All monetary amounts use `Math.Round(..., 2, MidpointRounding.AwayFromZero)`. Exchange rates are rounded to 5 decimal places. This is applied at the top of every `GuardarOperacion*` method before any other logic.

### Testing

Tests use **xUnit** with **EF Core InMemory** database. `TestDbContextFactory` wraps an in-memory context and suppresses `TransactionIgnoredWarning` (InMemory does not support real transactions, but the services call `BeginTransaction`). Each test class instantiates its own isolated database via `Guid.NewGuid()` as the database name.

The test project references the main project directly (`ProjectReference`), so all production code is available without mocking.

### Adding New Screens

Follow `SKILL.md` order: Model → Service interface + implementation → ViewModel → View. Register new services in `ServiceCollectionExtensions.ConfigurarServicios()`.
