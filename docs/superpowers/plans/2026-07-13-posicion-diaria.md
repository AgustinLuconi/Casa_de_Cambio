# Posición Diaria Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nueva pantalla "Posición Diaria" que muestra, por moneda, la posición de capital en las cajas de Efectivo al inicio y al final de un rango de fechas, con cotizaciones editables que calculan en vivo la ganancia/pérdida en USD.

**Architecture:** Nuevo endpoint de servidor (`GET api/posicion-diaria`) que calcula Cap. Inicial/Final sumando `Movimiento.Monto` de cuentas tipo Efectivo, cortado por fecha. Nuevo campo `Moneda.TipoPase` ("M"/"D") configurable desde la gestión de monedas existente en `ConfiguracionWindow`. Nueva ventana desktop con un `DataGrid` cuyas filas son objetos `ObservableObject` (CommunityToolkit.Mvvm) — al escribir una cotización, la fila se recalcula sola (sin tocar el `ItemsSource` del grid), evitando el problema de foco que tendría un refresco completo del grid en cada tecla.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core + Npgsql, Avalonia UI, CommunityToolkit.Mvvm (ya referenciado en el proyecto).

## Global Constraints

- Desktop (`SistemaCambio.csproj`) NO tiene `ImplicitUsings` habilitado — agregar `using System;` etc. explícitos en cualquier archivo nuevo del desktop.
- Server y Shared SÍ tienen implicit usings.
- Antes de aplicar cualquier migración EF a Supabase: generar con `dotnet ef migrations add`, LEER el archivo generado, y corregir cualquier `defaultValue` que no coincida con el valor por defecto real deseado (ya pasó dos veces en esta sesión: un booleano generado con `defaultValue: false` cuando el modelo quería `true`, y hay que verificar lo mismo para el string nuevo).
- Aplicar el SQL de la migración directamente contra Supabase vía MCP `execute_sql`, y registrar la migración en `__EFMigrationsHistory` (mismo mecanismo ya usado en esta sesión — `dotnet ef database update` no tiene conexión local funcional).
- Todo commit debe pasar `dotnet build Sistema_Casa_Cambio.sln` (0 errores) y `dotnet test` en ambos proyectos de test antes de darse por terminado.

---

### Task 1: Campo TipoPase en Moneda (modelo, DTO, requests, migración)

**Files:**
- Modify: `src/CasaCambio.Server/Models/Moneda.cs`
- Modify: `src/CasaCambio.Shared/DTOs/MonedaDto.cs`
- Modify: `src/CasaCambio.Shared/Requests/CrearMonedaRequest.cs`
- Modify: `src/CasaCambio.Shared/Requests/ActualizarMonedaRequest.cs`
- Modify: `src/CasaCambio.Server/Controllers/MonedasController.cs`
- Create: `src/CasaCambio.Server/Migrations/<timestamp>_AgregarTipoPaseAMoneda.cs` (generado por EF, luego corregido)

**Interfaces:**
- Produce: `Moneda.TipoPase` (string, "M" o "D", default "D"), disponible para el resto de las tareas vía `MonedaDto.TipoPase`.

- [ ] **Step 1: Agregar el campo al modelo**

En `src/CasaCambio.Server/Models/Moneda.cs`, agregar después de `Activa`:

```csharp
[Column("tipo_pase")] public string TipoPase { get; set; } = "D";
```

- [ ] **Step 2: Agregar el campo a MonedaDto**

En `src/CasaCambio.Shared/DTOs/MonedaDto.cs`, agregar:

```csharp
public string TipoPase { get; set; } = "D";
```

- [ ] **Step 3: Agregar el campo a los requests**

En `src/CasaCambio.Shared/Requests/CrearMonedaRequest.cs`, agregar:

```csharp
public string TipoPase { get; set; } = "D";
```

En `src/CasaCambio.Shared/Requests/ActualizarMonedaRequest.cs`, agregar:

```csharp
public string TipoPase { get; set; } = "D";
```

- [ ] **Step 4: Propagar el campo en MonedasController**

En `src/CasaCambio.Server/Controllers/MonedasController.cs`, actualizar las tres construcciones de `MonedaDto` y `Models.Moneda` para incluir `TipoPase`:

```csharp
[HttpGet]
public IActionResult GetMonedas()
{
    using var db = _contextFactory.CreateDbContext();
    var monedas = db.Monedas.Where(m => m.Activa).AsNoTracking().ToList();
    return Ok(monedas.Select(m => new MonedaDto { Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa, TipoPase = m.TipoPase }));
}

[HttpPost]
public IActionResult CrearMoneda([FromBody] CrearMonedaRequest req)
{
    using var db = _contextFactory.CreateDbContext();
    var moneda = new Models.Moneda { Codigo = req.Codigo, Nombre = req.Nombre, Activa = true, TipoPase = string.IsNullOrEmpty(req.TipoPase) ? "D" : req.TipoPase };
    db.Monedas.Add(moneda);
    db.SaveChanges();
    return CreatedAtAction(nameof(GetMonedas), new { id = moneda.Id }, new MonedaDto { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa, TipoPase = moneda.TipoPase });
}

[HttpPut("{id}")]
public IActionResult ActualizarMoneda(int id, [FromBody] ActualizarMonedaRequest req)
{
    using var db = _contextFactory.CreateDbContext();
    var moneda = db.Monedas.FirstOrDefault(m => m.Id == id);
    if (moneda == null)
        return NotFound($"Moneda {id} no encontrada.");
    moneda.Codigo = req.Codigo.Trim().ToUpper();
    moneda.Nombre = req.Nombre.Trim();
    moneda.Activa = req.Activa;
    moneda.TipoPase = string.IsNullOrEmpty(req.TipoPase) ? "D" : req.TipoPase;
    db.SaveChanges();
    return Ok(new MonedaDto { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa, TipoPase = moneda.TipoPase });
}
```

(El método `EliminarMoneda` no cambia.)

- [ ] **Step 5: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Server/CasaCambio.Server.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 6: Generar la migración**

Run:
```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio && dotnet ef migrations add AgregarTipoPaseAMoneda --project src/CasaCambio.Server/CasaCambio.Server.csproj --startup-project src/CasaCambio.Server/CasaCambio.Server.csproj
```
Expected: `Done. To undo this action, use 'ef migrations remove'` sin warning de pérdida de datos.

- [ ] **Step 7: Verificar y corregir el defaultValue de la migración**

Abrir el archivo `src/CasaCambio.Server/Migrations/<timestamp>_AgregarTipoPaseAMoneda.cs` generado. Va a tener algo como:

```csharp
migrationBuilder.AddColumn<string>(
    name: "tipo_pase",
    table: "monedas",
    type: "text",
    nullable: false,
    defaultValue: "");
```

Cambiar `defaultValue: ""` por `defaultValue: "D"` (el modelo por defecto es `"D"`, y todas las monedas existentes deben quedar con ese valor al agregar la columna, no con string vacío):

```csharp
migrationBuilder.AddColumn<string>(
    name: "tipo_pase",
    table: "monedas",
    type: "text",
    nullable: false,
    defaultValue: "D");
```

- [ ] **Step 8: Build tras corregir la migración**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Server/CasaCambio.Server.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 9: Aplicar el cambio de schema a Supabase**

Usar la herramienta MCP de Supabase (`execute_sql`, project_id `vtyaunxljytbxbgyhmaz`) con:

```sql
ALTER TABLE monedas ADD COLUMN tipo_pase text NOT NULL DEFAULT 'D';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
('<timestamp>_AgregarTipoPaseAMoneda', '8.0.0')
ON CONFLICT DO NOTHING;
```
(reemplazar `<timestamp>` por el nombre exacto del archivo de migración generado en el Step 6).

- [ ] **Step 10: Verificar que las monedas existentes quedaron con tipo_pase='D'**

Query de verificación:
```sql
SELECT codigo, tipo_pase FROM monedas ORDER BY codigo;
```
Expected: todas las filas con `tipo_pase = 'D'`.

- [ ] **Step 11: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Server/Models/Moneda.cs src/CasaCambio.Shared/DTOs/MonedaDto.cs src/CasaCambio.Shared/Requests/CrearMonedaRequest.cs src/CasaCambio.Shared/Requests/ActualizarMonedaRequest.cs src/CasaCambio.Server/Controllers/MonedasController.cs src/CasaCambio.Server/Migrations/
git commit -m "feat: agregar TipoPase (M/D) a Moneda para Posición Diaria"
```

---

### Task 2: Endpoint de servidor GET api/posicion-diaria

**Files:**
- Create: `src/CasaCambio.Shared/DTOs/PosicionDiariaDto.cs`
- Create: `src/CasaCambio.Server/Controllers/PosicionDiariaController.cs`
- Create: `src/CasaCambio.Tests/PosicionDiariaControllerTests.cs`

**Interfaces:**
- Consumes: `Moneda.TipoPase` (Task 1), `Movimiento.CuentaId/Moneda/Monto/Fecha`, `Cuenta.Tipo`.
- Produces: `PosicionDiariaDto { Codigo, Nombre, TipoPase, CapInicial, CapFinal }`, endpoint `GET api/posicion-diaria?desde={fecha}&hasta={fecha}` devolviendo `List<PosicionDiariaDto>`, usado por Task 3.

- [ ] **Step 1: Crear el DTO**

Crear `src/CasaCambio.Shared/DTOs/PosicionDiariaDto.cs`:

```csharp
namespace CasaCambio.Shared.DTOs;

public class PosicionDiariaDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string TipoPase { get; set; } = "D";
    public decimal CapInicial { get; set; }
    public decimal CapFinal { get; set; }
}
```

- [ ] **Step 2: Escribir el test (falla porque el controller no existe todavía)**

Crear `src/CasaCambio.Tests/PosicionDiariaControllerTests.cs`:

```csharp
using CasaCambio.Server.Controllers;
using CasaCambio.Server.Models;
using CasaCambio.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CasaCambio.Tests;

public class PosicionDiariaControllerTests
{
    private readonly TestDbContextFactory _factory;
    private readonly PosicionDiariaController _controller;

    public PosicionDiariaControllerTests()
    {
        _factory = new TestDbContextFactory();
        _controller = new PosicionDiariaController(_factory);

        using var db = _factory.CreateDbContext();
        db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true, TipoPase = "D" });
        db.Monedas.Add(new Moneda { Id = 2, Codigo = "EUR", Nombre = "Euro", Activa = true, TipoPase = "M" });

        var cajaEfectivo = new Cuenta { Id = 1, Nombre = "EFECTIVO USD", Tipo = "Efectivo" };
        var cuentaCliente = new Cuenta { Id = 2, Nombre = "CLIENTE X", Tipo = "Cliente" };
        db.Cuentas.Add(cajaEfectivo);
        db.Cuentas.Add(cuentaCliente);

        var op = new Operacion { TipoOperacion = "Compra", MontoTotalOrigen = 1m, MontoTotalDestino = 1m, CotizacionAplicada = 1m };
        db.Operaciones.Add(op);

        // Antes del rango (desde=2026-06-10): debe entrar en Cap Inicial y Cap Final
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 1, Moneda = "USD", Monto = 1000m, Fecha = new DateTime(2026, 6, 1) });
        // Dentro del rango (entre desde y hasta): entra solo en Cap Final
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 1, Moneda = "USD", Monto = 500m, Fecha = new DateTime(2026, 6, 15) });
        // Cuenta Cliente (no Efectivo): NO debe contarse en ningún lado
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 2, Moneda = "USD", Monto = 9999m, Fecha = new DateTime(2026, 6, 10) });

        db.SaveChanges();
    }

    [Fact]
    public void GetPosicionDiaria_CalculaCapInicialYFinal_SoloConCuentasEfectivo()
    {
        var result = _controller.GetPosicionDiaria(desde: new DateTime(2026, 6, 10), hasta: new DateTime(2026, 6, 20));

        var ok = Assert.IsType<OkObjectResult>(result);
        var posiciones = Assert.IsAssignableFrom<System.Collections.Generic.List<PosicionDiariaDto>>(ok.Value);

        var usd = Assert.Single(posiciones, p => p.Codigo == "USD");
        Assert.Equal(1000m, usd.CapInicial);
        Assert.Equal(1500m, usd.CapFinal);
        Assert.Equal("D", usd.TipoPase);
    }

    [Fact]
    public void GetPosicionDiaria_MonedaSinMovimientos_DevuelveCeros()
    {
        var result = _controller.GetPosicionDiaria(desde: new DateTime(2026, 6, 10), hasta: new DateTime(2026, 6, 20));

        var ok = Assert.IsType<OkObjectResult>(result);
        var posiciones = Assert.IsAssignableFrom<System.Collections.Generic.List<PosicionDiariaDto>>(ok.Value);

        var eur = Assert.Single(posiciones, p => p.Codigo == "EUR");
        Assert.Equal(0m, eur.CapInicial);
        Assert.Equal(0m, eur.CapFinal);
        Assert.Equal("M", eur.TipoPase);
    }
}
```

- [ ] **Step 3: Correr el test para verificar que falla**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~PosicionDiariaControllerTests" --nologo -v quiet`
Expected: FAIL (error de compilación, `PosicionDiariaController` no existe).

- [ ] **Step 4: Implementar el controller**

Crear `src/CasaCambio.Server/Controllers/PosicionDiariaController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PosicionDiariaController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public PosicionDiariaController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetPosicionDiaria([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        using var db = _contextFactory.CreateDbContext();
        var monedas = db.Monedas.Where(m => m.Activa).OrderBy(m => m.Codigo).AsNoTracking().ToList();
        var corteInicial = desde.Date;
        var corteFinal = hasta.Date.AddDays(1);

        var resultado = monedas.Select(m => new PosicionDiariaDto
        {
            Codigo = m.Codigo,
            Nombre = m.Nombre,
            TipoPase = m.TipoPase,
            CapInicial = SumaMovimientosEfectivo(db, m.Codigo, corteInicial),
            CapFinal = SumaMovimientosEfectivo(db, m.Codigo, corteFinal)
        }).ToList();

        return Ok(resultado);
    }

    private static decimal SumaMovimientosEfectivo(AppDbContext db, string moneda, DateTime corte)
    {
        return db.Movimientos
            .Where(mv => mv.Moneda == moneda && mv.Fecha < corte && mv.Cuenta.Tipo == "Efectivo")
            .Sum(mv => (decimal?)mv.Monto) ?? 0;
    }
}
```

- [ ] **Step 5: Correr el test para verificar que pasa**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --filter "FullyQualifiedName~PosicionDiariaControllerTests" --nologo -v quiet`
Expected: `Superado: 2, Con error: 0`

- [ ] **Step 6: Correr toda la suite de tests del servidor**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --nologo -v quiet`
Expected: todos los tests pasan (78 anteriores + 2 nuevos = 80).

- [ ] **Step 7: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Shared/DTOs/PosicionDiariaDto.cs src/CasaCambio.Server/Controllers/PosicionDiariaController.cs src/CasaCambio.Tests/PosicionDiariaControllerTests.cs
git commit -m "feat: endpoint GET api/posicion-diaria con cálculo de Cap. Inicial/Final"
```

---

### Task 3: Cliente API desktop

**Files:**
- Modify: `src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs`
- Modify: `src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs`

**Interfaces:**
- Consumes: `PosicionDiariaDto` (Task 2), endpoint `GET api/posicion-diaria`.
- Produces: `ICasaCambioApiClient.ObtenerPosicionDiariaAsync(DateTime desde, DateTime hasta)`, usado por Task 5.

- [ ] **Step 1: Agregar el método a la interfaz**

En `src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs`, agregar junto a los métodos de Monedas/Cuentas:

```csharp
Task<List<PosicionDiariaDto>> ObtenerPosicionDiariaAsync(DateTime desde, DateTime hasta);
```

- [ ] **Step 2: Implementar el método**

En `src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs`, agregar:

```csharp
public async Task<List<PosicionDiariaDto>> ObtenerPosicionDiariaAsync(DateTime desde, DateTime hasta)
    => await GetAuthenticatedAsync<List<PosicionDiariaDto>>($"api/posicion-diaria?desde={desde:O}&hasta={hasta:O}");
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Client.Core/CasaCambio.Client.Core.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add src/CasaCambio.Client.Core/ApiClient/ICasaCambioApiClient.cs src/CasaCambio.Client.Core/ApiClient/CasaCambioApiClient.cs
git commit -m "feat: cliente desktop para GET api/posicion-diaria"
```

---

### Task 4: Selector T.Pase en la gestión de Monedas (ConfiguracionWindow)

**Files:**
- Modify: `Views/ConfiguracionWindow.axaml`
- Modify: `Views/ConfiguracionWindow.axaml.cs`

**Interfaces:**
- Consumes: `MonedaDto.TipoPase` (Task 1), `CrearMonedaRequest.TipoPase`/`ActualizarMonedaRequest.TipoPase` (Task 1).

- [ ] **Step 1: Agregar la columna T.PASE al grid de monedas**

En `Views/ConfiguracionWindow.axaml`, dentro de `<DataGrid x:Name="dgMonedas">`, agregar una columna nueva entre "NOMBRE" y "ACTIVA" (la columna NOMBRE actual termina en la línea con `</DataGridTemplateColumn>` seguida de `<DataGridTemplateColumn Header="ACTIVA"...`):

```xml
<DataGridTemplateColumn Header="T.PASE" Width="90">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <ComboBox SelectedItem="{Binding TipoPase, Mode=TwoWay}"
                      HorizontalAlignment="Stretch" Margin="2">
                <ComboBox.Items>
                    <x:String>D</x:String>
                    <x:String>M</x:String>
                </ComboBox.Items>
            </ComboBox>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: Agregar el selector T.Pase al formulario de "Nueva Moneda"**

Buscar en `Views/ConfiguracionWindow.axaml` el bloque donde están `txtNuevoCodigo`/`txtNuevoNombre` (formulario de alta de moneda) y agregar al lado un ComboBox:

```xml
<ComboBox x:Name="cmbNuevoTipoPase" Width="80" SelectedIndex="0">
    <ComboBoxItem Content="D"/>
    <ComboBoxItem Content="M"/>
</ComboBox>
```

- [ ] **Step 3: Leer el nuevo selector al crear una moneda**

En `Views/ConfiguracionWindow.axaml.cs`, método `BtnNuevaMoneda_Click`, agregar el campo al request:

```csharp
private async void BtnNuevaMoneda_Click(object? sender, RoutedEventArgs e)
{
    var codigo = txtNuevoCodigo.Text?.Trim().ToUpper();
    var nombre = txtNuevoNombre.Text?.Trim();
    var tipoPase = (cmbNuevoTipoPase.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D";
    if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
    {
        await DialogHelper.MensajeAsync(this,"Error", "Debe ingresar el codigo y el nombre de la moneda.");
        return;
    }
    try
    {
        await _apiClient.CrearMonedaAsync(new CrearMonedaRequest { Codigo = codigo, Nombre = nombre, TipoPase = tipoPase });
        txtNuevoCodigo.Text = "";
        txtNuevoNombre.Text = "";
        CargarMonedasAsync();
    }
    catch (Exception ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
}
```

- [ ] **Step 4: Propagar TipoPase al guardar cambios**

En `Views/ConfiguracionWindow.axaml.cs`, método `BtnGuardarCambios_Click`, agregar `TipoPase = m.TipoPase` al request:

```csharp
await _apiClient.ActualizarMonedaAsync(m.Id, new ActualizarMonedaRequest { Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa, TipoPase = m.TipoPase });
```

- [ ] **Step 5: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 6: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add Views/ConfiguracionWindow.axaml Views/ConfiguracionWindow.axaml.cs
git commit -m "feat: selector T.Pase (M/D) en la gestión de Monedas de Configuración"
```

---

### Task 5: Ventana PosicionDiariaWindow

**Files:**
- Create: `Views/PosicionDiariaWindow.axaml`
- Create: `Views/PosicionDiariaWindow.axaml.cs`

**Interfaces:**
- Consumes: `ICasaCambioApiClient.ObtenerPosicionDiariaAsync` (Task 3), `PosicionDiariaDto` (Task 2), `MontoHelper.Parsear` (existente en `SistemaCambio.Services`), `NotificationService`, `DialogHelper`.
- Produces: clase `PosicionDiariaItem` (fila del grid, `ObservableObject`), consumida internamente por esta misma ventana.

- [ ] **Step 1: Crear el AXAML de la ventana**

Crear `Views/PosicionDiariaWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:SistemaCambio.Views.Controls"
        xmlns:helpers="clr-namespace:SistemaCambio.Views.Helpers"
        x:Class="SistemaCambio.Views.PosicionDiariaWindow"
        x:CompileBindings="False"
        Title="Posición Diaria"
        Width="1280" MinWidth="1000" Height="640" MinHeight="420"
        WindowStartupLocation="CenterOwner"
        CanResize="True"
        Background="{DynamicResource AppBackgroundBrush}">

    <Grid RowDefinitions="Auto, *, Auto" Margin="16">

        <controls:NotificationPanel x:Name="notificationPanel"
                                    Grid.RowSpan="3"
                                    VerticalAlignment="Top"
                                    HorizontalAlignment="Right"
                                    ZIndex="100"/>

        <!-- Header: rango de fechas -->
        <Border Grid.Row="0" Background="{DynamicResource CardBackgroundBrush}"
                BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
                CornerRadius="8" Padding="16,12" Margin="0,0,0,16">
            <StackPanel Orientation="Horizontal" Spacing="10">
                <TextBlock Text="Desde" VerticalAlignment="Center" Foreground="{DynamicResource SecondaryTextBrush}"/>
                <DatePicker x:Name="dpDesde" Width="180"/>
                <TextBlock Text="Hasta" VerticalAlignment="Center" Foreground="{DynamicResource SecondaryTextBrush}" Margin="10,0,0,0"/>
                <DatePicker x:Name="dpHasta" Width="180"/>
                <Button Content="Buscar" Classes="Primary" Margin="10,0,0,0"
                        HorizontalContentAlignment="Center" VerticalContentAlignment="Center"
                        Click="BtnBuscar_Click"/>
            </StackPanel>
        </Border>

        <!-- Grid de posición -->
        <Border Grid.Row="1" Background="{DynamicResource CardBackgroundBrush}"
                BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1" CornerRadius="8" Padding="8">
            <Grid RowDefinitions="*, Auto">
                <DataGrid Grid.Row="0" x:Name="dgPosicion" AutoGenerateColumns="False" IsReadOnly="True"
                          BorderThickness="1" BorderBrush="{DynamicResource BorderBrush}"
                          GridLinesVisibility="Horizontal"
                          HorizontalGridLinesBrush="{DynamicResource BorderBrush}"
                          HeadersVisibility="Column">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="CÓDIGO" Binding="{Binding Codigo}" Width="70"/>
                        <DataGridTextColumn Header="MONEDA" Binding="{Binding Nombre}" Width="140"/>
                        <DataGridTextColumn Header="T.PASE" Binding="{Binding TipoPase}" Width="70"/>
                        <DataGridTextColumn Header="CAP. INICIAL" Binding="{Binding CapInicial, StringFormat='{}{0:N2}'}" Width="120"/>
                        <DataGridTemplateColumn Header="COT. INICIAL" Width="110">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <TextBox Text="{Binding CotInicialTexto, Mode=TwoWay}"
                                             Background="Transparent" BorderThickness="0"
                                             TextAlignment="Right" Padding="6,3"
                                             helpers:NumericInput.Enabled="True"
                                             Foreground="{DynamicResource PrimaryTextBrush}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                        <DataGridTextColumn Header="USD INICIAL" Binding="{Binding UsdInicialFormatted}" Width="110"/>
                        <DataGridTextColumn Header="CAP. FINAL" Binding="{Binding CapFinal, StringFormat='{}{0:N2}'}" Width="120"/>
                        <DataGridTemplateColumn Header="COT. FINAL" Width="110">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <TextBox Text="{Binding CotFinalTexto, Mode=TwoWay}"
                                             Background="Transparent" BorderThickness="0"
                                             TextAlignment="Right" Padding="6,3"
                                             helpers:NumericInput.Enabled="True"
                                             Foreground="{DynamicResource PrimaryTextBrush}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                        <DataGridTextColumn Header="USD FINAL" Binding="{Binding UsdFinalFormatted}" Width="110"/>
                        <DataGridTemplateColumn Header="GANANCIA" Width="120">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding GananciaFormatted}"
                                               Foreground="{Binding GananciaColor}"
                                               FontWeight="Bold" TextAlignment="Right"
                                               Padding="6,3" VerticalAlignment="Center"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>
                <TextBlock Grid.Row="0" x:Name="txtSinDatos" Text="No hay monedas activas para mostrar."
                           Foreground="{DynamicResource SecondaryTextBrush}"
                           HorizontalAlignment="Center" VerticalAlignment="Center" IsVisible="False"/>
            </Grid>
        </Border>

        <!-- Footer -->
        <Grid Grid.Row="2" Margin="0,16,0,0">
            <Button HorizontalAlignment="Right" Content="Cerrar" Classes="Secondary" Width="100" Height="36"
                    HorizontalContentAlignment="Center" VerticalContentAlignment="Center"
                    Click="BtnCerrar_Click"/>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 2: Crear el code-behind con la clase de fila observable**

Crear `Views/PosicionDiariaWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaCambio.Views
{
    public partial class PosicionDiariaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private ISolidColorBrush? _successBrush;
        private ISolidColorBrush? _dangerBrush;
        private ISolidColorBrush? _neutralBrush;

        public ObservableCollection<PosicionDiariaItem> Items { get; } = new();

        public PosicionDiariaWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            dpDesde.SelectedDate = new DateTimeOffset(DateTime.Today);
            dpHasta.SelectedDate = new DateTimeOffset(DateTime.Today);
            _successBrush = (ISolidColorBrush)this.FindResource("SuccessBrush")!;
            _dangerBrush = (ISolidColorBrush)this.FindResource("DangerBrush")!;
            _neutralBrush = (ISolidColorBrush)this.FindResource("PrimaryTextBrush")!;
            dgPosicion.ItemsSource = Items;

            _ = BuscarAsync();
        }

        private async void BtnBuscar_Click(object? sender, RoutedEventArgs e) => await BuscarAsync();

        private async Task BuscarAsync()
        {
            var desde = dpDesde.SelectedDate?.DateTime ?? DateTime.Today;
            var hasta = dpHasta.SelectedDate?.DateTime ?? DateTime.Today;
            if (hasta.Date < desde.Date)
            {
                NotificationService.Warning("Rango inválido", "La fecha 'Hasta' no puede ser anterior a 'Desde'.");
                return;
            }

            try
            {
                var posiciones = await _apiClient.ObtenerPosicionDiariaAsync(desde, hasta);
                Items.Clear();
                foreach (var p in posiciones)
                {
                    Items.Add(new PosicionDiariaItem
                    {
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        TipoPase = p.TipoPase,
                        CapInicial = p.CapInicial,
                        CapFinal = p.CapFinal,
                        SuccessBrush = _successBrush,
                        DangerBrush = _dangerBrush,
                        NeutralBrush = _neutralBrush
                    });
                }
                txtSinDatos.IsVisible = Items.Count == 0;
            }
            catch (Exception ex) { NotificationService.Error("Error", ex.Message); }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }

    public partial class PosicionDiariaItem : ObservableObject
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string TipoPase { get; set; } = "D";
        public decimal CapInicial { get; set; }
        public decimal CapFinal { get; set; }

        public ISolidColorBrush? SuccessBrush { get; set; }
        public ISolidColorBrush? DangerBrush { get; set; }
        public ISolidColorBrush? NeutralBrush { get; set; }

        [ObservableProperty] private string _cotInicialTexto = "0.00000";
        [ObservableProperty] private string _cotFinalTexto = "0.00000";
        [ObservableProperty] private string _usdInicialFormatted = "0.00";
        [ObservableProperty] private string _usdFinalFormatted = "0.00";
        [ObservableProperty] private string _gananciaFormatted = "0.00";
        [ObservableProperty] private ISolidColorBrush? _gananciaColor;

        partial void OnCotInicialTextoChanged(string value) => Recalcular();
        partial void OnCotFinalTextoChanged(string value) => Recalcular();

        private void Recalcular()
        {
            decimal cotInicial = MontoHelper.Parsear(CotInicialTexto);
            decimal cotFinal = MontoHelper.Parsear(CotFinalTexto);
            decimal usdInicial = ConvertirAUsd(CapInicial, cotInicial, TipoPase);
            decimal usdFinal = ConvertirAUsd(CapFinal, cotFinal, TipoPase);
            decimal ganancia = usdFinal - usdInicial;

            UsdInicialFormatted = usdInicial.ToString("N2");
            UsdFinalFormatted = usdFinal.ToString("N2");
            GananciaFormatted = ganancia.ToString("N2");
            GananciaColor = ganancia > 0 ? SuccessBrush : (ganancia < 0 ? DangerBrush : NeutralBrush);
        }

        private static decimal ConvertirAUsd(decimal capital, decimal cotizacion, string tipoPase)
        {
            if (cotizacion == 0) return 0;
            return tipoPase == "M" ? capital * cotizacion : capital / cotizacion;
        }
    }
}
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add Views/PosicionDiariaWindow.axaml Views/PosicionDiariaWindow.axaml.cs
git commit -m "feat: ventana Posición Diaria con recálculo en vivo de ganancia en USD"
```

---

### Task 6: Acceso desde el sidebar de MainWindow

**Files:**
- Modify: `Views/MainWindow.axaml`
- Modify: `Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `PosicionDiariaWindow` (Task 5).

- [ ] **Step 1: Agregar el botón al sidebar**

En `Views/MainWindow.axaml`, agregar un botón nuevo justo después del botón "Reportes" (que usa `Click="BtnReportes_Click"`) y antes de "Configuración":

```xml
<Button Classes="SidebarButton" Click="BtnPosicionDiaria_Click">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <icon:MaterialIcon Kind="CurrencyUsd" Width="18" Height="18"/>
        <TextBlock Text="Posición Diaria" VerticalAlignment="Center" FontSize="13"/>
    </StackPanel>
</Button>
```

- [ ] **Step 2: Agregar el handler y el método de apertura**

En `Views/MainWindow.axaml.cs`, junto a `BtnReportes_Click`/`AbrirReportesWindow`:

```csharp
private async void BtnPosicionDiaria_Click(object? sender, RoutedEventArgs e) => await AbrirPosicionDiariaWindow();
```

Y junto a `AbrirReportesWindow`:

```csharp
private async Task AbrirPosicionDiariaWindow() { var w = new PosicionDiariaWindow(); await w.ShowDialog(this); }
```

- [ ] **Step 3: Build para confirmar que compila**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/SistemaCambio.csproj --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 4: Commit**

```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git add Views/MainWindow.axaml Views/MainWindow.axaml.cs
git commit -m "feat: acceso a Posición Diaria desde el sidebar"
```

---

### Task 7: Verificación final y deploy

**Files:** ninguno (solo verificación y despliegue)

- [ ] **Step 1: Build completo de la solución**

Run: `dotnet build /home/agustin/PROYECTOS/Sistema_Casa_Cambio/Sistema_Casa_Cambio.sln --nologo -v quiet`
Expected: `0 Errores`

- [ ] **Step 2: Correr toda la suite de tests del servidor**

Run: `dotnet test /home/agustin/PROYECTOS/Sistema_Casa_Cambio/src/CasaCambio.Tests/CasaCambio.Tests.csproj --nologo -v quiet`
Expected: todos pasan (80 esperados: 78 previos + 2 de esta feature).

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
