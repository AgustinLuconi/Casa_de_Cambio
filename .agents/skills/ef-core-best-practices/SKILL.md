---
name: "EF Core - Mejores Prácticas"
description: "Reglas estrictas para el manejo de la base de datos, AppDbContext y migraciones con Entity Framework Core en el proyecto Sistema_Casa_Cambio."
triggers:
  - "crear migración"
  - "actualizar base de datos"
  - "nueva tabla"
  - "modificar db"
  - "crear modelo db"
---

# EF Core - Mejores Prácticas

Este documento establece las directrices arquitectónicas obligatorias para trabajar con Entity Framework Core en el proyecto **Sistema_Casa_Cambio**. El objetivo es mantener una separación de responsabilidades clara y asegurar la escalabilidad del sistema.

## 1. Definición de Modelos (Models)

Los modelos de datos deben ser clases **POCO** (Plain Old CLR Objects) limpias.
- **Ubicación:** `/home/agustin/PROYECTOS/Sistema_Casa_Cambio/Models/`
- **Regla:** Evita saturar las clases de modelo con Data Annotations (como `[Required]`, `[MaxLength]`).
- **Preferencia:** Utiliza **Fluent API** dentro del método `OnModelCreating` en el archivo que define tu `AppDbContext`. Esto centraliza la configuración de la base de datos y mantiene los modelos desacoplados de los detalles de infraestructura.

## 2. Separación de Capas (UI vs DB)

Queda estrictamente prohibido mezclar lógica de la interfaz gráfica en los modelos de base de datos.
- **NUNCA** utilices tipos de Avalonia (como `ReactiveObject`, `ICommand`, etc.) dentro de las clases en la carpeta `Models`.
- Los modelos de base de datos representan el esquema; para la UI, utiliza ViewModels o modelos de vista específicos si es necesario transformar los datos.

## 3. Acceso a Datos y AppDbContext

El `AppDbContext` es el corazón de la persistencia, pero su acceso debe estar controlado:
- **Inyección de Dependencia:** El contexto solo debe inyectarse y utilizarse dentro de la capa de **Services** (`/home/agustin/PROYECTOS/Sistema_Casa_Cambio/Services/`).
- **ViewModels:** Tienen **prohibido** interactuar directamente con el `AppDbContext`. Cualquier operación de datos que necesite un ViewModel debe realizarse invocando un método de un servicio.

## 4. Operaciones Asíncronas

Para evitar bloqueos en la interfaz de usuario (especialmente importante en aplicaciones de escritorio como Avalonia):
- Todas las consultas y operaciones en los servicios **DEBEN** ser asíncronas.
- Utiliza siempre los métodos asíncronos de EF Core:
  - `SaveChangesAsync()` en lugar de `SaveChanges()`.
  - `ToListAsync()`, `FirstOrDefaultAsync()`, `AnyAsync()`, etc.
- Asegúrate de propagar el uso de `async` y `await` correctamente.

## 5. Gestión de Migraciones

Cada vez que se proponga un cambio en el esquema de la base de datos (crear tablas, añadir columnas, modificar relaciones):
1. **Modelar el cambio:** Modificar el archivo en `Models/` o la configuración en el `DbContext`.
2. **Notificar al Usuario:** Debes recordar explícitamente al usuario que ejecute los siguientes comandos en la terminal antes de continuar con pruebas de ejecución:
   ```bash
   dotnet ef migrations add <NombreDescriptivoDeLaMigracion>
   dotnet ef database update
   ```

---

*Nota: Esta política es vital para asegurar que el sistema soporte una alta carga de operaciones sin degradación del rendimiento.*
