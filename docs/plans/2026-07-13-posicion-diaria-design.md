# Posición Diaria — Diseño

## Contexto

Pantalla nueva que muestra, por moneda, la posición de capital en las cajas de Efectivo al inicio y al final de un rango de fechas, y permite tipear una cotización contra el dólar para calcular la ganancia/pérdida en USD de cada moneda. Basado en una captura de un sistema anterior ("Diferencia de capital").

## Alcance

- Cap. Inicial / Cap. Final: suma de `Movimiento.Monto` en cuentas `Tipo == "Efectivo"`, agrupado por moneda, cortando por fecha. No incluye cuentas Cliente/Banco (se puede ampliar a futuro si hace falta).
- Cotizaciones (Inicial/Final): las tipea el usuario en la pantalla, no se auto-completan.
- T.Pase (Multiplica/Divide): propiedad fija por moneda, configurable en la sección de Monedas de `ConfiguracionWindow`.
- Ganancia = USD Final − USD Inicial.

## Modelo de datos

- Nuevo campo `Moneda.TipoPase` (string, `"M"` o `"D"`, default `"D"`), columna `tipo_pase` en `monedas`.
- Migración EF: `AddColumn<string>("tipo_pase", table: "monedas", defaultValue: "D")`. Verificar el default generado antes de aplicar (ya hubo un caso en esta sesión donde el scaffold puso un default incorrecto).
- Actualizar `MonedaDto`, `CrearMonedaRequest`, `ActualizarMonedaRequest` con `TipoPase`.

## Servidor

- Nuevo endpoint `GET api/posicion-diaria?desde={fecha}&hasta={fecha}`.
- Nuevo DTO `PosicionDiariaDto { Codigo, Nombre, TipoPase, CapInicial, CapFinal }`.
- Cálculo: método privado reutilizado con distinto corte de fecha —
  `SUM(Movimiento.Monto) WHERE Movimiento.Cuenta.Tipo == "Efectivo" AND Movimiento.Moneda == codigo AND Fecha < corte`.
  - Cap Inicial: corte = `desde`.
  - Cap Final: corte = `hasta.AddDays(1)` (incluye todo el día "hasta").
- Solo monedas activas (`Moneda.Activa == true`).

## Cliente desktop

- `ConfiguracionWindow`: agregar selector M/D (ComboBox o RadioButtons) al crear/editar moneda, junto a Código/Nombre/Activa.
- Nueva ventana `PosicionDiariaWindow.axaml` / `.axaml.cs`, mismo estilo oscuro que Arqueo/Cierre de Caja. Acceso desde el sidebar (sin tecla F, todas ocupadas F1–F8).
  - Header: `DatePicker` Desde / Hasta (default hoy) + botón Buscar.
  - `DataGrid` con columnas: Código, Moneda, T.Pase, Cap.Inicial, Cot.Inicial (editable), USD Inicial (solo lectura), Cap.Final, Cot.Final (editable), USD Final (solo lectura), Ganancia (solo lectura, coloreada verde/rojo).
  - Cot.Inicial/Cot.Final: `DataGridTemplateColumn` con `TextBox`, recalculando con evento `KeyUp` (mismo patrón que `RecalcularCredito`/`RecalcularDebito` en Compra/Venta/Crédito-Débito) — no se usa binding reactivo tipo MVVM, se mantiene el estilo procedural ya establecido en el resto de la app.
  - Al recalcular una fila, refrescar el `ItemsSource` del grid (mismo patrón usado en `ToggleSinLimite_Changed` de ConfiguracionWindow).

## Casos borde

- Cotización vacía o inválida → 0 (mismo criterio que `MontoHelper.Parsear`).
- "Hasta" anterior a "Desde" → aviso, no ejecuta búsqueda.
- Sin monedas activas → grid vacío con mensaje "sin datos".

## Testing

- Test de servidor (xUnit) verificando que Cap. Inicial/Final cortan correctamente por fecha con movimientos de ejemplo en varias monedas y cuentas (incluyendo una cuenta no-Efectivo, para confirmar que se excluye).
- Sin test para el recálculo de UI (lógica simple client-side, mismo criterio que el resto de los recálculos de la app, que tampoco tienen test).
