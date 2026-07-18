# Compra/Venta (Arbitraje) — Diseño

## Contexto

Nueva pantalla que ejecuta una Compra y una Venta simultáneas de la misma naturaleza contable, usando ARS como pivote: el monto en Pesos de la Compra debe ser exactamente igual al de la Venta (se cancelan entre sí), de modo que el único efecto neto real es el cambio de posición en la moneda extranjera entre las dos cotizaciones aplicadas (el spread del arbitraje/canje). Basado en una captura de un sistema anterior.

Nombre interno del feature: `Arbitraje` (clase, ventana, endpoint), para no confundirlo con las ventanas `CompraWindow`/`VentaWindow` existentes. El menú puede mostrar "Compra / Venta" si se quiere, pero el código usa "Arbitraje".

## Decisiones tomadas

- **Arquitectura**: ViewModel dedicado con CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`), a pedido explícito — aunque las ventanas de operaciones existentes (Compra/Venta/Crédito-Débito) usan code-behind puro sin ViewModel. Es una excepción consciente para esta pantalla.
- **"Tipo operación"**: no es selección de un cliente puntual (esa función se eliminó de la app por no ser funcional). Es una categoría fija: `ComboBox` con dos opciones, `CLIENTE` / `CASA`.
- **Backend**: endpoint nuevo y atómico, no dos llamadas separadas a Compra/Venta existentes. La atomicidad es central: como el punto de la pantalla es que ambas patas se cancelen en pesos, un guardado parcial (una pata sí, la otra no) rompe el balance que la función existe para garantizar.
- **Anulación**: anular una pata anula automáticamente la otra, vía un campo de vínculo entre las dos operaciones.

## Modelo de datos

`Operacion.OperacionParejaId` (`int?`, FK a sí misma) — mismo patrón que `OperacionOriginalId` ya existente. Migración: `AddColumn<int>("operacion_pareja_id", table: "operaciones", nullable: true)` + FK auto-referenciada. Sin default problemático (nullable, no hay bug de scaffold posible aquí).

## Backend

- `POST api/operaciones/arbitraje`, request `CrearArbitrajeRequest { MonedaCompra, CuentaAcreditaCompraId, MontoExtranjeroCompra, CotizacionCompra, MonedaVenta, CuentaDebitaVentaId, MontoExtranjeroVenta, CotizacionVenta, TipoOperacion, Observaciones }`.
- `OperacionService.GuardarArbitraje(...)`:
  1. Calcula Pesos de cada pata (`MontoExtranjero * Cotizacion`, redondeado igual que el resto del sistema: `Math.Round(..., 2, MidpointRounding.AwayFromZero)`).
  2. Valida `PesosCompra == PesosVenta` — si no, error server-side (defense in depth; el botón Aceptar ya lo impide en el cliente, pero el servidor no debe confiar solo en eso).
  3. Dentro de una única `db.Database.BeginTransaction()`: aplica la lógica de saldo/límite de deuda ya existente (reutilizando el mismo criterio de `ObtenerLimiteDeuda`/`ValidarMonoMonedaEfectivo` que usa `GuardarOperacion`) para la cuenta ARS pivote (afectada por ambas patas) y para las cuentas de moneda extranjera de cada pata.
  4. Crea 2 filas `Operacion` (`TipoOperacion="Compra"` y `"Venta"`), cada una con sus movimientos correspondientes (4 movimientos cada una, igual que `GuardarOperacion`), vinculadas por `OperacionParejaId` (se necesitan 2 `SaveChanges` dentro de la misma transacción: uno para obtener los Ids, otro para setear el vínculo cruzado).
  5. Si todo OK: `transaction.Commit()`, registra PPP para ambas patas (`RegistrarCompra` + `RegistrarVenta`), devuelve éxito con los 2 Ids.
  6. Si algo falla en cualquier punto: `transaction.Rollback()`, no queda nada guardado.
- `AnularOperacion(int id)`: después de crear la anulación de `id`, si `original.OperacionParejaId.HasValue` y esa pareja existe y no está ya anulada, se anula también dentro de la misma transacción (misma lógica de reversión ya existente, aplicada a ambas).

## Frontend

- `ViewModels/ArbitrajeViewModel.cs`: propiedades duplicadas para Compra y Venta (Moneda, MontoExtranjero, Cotización, Pesos, Cuenta — usando el mismo `CuentaMonedaTag`/`AutoCompleteBox` que ya usan Compra/Venta), más `Observaciones` y `TipoOperacion`. Propiedad calculada `PuedeAceptar` (true solo si ambos montos > 0 y `PesosCompra == PesosVenta`), recalculada en cada cambio relevante.
- `Views/ArbitrajeWindow.axaml`: dos `Border` (Compra arriba con acento azul, Venta abajo con acento rojo), mismo estilo visual que Compra/Venta actuales. Footer con Observaciones, TipoOperación, botones Aceptar/Cancelar. Acceso desde el sidebar de `MainWindow`, sin tecla F dedicada (F1–F8 ocupadas).

### Cálculo reactivo sin loops

Cada sección (Compra, Venta) tiene 3 campos interdependientes: MontoExtranjero, Cotización, Pesos (cualquiera se puede derivar de los otros dos). Se usa un flag booleano `_recalculandoCompra`/`_recalculandoVenta`: al asignar `Pesos` desde el cálculo automático (`MontoExtranjero * Cotizacion`), se activa el flag antes de asignar y se desactiva después: el `partial void On...Changed` de `Pesos` chequea el flag y no dispara un recálculo hacia atrás si la actualización vino de ahí mismo. Mismo patrón que ya usa `ConfiguracionWindow.axaml.cs` con `_actualizandoDesdeCombo`.

## Testing

Tests de servidor (xUnit, `TestDbContextFactory`) para `GuardarArbitraje`:
- Rechaza si `PesosCompra != PesosVenta`.
- Atomicidad: fuerza un fallo en la segunda pata (ej. cuenta inexistente) y verifica que no queda ninguna `Operacion` ni `Movimiento` guardado de la primera pata tampoco.
- Éxito: verifica los 2 `Operacion` creados, vinculados por `OperacionParejaId`, con los 8 movimientos correctos y los saldos actualizados.
- Anulación en cascada: anular una pata anula la otra automáticamente.
