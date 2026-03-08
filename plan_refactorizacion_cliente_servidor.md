# Plan de Ejecución Técnico: Refactorización a Cliente-Servidor con Offline-First

> **Proyecto:** Sistema Casa de Cambio  
> **Fecha:** 7 de Marzo de 2026  
> **Autor:** Arquitecto de Soluciones Senior  
> **Destinatario:** Agente autónomo de código (Claude Code / Gemini Code Assist)  
> **Repositorio:** `AgustinLuconi/Casa_de_Cambio`

---

## 1. Diagnóstico del Estado Actual

### 1.1 Stack Tecnológico Actual

| Componente | Tecnología | Versión |
|---|---|---|
| Framework UI | Avalonia UI | 11.0.6 |
| Runtime | .NET | 8.0 |
| ORM | Entity Framework Core | 8.0.0 |
| Base de Datos | PostgreSQL (local) | — |
| Proveedor DB | Npgsql | 8.0.0 |
| MVVM | CommunityToolkit.Mvvm | 8.2.2 |
| DI | Microsoft.Extensions.DependencyInjection | 8.0.0 |
| UI Theme | Material.Avalonia | 3.1.1 |
| Gráficos | ScottPlot.Avalonia | 5.0.42 |

### 1.2 Inventario de Entidades del Dominio (13 tablas)

| Tabla PostgreSQL | Modelo C# | Rol |
|---|---|---|
| `cuentas` | `Cuenta` | Cuentas contables (Caja, Externo, etc.) |
| `saldos_cuenta` | `SaldoCuenta` | Saldos por moneda por cuenta |
| `clientes` | `Cliente` | Clientes de la casa de cambio |
| `operaciones` | `Operacion` | Operaciones de compra/venta/crédito |
| `movimientos` | `Movimiento` | Patas contables (partida doble) |
| `monedas` | `Moneda` | Divisas configuradas |
| `cotizaciones_diarias` | `CotizacionDiaria` | Cotizaciones de referencia |
| `arqueos` | `Arqueo` | Arqueos ciegos de caja |
| `cierres_caja` | `CierreCaja` | Cierre diario de operaciones |
| `estados_caja` | `EstadoCaja` | Apertura/cierre de cajas |
| `tenencias_moneda` | `TenenciaMoneda` | PPP (Precio Promedio Ponderado) |
| `audit_logs` | `AuditLog` | Pista de auditoría |
| — | `ValidationResult` | No persistido (modelo de dominio) |
| — | `NotificationMessage` | No persistido (modelo de UI) |

### 1.3 Inventario de Servicios de Negocio (7 servicios + 2 validadores)

| Servicio | Interfaz | Responsabilidad | Acoplamiento a EF |
|---|---|---|---|
| `OperacionService` | `IOperacionService` | Compra, venta, crédito/débito, interbancaria | **ALTO** — usa `IDbContextFactory` directamente |
| `ArqueoService` | `IArqueoService` | Arqueo ciego + ajuste automático | **ALTO** |
| `CierreCajaService` | `ICierreCajaService` | Cierre diario, bloqueo de día | **ALTO** |
| `PPPService` | `IPPPService` | Precio Promedio Ponderado | **ALTO** |
| `AuditService` | `IAuditService` | Registro de auditoría | **ALTO** |
| `DashboardService` | `IDashboardService` | Métricas y gráficos | **ALTO** |
| `QueryService` | `IQueryService` | Consultas optimizadas, paginación | **ALTO** |
| `OperacionValidator` | — (clase directa) | Validación pre-operación | **ALTO** |
| `ArqueoValidator` | — (clase directa) | Validación de arqueos | **ALTO** |

### 1.4 Acoplamientos Críticos Identificados

> [!WARNING]
> Las vistas (`CompraWindow`, `VentaWindow`, etc.) acceden directamente al `IDbContextFactory<AppDbContext>` para poblar combos, cargar cotizaciones, y leer datos de referencia. Esto constituye un doble acoplamiento: **View → EF Core** y **View → Services**.

```mermaid
graph LR
    subgraph "MONOLITO ACTUAL"
        V[Views<br/>CompraWindow<br/>VentaWindow<br/>etc.] --> S[Services<br/>OperacionService<br/>ArqueoService<br/>etc.]
        V --> DB[(PostgreSQL<br/>Local)]
        S --> DB
    end
    style DB fill:#e74c3c,color:#fff
```

**Problema concreto en vistas:**
- `CompraWindow.cs`, `VentaWindow.cs`: inyectan `IDbContextFactory<AppDbContext>` para cargar monedas, cuentas, y cotizaciones
- `MainWindow.cs`: usa DbContextFactory para el dashboard
- `ConfiguracionWindow.cs`: CRUD directo contra EF para monedas y cuentas

---

## 2. Arquitectura Objetivo

```mermaid
graph TB
    subgraph "NUBE"
        API["ASP.NET Core Web API<br/>(Supabase-hosted PostgreSQL)"]
        DB[(Supabase<br/>PostgreSQL)]
        API --> DB
    end

    subgraph "CLIENTE ESCRITORIO"
        UI["Avalonia UI<br/>(Views + ViewModels)"]
        HC["ApiClient<br/>(HttpClient)"]
        SYNC["SyncService<br/>(Background Worker)"]
        SQLITE[(SQLite<br/>Local Cache)]
        
        UI --> HC
        UI --> SQLITE
        HC --> API
        SYNC --> SQLITE
        SYNC --> HC
    end

    style DB fill:#3ecf8e,color:#fff
    style SQLITE fill:#2980b9,color:#fff
    style API fill:#8e44ad,color:#fff
```

---

## 3. Estructura de la Solución (5 Proyectos)

```
Sistema_Casa_Cambio/
├── Sistema_Casa_Cambio.sln
│
├── src/
│   ├── CasaCambio.Shared/                 ← PROYECTO 1: Biblioteca de Contratos
│   │   ├── CasaCambio.Shared.csproj       (netstandard2.1 o net8.0)
│   │   ├── DTOs/
│   │   │   ├── OperacionDto.cs
│   │   │   ├── CuentaDto.cs
│   │   │   ├── SaldoCuentaDto.cs
│   │   │   ├── MonedaDto.cs
│   │   │   ├── CotizacionDto.cs
│   │   │   ├── ClienteDto.cs
│   │   │   ├── ArqueoDto.cs
│   │   │   ├── CierreCajaDto.cs
│   │   │   ├── AuditLogDto.cs
│   │   │   └── DashboardDto.cs
│   │   ├── Requests/
│   │   │   ├── CrearOperacionRequest.cs
│   │   │   ├── CrearArqueoRequest.cs
│   │   │   ├── CrearCierreCajaRequest.cs
│   │   │   ├── LoginRequest.cs
│   │   │   └── RegistrarCompraRequest.cs
│   │   ├── Responses/
│   │   │   ├── OperacionResponse.cs
│   │   │   ├── PaginatedResponse.cs
│   │   │   ├── AuthResponse.cs
│   │   │   └── ApiErrorResponse.cs
│   │   └── Enums/
│   │       ├── TipoOperacion.cs
│   │       ├── EstadoSincronizacion.cs
│   │       └── TipoMoneda.cs
│   │
│   ├── CasaCambio.Server/                 ← PROYECTO 2: Web API
│   │   ├── CasaCambio.Server.csproj       (net8.0, Microsoft.NET.Sdk.Web)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs             (migrado desde Models/)
│   │   │   └── Migrations/                (migradas)
│   │   ├── Models/                         (entidades EF, migradas del monolito)
│   │   ├── Services/                       (migrados del monolito)
│   │   ├── Validators/                     (migrados)
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── OperacionesController.cs
│   │   │   ├── CuentasController.cs
│   │   │   ├── MonedasController.cs
│   │   │   ├── CotizacionesController.cs
│   │   │   ├── ArqueoController.cs
│   │   │   ├── CierreCajaController.cs
│   │   │   ├── ClientesController.cs
│   │   │   ├── PPPController.cs
│   │   │   ├── DashboardController.cs
│   │   │   └── SyncController.cs           ★ Endpoint de sincronización offline
│   │   ├── Auth/
│   │   │   ├── JwtSettings.cs
│   │   │   ├── JwtService.cs
│   │   │   └── UsuarioSeed.cs
│   │   └── Middleware/
│   │       └── ExceptionMiddleware.cs
│   │
│   ├── CasaCambio.Desktop/               ← PROYECTO 3: Cliente Avalonia (refactorizado)
│   │   ├── CasaCambio.Desktop.csproj
│   │   ├── Program.cs
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── Views/                          (se mantienen, pero sin EF)
│   │   ├── ViewModels/
│   │   ├── ApiClient/
│   │   │   ├── ICasaCambioApiClient.cs
│   │   │   ├── CasaCambioApiClient.cs      (HttpClient wrapper)
│   │   │   └── AuthTokenStore.cs
│   │   ├── LocalDb/
│   │   │   ├── LocalDbContext.cs            (SQLite)
│   │   │   ├── LocalOperacion.cs            (entidad local con estado sync)
│   │   │   └── Migrations/
│   │   ├── Sync/
│   │   │   ├── SyncService.cs               ★ Motor de sincronización
│   │   │   ├── SyncStatus.cs
│   │   │   └── ConnectivityChecker.cs
│   │   └── Services/
│   │       └── OfflineOperacionService.cs   ★ Decide: API o SQLite
│   │
│   └── CasaCambio.Tests/                  ← PROYECTO 4: Tests (migrado)
│       ├── CasaCambio.Tests.csproj
│       ├── Server/
│       │   ├── OperacionServiceTests.cs
│       │   ├── ArqueoServiceTests.cs
│       │   └── ...
│       └── Desktop/
│           ├── SyncServiceTests.cs
│           └── OfflineOperacionTests.cs
```

> [!IMPORTANT]
> **`CasaCambio.Shared`** es la pieza clave. Tanto el servidor como el cliente lo referencian. Contiene los DTOs (Data Transfer Objects) que viajan por HTTP. **Nunca entidades de EF Core.**

---

## 4. Estrategia de Migración de Datos a Supabase

### 4.1 Crear proyecto en Supabase

1. Ir a [supabase.com](https://supabase.com) → Crear proyecto
2. Seleccionar región cercana (ej: `South America (São Paulo)`)
3. Anotar:
   - **Project URL** → `https://<project-ref>.supabase.co`
   - **Connection String** → `postgresql://postgres.[ref]:[password]@aws-0-sa-east-1.pooler.supabase.com:6543/postgres`
   - **Anon Key** y **Service Key** (para acceso programático)

### 4.2 Migrar esquema y datos

> [!CAUTION]
> **No usar la migración de EF Core directamente contra Supabase en producción.** Primero exportar el esquema local, verificar, y luego aplicar.

**Paso 1 — Exportar esquema desde PostgreSQL local:**
```bash
pg_dump -h localhost -U postgres -d SistemaCambio --schema-only -f schema_export.sql
```

**Paso 2 — Exportar datos:**
```bash
pg_dump -h localhost -U postgres -d SistemaCambio --data-only -f data_export.sql
```

**Paso 3 — Importar en Supabase:**
```bash
# Obtener el connection string de Supabase Dashboard → Settings → Database
psql "postgresql://postgres.[ref]:[password]@aws-0-sa-east-1.pooler.supabase.com:6543/postgres" -f schema_export.sql
psql "postgresql://postgres.[ref]:[password]@aws-0-sa-east-1.pooler.supabase.com:6543/postgres" -f data_export.sql
```

**Paso 4 — Ajustar `appsettings.json` del servidor:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-0-sa-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.[ref];Password=[password];SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

**Paso 5 — Verificar que EF genera migraciones limpias:**
```bash
cd src/CasaCambio.Server
dotnet ef migrations add InitialMigration --context AppDbContext
dotnet ef database update --context AppDbContext
```

### 4.3 Agregar tabla de usuarios (nueva)

Crear una migración manual para la tabla de usuarios del sistema:

```sql
-- Agregar al esquema de Supabase
CREATE TABLE usuarios (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,
    nombre_completo VARCHAR(100) NOT NULL,
    rol VARCHAR(20) NOT NULL DEFAULT 'Cajero',  -- 'Admin', 'Cajero'
    activo BOOLEAN NOT NULL DEFAULT true,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW()
);
```

---

## 5. Diseño de la Web API (Capa Servidor)

### 5.1 Endpoints REST

| Método | Ruta | Controller | Descripción |
|---|---|---|---|
| `POST` | `/api/auth/login` | `AuthController` | Login, devuelve JWT |
| `POST` | `/api/auth/refresh` | `AuthController` | Refresh token |
| | | | |
| `GET` | `/api/operaciones` | `OperacionesController` | Lista con filtros y paginación |
| `GET` | `/api/operaciones/{id}` | `OperacionesController` | Detalle de operación |
| `POST` | `/api/operaciones/compra` | `OperacionesController` | Registrar compra |
| `POST` | `/api/operaciones/venta` | `OperacionesController` | Registrar venta |
| `POST` | `/api/operaciones/credito-debito` | `OperacionesController` | Crédito/débito |
| `POST` | `/api/operaciones/interbancaria` | `OperacionesController` | Interbancaria |
| | | | |
| `GET` | `/api/cuentas` | `CuentasController` | Lista cuentas con saldos |
| `POST` | `/api/cuentas` | `CuentasController` | Crear cuenta |
| `GET` | `/api/cuentas/{id}/movimientos` | `CuentasController` | Movimientos por cuenta |
| `GET` | `/api/cuentas/{id}/saldos` | `CuentasController` | Saldos por moneda |
| | | | |
| `GET` | `/api/monedas` | `MonedasController` | Lista monedas activas |
| `POST` | `/api/monedas` | `MonedasController` | Crear moneda |
| | | | |
| `GET` | `/api/cotizaciones/hoy` | `CotizacionesController` | Cotizaciones del día |
| `POST` | `/api/cotizaciones` | `CotizacionesController` | Guardar cotización |
| | | | |
| `POST` | `/api/arqueo` | `ArqueoController` | Realizar arqueo ciego |
| `GET` | `/api/cierre/hoy` | `CierreCajaController` | Cierre del día actual |
| `POST` | `/api/cierre/generar` | `CierreCajaController` | Generar cierre |
| `POST` | `/api/cierre/{id}/cerrar` | `CierreCajaController` | Cerrar definitivo |
| | | | |
| `GET` | `/api/ppp/{moneda}` | `PPPController` | PPP actual |
| `GET` | `/api/dashboard` | `DashboardController` | Métricas del dashboard |
| | | | |
| `POST` | `/api/sync/push` | `SyncController` | ★ Recibir operaciones offline |
| `GET` | `/api/sync/pull` | `SyncController` | ★ Descargar datos actualizados |

### 5.2 Autenticación JWT

```mermaid
sequenceDiagram
    participant C as Cliente Desktop
    participant A as API Server
    participant DB as Supabase

    C->>A: POST /api/auth/login {username, password}
    A->>DB: Verificar credenciales (BCrypt)
    DB-->>A: Usuario encontrado
    A-->>C: { token: "eyJ...", expiresAt: "...", refreshToken: "..." }
    
    Note over C: El cliente almacena el JWT<br/>en memoria + SecureStorage
    
    C->>A: GET /api/operaciones<br/>Authorization: Bearer eyJ...
    A-->>C: [operaciones]
    
    Note over C: Si el token expiró (401)
    C->>A: POST /api/auth/refresh {refreshToken}
    A-->>C: { token: "nuevo_token" }
```

**Configuración JWT recomendada:**
- **Access Token TTL:** 30 minutos
- **Refresh Token TTL:** 7 días  
- **Algoritmo:** HS256
- Los claims deben incluir: `userId`, `username`, `rol`

---

## 6. Arquitectura Offline-First y Sincronización

### 6.1 Modelo de Datos Local (SQLite)

El SQLite del cliente **NO** replica toda la base de datos. Solo almacena:

| Tabla Local SQLite | Propósito |
|---|---|
| `operaciones_pendientes` | ★ Operaciones creadas offline, pendientes de sincronización |
| `cache_cuentas` | Cache de cuentas y saldos (solo lectura) |
| `cache_monedas` | Cache de monedas activas (solo lectura) |
| `cache_cotizaciones` | Cache de cotizaciones del día |
| `sync_metadata` | Última fecha de sincronización, versión, etc. |
| `auth_session` | Token JWT y refresh token cifrados |

### 6.2 Entidad `LocalOperacion` (la pieza central del offline)

```
Tabla: operaciones_pendientes

Columnas:
  - Id                    (TEXT, PK, GUID generado localmente)
  - TipoOperacion         (TEXT: "Compra", "Venta", "Crédito/Débito")
  - CuentaOrigenId        (INTEGER)
  - CuentaDestinoId       (INTEGER)
  - MonedaOrigen          (TEXT)
  - MonedaDestino         (TEXT)
  - MontoOrigen           (REAL)
  - MontoDestino          (REAL)
  - CotizacionAplicada    (REAL)
  - ClienteId             (INTEGER, nullable)
  - Observaciones         (TEXT)
  - FechaCreacionLocal    (TEXT, ISO 8601)
  - EstadoSync            (TEXT: "Pendiente", "Enviando", "Sincronizado", "Error")
  - ErrorSync             (TEXT, nullable, mensaje del último error)
  - IntentosSyncCount     (INTEGER, default 0)
  - ServerOperacionId     (INTEGER, nullable, ID asignado por el servidor al sincronizar)
```

> [!IMPORTANT]
> **¿Por qué GUID como PK local?** Porque el cliente genera el ID antes de que exista en el servidor. Esto previene colisiones de IDs numéricos entre múltiples terminales offline.

### 6.3 Patrón de Sincronización: Outbox Pattern

```mermaid
stateDiagram-v2
    [*] --> Pendiente: Usuario registra operación offline
    Pendiente --> Enviando: SyncWorker detecta conexión
    Enviando --> Sincronizado: API responde 200 OK
    Enviando --> Error: API responde 4xx/5xx o timeout
    Error --> Pendiente: Retry automático (max 5 intentos)
    Error --> RequiereRevision: > 5 intentos fallidos
    Sincronizado --> [*]: Se marca como completado

    note right of Pendiente
        Guardado en SQLite con
        EstadoSync = "Pendiente"
    end note
    
    note right of Enviando
        SyncService envía vía
        POST /api/sync/push
    end note
```

### 6.4 Flujo de Decisión Online/Offline (OfflineOperacionService)

```mermaid
flowchart TD
    A[Usuario registra operación] --> B{¿Hay conexión<br/>a internet?}
    B -->|SÍ| C[Enviar via HttpClient<br/>POST /api/operaciones/compra]
    B -->|NO| D[Guardar en SQLite<br/>EstadoSync = Pendiente]
    
    C --> E{¿Respuesta OK?}
    E -->|SÍ| F[Mostrar confirmación<br/>Operación #ID]
    E -->|NO, Timeout| D
    E -->|NO, Error 4xx| G[Mostrar error<br/>al usuario]
    
    D --> H[Mostrar confirmación<br/>⚠️ Operación guardada offline<br/>Se sincronizará automáticamente]
    
    subgraph "Background Worker (cada 30s)"
        I[SyncService.TrySyncAsync] --> J{¿Hay pendientes<br/>en SQLite?}
        J -->|SÍ| K[Enviar batch a<br/>POST /api/sync/push]
        K --> L{¿Éxito?}
        L -->|SÍ| M[Marcar como Sincronizado<br/>Guardar ServerOperacionId]
        L -->|NO| N[Incrementar IntentosSyncCount<br/>Registrar ErrorSync]
        J -->|NO| O[Dormir 30s]
    end
```

### 6.5 Estrategia de Resolución de Conflictos

> [!NOTE]
> **Regla de negocio simplificadora:** La cotización es ingresada manualmente por el cajero, por lo tanto no hay "versión verdadera" de la cotización — la que ingresó el cajero es la correcta. **No hay conflicto de datos posible en las operaciones en sí.**

**Fuentes de conflictos posibles y su resolución:**

| Conflicto | Probabilidad | Resolución |
|---|---|---|
| Dos terminales venden la misma divisa simultáneamente | Baja (es una pyme) | **Server-wins**: el servidor valida saldo al momento del `sync/push`. Si el saldo ya se agotó, la operación offline se rechaza y el cajero es notificado |
| Operación offline con cuenta que fue eliminada | Muy baja | El servidor devuelve error 422 y se marca como `RequiereRevision` |
| Cotización del día se cargó distinta en 2 terminales | No aplica | Cada operación lleva su propia cotización embebida |
| Cierre de caja con operaciones pendientes offline | Media | El servidor bloquea cierre si detecta terminales con `sync pendiente`. Se debe sincronizar antes de cerrar |

**Regla de Sincronización estricta:**
```
ALWAYS: Server-Wins para saldos  
ALWAYS: Client-Wins para datos de operación (cotización, montos, observaciones)
ALWAYS: Orden cronológico por FechaCreacionLocal
```

### 6.6 SyncController (Endpoint del Servidor)

El endpoint `POST /api/sync/push` recibe un batch de operaciones offline y las procesa **secuencialmente dentro de una transacción**:

```
POST /api/sync/push
Authorization: Bearer {token}
Content-Type: application/json

{
  "operaciones": [
    {
      "localId": "guid-1234-...",
      "tipoOperacion": "Compra",
      "cuentaOrigenId": 1,
      "cuentaDestinoId": 2,
      "monedaOrigen": "ARS",
      "monedaDestino": "USD",
      "montoOrigen": 1000.00,
      "montoDestino": 1.00,
      "cotizacionAplicada": 1000.00,
      "fechaCreacionLocal": "2026-03-07T18:00:00-03:00",
      "observaciones": "Compra offline"
    }
  ]
}

RESPUESTA 200:
{
  "resultados": [
    {
      "localId": "guid-1234-...",
      "serverOperacionId": 45,
      "exitoso": true,
      "mensaje": null
    }
  ]
}

RESPUESTA 207 (Multi-Status, resultados parciales):
{
  "resultados": [
    { "localId": "guid-1234-...", "serverOperacionId": 45, "exitoso": true },
    { "localId": "guid-5678-...", "serverOperacionId": null, "exitoso": false, "mensaje": "Saldo insuficiente" }
  ]
}
```

---

## 7. Plan de Ejecución por Fases

---

### FASE 0: Preparación del Entorno y Reestructuración de la Solución

> **Objetivo:** Crear la estructura de proyectos múltiples sin romper nada.

**Paso 0.1 — Crear rama de trabajo:**
```bash
cd /home/agustin/PROYECTOS/Sistema_Casa_Cambio
git checkout -b refactor/cliente-servidor
```

**Paso 0.2 — Crear estructura de directorios:**
```bash
mkdir -p src/CasaCambio.Shared/DTOs
mkdir -p src/CasaCambio.Shared/Requests
mkdir -p src/CasaCambio.Shared/Responses
mkdir -p src/CasaCambio.Shared/Enums
mkdir -p src/CasaCambio.Server/Controllers
mkdir -p src/CasaCambio.Server/Data
mkdir -p src/CasaCambio.Server/Auth
mkdir -p src/CasaCambio.Server/Middleware
mkdir -p src/CasaCambio.Desktop/ApiClient
mkdir -p src/CasaCambio.Desktop/LocalDb
mkdir -p src/CasaCambio.Desktop/Sync
```

**Paso 0.3 — Crear proyectos:**
```bash
# Shared library (contratos)
dotnet new classlib -n CasaCambio.Shared -o src/CasaCambio.Shared --framework net8.0
# Web API
dotnet new webapi -n CasaCambio.Server -o src/CasaCambio.Server --framework net8.0 --no-https false
# Tests
dotnet new xunit -n CasaCambio.Tests -o src/CasaCambio.Tests --framework net8.0
```

**Paso 0.4 — Actualizar el .sln:**
```bash
dotnet sln Sistema_Casa_Cambio.sln add src/CasaCambio.Shared/CasaCambio.Shared.csproj
dotnet sln Sistema_Casa_Cambio.sln add src/CasaCambio.Server/CasaCambio.Server.csproj
dotnet sln Sistema_Casa_Cambio.sln add src/CasaCambio.Tests/CasaCambio.Tests.csproj
```

**Paso 0.5 — Agregar referencias entre proyectos:**
```bash
# El Server referencia a Shared
dotnet add src/CasaCambio.Server/CasaCambio.Server.csproj reference src/CasaCambio.Shared/CasaCambio.Shared.csproj

# El Desktop (monolito actual) referenciará a Shared
dotnet add SistemaCambio.csproj reference src/CasaCambio.Shared/CasaCambio.Shared.csproj

# Tests referencia a Server y Shared
dotnet add src/CasaCambio.Tests/CasaCambio.Tests.csproj reference src/CasaCambio.Server/CasaCambio.Server.csproj
dotnet add src/CasaCambio.Tests/CasaCambio.Tests.csproj reference src/CasaCambio.Shared/CasaCambio.Shared.csproj
```

**Paso 0.6 — Instalar paquetes del Server:**
```bash
cd src/CasaCambio.Server
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add package BCrypt.Net-Next --version 4.0.3
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
cd ../..
```

**Paso 0.7 — Instalar paquetes SQLite para el Desktop (preparatorio):**
```bash
dotnet add SistemaCambio.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
```

**Paso 0.8 — Verificar compilación:**
```bash
dotnet build Sistema_Casa_Cambio.sln
```

> [!TIP]
> **Checkpoint:** La solución debe compilar sin errores con los 4 proyectos. El monolito original no se ha modificado aún.

---

### FASE 1: Crear Capa de Contratos (CasaCambio.Shared)

> **Objetivo:** Definir los DTOs que compartirán servidor y cliente.

**Paso 1.1 — Crear DTOs** (1 archivo por DTO). Estos se infieren directamente de las entidades actuales:

| DTO | Basado en | Diferencias con la entidad |
|---|---|---|
| `OperacionDto` | `Operacion` | Sin navegación a `Movimientos`. Agrega `NombreCliente` como string plano |
| `MovimientoDto` | `Movimiento` | Sin navegación. Agrega `NombreCuenta` como string |
| `CuentaDto` | `Cuenta` | Incluye lista de `SaldoCuentaDto` |
| `SaldoCuentaDto` | `SaldoCuenta` | Solo `Moneda` y `Saldo` |
| `MonedaDto` | `Moneda` | Igual (Codigo, Nombre, Activa) |
| `CotizacionDto` | `CotizacionDiaria` | Agrega `CodigoMoneda` en vez de `MonedaId` |
| `ClienteDto` | `Cliente` | Igual |
| `ArqueoDto` | `Arqueo` | Agrega `NombreCuenta` |
| `CierreCajaDto` | `CierreCaja` | Igual |
| `AuditLogDto` | `AuditLog` | Igual |
| `DashboardDto` | `DashboardChartData` + ad-hoc | Métricas agregadas |

**Paso 1.2 — Crear Requests:**

| Request | Corresponde a... |
|---|---|
| `CrearOperacionRequest` | Los parámetros de `OperacionService.GuardarOperacion()` |
| `CrearCreditoDebitoRequest` | Los parámetros de `GuardarCreditoDebito()` |
| `CrearInterbancarioRequest` | Los parámetros de `GuardarOperacionInterbancaria()` |
| `CrearArqueoRequest` | Los parámetros de `ArqueoService.RealizarArqueoCiego()` |
| `LoginRequest` | `{ Username, Password }` |
| `SyncPushRequest` | `{ List<OperacionOffline> Operaciones }` |

**Paso 1.3 — Crear Responses:**

| Response | Uso |
|---|---|
| `OperacionResponse` | Wrapper de `OperacionResult` actual |
| `AuthResponse` | `{ Token, RefreshToken, ExpiresAt, Usuario }` |
| `PaginatedResponse<T>` | Wrapper de `PaginatedResult<T>` actual |
| `SyncPushResponse` | `{ List<SyncResultItem> Resultados }` |
| `ApiErrorResponse` | `{ Code, Message, Details }` |

**Paso 1.4 — Crear Enums:**
- `EstadoSincronizacion`: `Pendiente, Enviando, Sincronizado, Error, RequiereRevision`

**Paso 1.5 — Compilar y verificar:**
```bash
dotnet build src/CasaCambio.Shared/CasaCambio.Shared.csproj
```

---

### FASE 2: Construir Web API (CasaCambio.Server)

> **Objetivo:** Mover TODA la lógica de negocio al servidor.

**Paso 2.1 — Copiar modelos EF (NO mover, COPIAR):**
```bash
# Copiar entidades al Server
cp Models/Operacion.cs src/CasaCambio.Server/Models/
cp Models/Movimiento.cs src/CasaCambio.Server/Models/
cp Models/Cliente.cs src/CasaCambio.Server/Models/
cp Models/Moneda.cs src/CasaCambio.Server/Models/
cp Models/CotizacionDiaria.cs src/CasaCambio.Server/Models/
cp Models/Arqueo.cs src/CasaCambio.Server/Models/
cp Models/CierreCaja.cs src/CasaCambio.Server/Models/
cp Models/EstadoCaja.cs src/CasaCambio.Server/Models/
cp Models/TenenciaMoneda.cs src/CasaCambio.Server/Models/
cp Models/AuditLog.cs src/CasaCambio.Server/Models/
cp Models/ValidationResult.cs src/CasaCambio.Server/Models/
```

**Paso 2.2 — Copiar AppDbContext al Server:**
```bash
cp Models/AppDbContext.cs src/CasaCambio.Server/Data/
```
- Cambiar el namespace a `CasaCambio.Server.Data`
- Eliminar el `OnConfiguring` hardcodeado
- La conexión se inyectará vía `appsettings.json` / `Program.cs`
- Agregar `DbSet<Usuario>` para la tabla de usuarios

**Paso 2.3 — Copiar servicios al Server:**
```bash
cp Services/OperacionService.cs src/CasaCambio.Server/Services/
cp Services/ArqueoService.cs src/CasaCambio.Server/Services/
cp Services/CierreCajaService.cs src/CasaCambio.Server/Services/
cp Services/PPPService.cs src/CasaCambio.Server/Services/
cp Services/AuditService.cs src/CasaCambio.Server/Services/
cp Services/DashboardService.cs src/CasaCambio.Server/Services/
cp Services/QueryService.cs src/CasaCambio.Server/Services/
cp Services/MontoHelper.cs src/CasaCambio.Server/Services/
cp -r Services/Validators/ src/CasaCambio.Server/Validators/
```
- Actualizar todos los namespaces a `CasaCambio.Server.Services`
- **Crítico:** Cambiar los servicios actuales (que son sincrónicos) a **async/await**

**Paso 2.4 — Copiar migraciones EF:**
```bash
cp -r Migrations/ src/CasaCambio.Server/Data/Migrations/
```
- Actualizar namespaces en los archivos de migración

**Paso 2.5 — Crear el modelo `Usuario`:**
```
Crear src/CasaCambio.Server/Models/Usuario.cs con:
  - Id (int)
  - Username (string, unique)
  - PasswordHash (string, BCrypt)
  - NombreCompleto (string)
  - Rol (string: "Admin", "Cajero")
  - Activo (bool)
  - FechaCreacion (DateTime)
```

**Paso 2.6 — Crear JwtService:**
```
Crear src/CasaCambio.Server/Auth/JwtService.cs
  - GenerarToken(Usuario) → string (JWT)
  - GenerarRefreshToken() → string
  - ValidarRefreshToken(string) → bool
  
Crear src/CasaCambio.Server/Auth/JwtSettings.cs
  - SecretKey, Issuer, Audience, AccessTokenExpirationMinutes, RefreshTokenExpirationDays
```

**Paso 2.7 — Crear Controllers:**

Cada controller debe:
1. Inyectar el servicio correspondiente
2. Mapear Request DTO → parámetros del servicio
3. Mapear resultado del servicio → Response DTO
4. Manejar errores con `try/catch` y devolver `ApiErrorResponse`
5. Requerir `[Authorize]` en todos excepto `AuthController.Login`

**Ejemplo de mapeo `OperacionesController`:**
```
POST /api/operaciones/compra
  → Recibe: CrearOperacionRequest
  → Llama: OperacionService.GuardarOperacion(...)
  → Devuelve: OperacionResponse { Exitoso, Mensaje, OperacionId }
  → Registra PPP automáticamente después si la operación fue exitosa
```

**Paso 2.8 — Crear SyncController:**
```
POST /api/sync/push
  → Recibe: SyncPushRequest { List<OperacionOffline> }
  → Por cada operación:
      1. Validar con OperacionValidator
      2. Si válida → GuardarOperacion() con la FechaCreacionLocal como fecha
      3. Si inválida → marcar como error en la respuesta
  → Devuelve: SyncPushResponse con resultado individual por operación
```

**Paso 2.9 — Configurar Program.cs del Server:**
```
1. AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(...))
2. AddAuthentication().AddJwtBearer(...)
3. AddAuthorization()
4. Registrar todos los servicios (copiar patrón de ServiceCollectionExtensions)
5. AddSwaggerGen()
6. Middleware de excepciones global
7. app.UseAuthentication/UseAuthorization
```

**Paso 2.10 — Configurar appsettings.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=xxx.supabase.com;Port=6543;Database=postgres;Username=...;Password=...;SSL Mode=Require"
  },
  "JwtSettings": {
    "SecretKey": "GENERAR-CLAVE-SEGURA-DE-32-CHARS-MINIMO",
    "Issuer": "CasaCambio.Server",
    "Audience": "CasaCambio.Desktop",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Paso 2.11 — Crear ExceptionMiddleware:**
```
Captura excepciones no manejadas y devuelve ApiErrorResponse con status 500.
En desarrollo: incluir stack trace. En producción: solo mensaje genérico.
```

**Paso 2.12 — Verificar compilación y levantar:**
```bash
cd src/CasaCambio.Server
dotnet build
dotnet run
# Verificar Swagger en https://localhost:5001/swagger
```

> [!TIP]
> **Checkpoint:** La API debe levantar, Swagger debe mostrar todos los endpoints, y el login debe devolver un JWT válido.

---

### FASE 3: Cliente API (HttpClient en Desktop)

> **Objetivo:** Crear el wrapper de HttpClient que el Desktop usará en vez de EF Core.

**Paso 3.1 — Crear `ICasaCambioApiClient`:**

```
Interfaz con métodos async que espejean los endpoints de la API:
  Task<AuthResponse> LoginAsync(LoginRequest)
  Task<OperacionResponse> CrearCompraAsync(CrearOperacionRequest)
  Task<OperacionResponse> CrearVentaAsync(CrearOperacionRequest)
  Task<OperacionResponse> CrearCreditoDebitoAsync(CrearCreditoDebitoRequest)
  Task<List<CuentaDto>> ObtenerCuentasAsync()
  Task<List<MonedaDto>> ObtenerMonedasAsync()
  Task<CotizacionDto?> ObtenerCotizacionHoyAsync(string codigoMoneda)
  Task<PaginatedResponse<OperacionDto>> ObtenerOperacionesAsync(...)
  Task<SyncPushResponse> SyncPushAsync(SyncPushRequest)
  ... etc.
```

**Paso 3.2 — Crear `CasaCambioApiClient` (implementación):**
```
- Inyecta HttpClient via constructor
- Maneja JWT: agrega header Authorization automáticamente
- Si recibe 401: intenta refresh token, si falla → evento OnSessionExpired
- Serialización/deserialización con System.Text.Json
- Timeout: 15 segundos para operaciones normales
- Maneja excepciones de red y devuelve resultado descriptivo
```

**Paso 3.3 — Crear `AuthTokenStore`:**
```
- Almacena en memoria: AccessToken, RefreshToken, ExpiresAt
- Persiste en SQLite local (tabla auth_session) para sesiones persistentes
- Expone: bool IsAuthenticated, bool IsTokenExpired
```

**Paso 3.4 — Registrar en DI del Desktop:**
```csharp
services.AddHttpClient<ICasaCambioApiClient, CasaCambioApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

---

### FASE 4: Base de Datos Local SQLite (Offline Cache)

> **Objetivo:** Implementar el almacenamiento local para operaciones offline.

**Paso 4.1 — Crear `LocalDbContext`:**
```
Nuevo DbContext con provider SQLite.
Tablas: operaciones_pendientes, cache_cuentas, cache_monedas, cache_cotizaciones, 
        sync_metadata, auth_session
Path del archivo: {AppData}/CasaCambio/local.db
```

**Paso 4.2 — Crear entidad `LocalOperacion`:**
```
Mapear exactamente los campos del esquema definido en Sección 6.2.
PK: string (GUID)
Incluir: EstadoSync, ErrorSync, IntentosSyncCount, ServerOperacionId
```

**Paso 4.3 — Crear entidades de cache:**
```
CacheCuenta: Id, Nombre, Tipo, List<CacheSaldo>
CacheSaldo: CuentaId, Moneda, Saldo
CacheMoneda: Id, Codigo, Nombre, Activa
CacheCotizacion: MonedaId, CodigoMoneda, Fecha, CotizacionCompra, CotizacionVenta
SyncMetadata: Key (PK), Value, UpdatedAt
```

**Paso 4.4 — Generar migración SQLite:**
```bash
# Desde el directorio del proyecto Desktop
dotnet ef migrations add InitialLocalDb --context LocalDbContext --output-dir LocalDb/Migrations
```

**Paso 4.5 — Verificar que la DB se crea al arrancar la app:**
```csharp
// En App.axaml.cs
var localDb = Services.GetRequiredService<LocalDbContext>();
localDb.Database.EnsureCreated(); // O Migrate()
```

---

### FASE 5: Servicio Offline-First (OfflineOperacionService)

> **Objetivo:** Crear el servicio que decide si operar online u offline.

**Paso 5.1 — Crear `ConnectivityChecker`:**
```
- Método: Task<bool> IsOnlineAsync()
  → Intenta HEAD request a la API (/api/health)
  → Timeout de 3 segundos
  → Si falla → offline
- Evento: OnConnectivityChanged(bool isOnline)
- Timer interno que checkea cada 15 segundos
```

**Paso 5.2 — Crear `OfflineOperacionService` (implementa `IOperacionService`):**

```
Este servicio REEMPLAZA la inyección actual de IOperacionService en el Desktop.

GuardarOperacion(...):
  1. if (await connectivity.IsOnlineAsync())
       → return await apiClient.CrearCompraAsync(request)
  2. else
       → Guardar en SQLite como LocalOperacion(EstadoSync = Pendiente)
       → Actualizar cache local de saldos  
       → return OperacionResult.Success(localId) con flag IsOffline = true
```

> [!WARNING]
> **Decisión de diseño crítica:** En modo offline, la validación de saldos se hace contra el **cache local** en SQLite. El cajero puede operar con saldos potencialmente desactualizados. El servidor **re-validará** al sincronizar. Si rechaza, la operación se marcará como `Error` y el cajero deberá resolverla manualmente.

**Paso 5.3 — Crear `SyncService` (Background Worker):**

```
Clase: SyncService
  - Hereda de BackgroundService (Microsoft.Extensions.Hosting)
  - Timer: ejecutar cada 30 segundos
  - Lógica:
    1. Verificar conectividad
    2. Si online:
       a. Consultar SQLite: operaciones con EstadoSync IN ("Pendiente", "Error") 
          AND IntentosSyncCount < 5
       b. Ordenar por FechaCreacionLocal ASC
       c. Agrupar en batches de 10
       d. Por cada batch:
          - Marcar EstadoSync = "Enviando"
          - POST /api/sync/push
          - Procesar respuesta:
            · Exitoso → EstadoSync = "Sincronizado", guardar ServerOperacionId
            · Error → EstadoSync = "Error", guardar ErrorSync, incrementar IntentosSyncCount
       e. Después de sincronizar:
          - Refrescar cache local (GET /api/cuentas, saldos actualizados)
          - Emitir evento OnSyncCompleted para que la UI actualice
    3. Si offline: dormir y reintentar
```

**Paso 5.4 — Indicador visual en UI:**
```
En MainWindow, agregar un indicador de estado:
  🟢 Conectado (operaciones en tiempo real)
  🟡 Sincronizando... (3 operaciones pendientes)
  🔴 Sin conexión (operaciones se guardan localmente)
```

---

### FASE 6: Refactorizar las Vistas del Desktop

> **Objetivo:** Eliminar TODO acceso directo a EF Core / PostgreSQL desde las vistas.

**Paso 6.1 — Eliminar dependencias de EF del `.csproj` del Desktop:**
```xml
<!-- ELIMINAR estas líneas de SistemaCambio.csproj -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />

<!-- MANTENER solo EF Core base (para SQLite local) -->
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
```

**Paso 6.2 — Refactorizar cada vista** (orden de prioridad):

| Orden | Vista | Cambio principal |
|---|---|---|
| 1 | `CompraWindow.axaml.cs` | Reemplazar `_contextFactory.CreateDbContext()` por `ApiClient.ObtenerCuentasAsync()` y `ApiClient.ObtenerMonedasAsync()`. Usar `OfflineOperacionService` en vez de `OperacionService`. |
| 2 | `VentaWindow.axaml.cs` | Mismo patrón que Compra |
| 3 | `CreditoDebitoWindow.axaml.cs` | Mismo patrón |
| 4 | `MainWindow.axaml.cs` | Dashboard via `ApiClient.ObtenerDashboardAsync()`. Cache local para modo offline |
| 5 | `ConfiguracionWindow.axaml.cs` | CRUD via API. **Solo funciona online** (mostrar aviso si está offline) |
| 6 | `ArqueoWindow.axaml.cs` | Via API. **Solo funciona online** |
| 7 | `CierreCajaWindow.axaml.cs` | Via API. **Solo funciona online**. Debe verificar sync pendiente |
| 8 | `ReportesWindow.axaml.cs` | Via API. Cache opcional |
| 9 | `NuevaCuentaWindow.axaml.cs` | Via API. Solo online |
| 10 | `DetalleMovimientosWindow.axaml.cs` | Via API |
| 11 | `DetalleCuentaWindow.axaml.cs` | Via API |

**Paso 6.3 — Agregar pantalla de Login:**
```
Crear: Views/LoginWindow.axaml + LoginWindow.axaml.cs
  - Campos: Usuario, Contraseña
  - Botón: Iniciar Sesión
  - Al hacer login:
    1. Llamar a ApiClient.LoginAsync()
    2. Si exitoso: guardar tokens en AuthTokenStore, abrir MainWindow
    3. Si falla y tiene sesión offline vigente: permitir acceso limitado
```

**Paso 6.4 — Modificar `App.axaml.cs`:**
```
En vez de abrir MainWindow directamente:
  1. Verificar si hay sesión válida en AuthTokenStore
  2. Si hay → MainWindow
  3. Si no → LoginWindow
```

**Paso 6.5 — Actualizar DI en `ServiceCollectionExtensions.cs`:**
```csharp
// ELIMINAR:
services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(...));
services.AddSingleton<IOperacionService, OperacionService>();
// etc.

// AGREGAR:
services.AddDbContext<LocalDbContext>(options => options.UseSqlite("Data Source=local.db"));
services.AddHttpClient<ICasaCambioApiClient, CasaCambioApiClient>(...);
services.AddSingleton<ConnectivityChecker>();
services.AddSingleton<AuthTokenStore>();
services.AddSingleton<IOperacionService, OfflineOperacionService>();
services.AddHostedService<SyncService>();
```

---

### FASE 7: Testing y Validación Final

**Paso 7.1 — Tests del Server:**
```bash
cd src/CasaCambio.Tests
```

Tests a crear:
- `OperacionesControllerTests` — Tests de integración con InMemory DB
- `SyncControllerTests` — Simular push de operaciones offline
- `AuthControllerTests` — Login, JWT válido/inválido
- `OperacionServiceTests` — Migrar los tests existentes

**Paso 7.2 — Tests del Desktop:**
- `OfflineOperacionServiceTests` — Verificar que guarda en SQLite cuando offline
- `SyncServiceTests` — Verificar que el background worker procesa la cola correctamente
- `ConnectivityCheckerTests` — Mock de HttpClient

**Paso 7.3 — Test de integración End-to-End:**
```
Escenario de prueba manual:
1. Levantar el Server local (dotnet run)
2. Levantar el Desktop, Login
3. Registrar 3 compras online → verificar que llegan a la DB
4. Desconectar internet (desactivar WiFi o apagar servidor)
5. Registrar 2 compras offline → verificar que se guardan en SQLite
6. Reconectar internet
7. Esperar ≤30 segundos → verificar que las 2 operaciones se sincronizaron
8. Verificar saldos coherentes en la DB central
```

**Paso 7.4 — Compilar toda la solución:**
```bash
dotnet build Sistema_Casa_Cambio.sln
dotnet test src/CasaCambio.Tests/CasaCambio.Tests.csproj
```

---

## 8. Resumen de Dependencias de Paquetes NuGet por Proyecto

### CasaCambio.Shared
```
(Ningún paquete adicional — solo .NET primitives)
```

### CasaCambio.Server
```
Npgsql.EntityFrameworkCore.PostgreSQL    8.0.0
Microsoft.EntityFrameworkCore            8.0.0
Microsoft.EntityFrameworkCore.Design     8.0.0
Microsoft.AspNetCore.Authentication.JwtBearer  8.0.0
BCrypt.Net-Next                          4.0.3
Swashbuckle.AspNetCore                   6.5.0
```

### CasaCambio.Desktop (SistemaCambio.csproj modificado)
```
— ELIMINAR: Npgsql.EntityFrameworkCore.PostgreSQL
+ AGREGAR: Microsoft.EntityFrameworkCore.Sqlite  8.0.0
  MANTENER: Avalonia (todos los paquetes)
  MANTENER: CommunityToolkit.Mvvm
  MANTENER: Material.Avalonia
  MANTENER: ScottPlot.Avalonia
  MANTENER: Microsoft.Extensions.DependencyInjection
  MANTENER: Microsoft.EntityFrameworkCore (para SQLite)
```

### CasaCambio.Tests
```
xunit
Microsoft.NET.Test.Sdk
Microsoft.EntityFrameworkCore.InMemory   8.0.0
Moq                                       4.20.0
FluentAssertions                          6.12.0
```

---

## 9. Checklist Final de Verificación

- [ ] La API compila y arranca correctamente
- [ ] Swagger muestra todos los endpoints documentados
- [ ] Login devuelve JWT válido
- [ ] Todos los endpoints requieren autenticación (excepto login)
- [ ] Las operaciones se guardan en Supabase (PostgreSQL en la nube)
- [ ] El Desktop ya no tiene dependencia de Npgsql
- [ ] El Desktop funciona completamente online (via API)
- [ ] El Desktop detecta correctamente cuando está offline
- [ ] Las operaciones offline se guardan en SQLite local
- [ ] El indicador visual muestra el estado de conexión
- [ ] El SyncService envía operaciones pendientes automáticamente
- [ ] Los errores de sincronización se registran y se muestran al usuario
- [ ] El cierre de caja bloquea si hay operaciones pendientes de sincronización
- [ ] Los tests del servidor pasan
- [ ] Los tests del desktop pasan
- [ ] `dotnet build` compila toda la solución sin warnings

---

## 10. Diagrama de Arquitectura Final Completo

```mermaid
graph TB
    subgraph "NUBE (Supabase)"
        PG[(PostgreSQL)]
    end

    subgraph "SERVIDOR (ASP.NET Core)"
        MW[Middleware<br/>Auth + Errors]
        CTRL[Controllers<br/>Auth · Operaciones<br/>Sync · Dashboard]
        SRV[Services<br/>OperacionService<br/>ArqueoService<br/>CierreCajaService<br/>PPPService]
        EF[EF Core]
        
        MW --> CTRL
        CTRL --> SRV
        SRV --> EF
        EF --> PG
    end

    subgraph "CLIENTE DESKTOP (Avalonia)"
        LOGIN[LoginWindow]
        VIEWS[Views<br/>Compra · Venta<br/>CréditoDebito]
        OOS[OfflineOperacion<br/>Service]
        API_C[ApiClient<br/>HttpClient]
        CC[Connectivity<br/>Checker]
        SYNC[SyncService<br/>Background]
        LDB[(SQLite<br/>Local)]
        
        LOGIN --> API_C
        VIEWS --> OOS
        OOS --> CC
        CC -->|Online| API_C
        CC -->|Offline| LDB
        SYNC --> LDB
        SYNC --> API_C
    end

    API_C <-->|HTTPS + JWT| MW

    style PG fill:#3ecf8e,color:#fff
    style LDB fill:#2980b9,color:#fff
    style MW fill:#8e44ad,color:#fff
    style SYNC fill:#e67e22,color:#fff
```

---

> [!IMPORTANT]
> ## Orden de Ejecución para el Agente Autónomo
> 
> 1. **FASE 0** → Crear estructura de proyectos (sin romper el monolito)
> 2. **FASE 1** → DTOs y contratos compartidos  
> 3. **FASE 2** → Web API completa y funcionando
> 4. **FASE 3** → ApiClient en el Desktop
> 5. **FASE 4** → SQLite local en el Desktop
> 6. **FASE 5** → OfflineOperacionService + SyncService
> 7. **FASE 6** → Refactorizar vistas (eliminar EF del desktop)
> 8. **FASE 7** → Tests y validación
>
> **REGLA CRÍTICA**: Después de cada fase, ejecutar `dotnet build` para verificar que la solución completa sigue compilando. NUNCA avanzar a la siguiente fase si hay errores de compilación.
