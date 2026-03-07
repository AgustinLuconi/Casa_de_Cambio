---
name: "Avalonia MVVM - Casa de Cambio"
description: "Directrices estrictas para el desarrollo en C# .NET utilizando el framework Avalonia UI, el patrón MVVM y Entity Framework Core."
triggers:
  - "crear vista"
  - "nuevo viewmodel"
  - "crear pantalla"
  - "nueva feature"
  - "añadir tabla"
  - "crear servicio"
---

# Reglas Estrictas de Arquitectura (C# Avalonia MVVM)

Actúas como un Desarrollador Senior experto en C#, .NET 8+ y Avalonia UI. Para cualquier nueva funcionalidad, debes estructurar el código respetando estrictamente el patrón MVVM.

## 1. Models (Modelos)
- **Propósito:** Representar los datos de la aplicación y las tablas de la base de datos.
- **Reglas:** Deben ser clases simples (POCOs). Si representan tablas, asegúrate de configurar sus relaciones correctamente para que Entity Framework Core las procese en `AppDbContext.cs`.

## 2. ViewModels (Modelos de Vista)
- **Propósito:** Actuar como intermediario entre la Vista y el Modelo. Manejar la lógica de presentación y el estado de la UI.
- **Regla de Oro:** **CERO referencias a elementos de la interfaz gráfica.** Un ViewModel NUNCA debe importar espacios de nombres de UI ni conocer la existencia de controles de Avalonia (como `Button`, `TextBox`, etc.).
- **Implementación:** Deben heredar de tu clase base (ej. `ViewModelBase`). Utiliza propiedades observables (bindings) y `ICommand` (o `RelayCommand`) para las acciones. 
- **Inyección:** Los ViewModels deben recibir las interfaces de los `Services` a través del constructor (Inyección de Dependencias).

## 3. Views (Vistas)
- **Propósito:** La interfaz de usuario definida en archivos `.axaml`.
- **Regla de Oro:** El *Code-Behind* (el archivo `.axaml.cs`) debe estar completamente limpio. Su único trabajo es llamar a `InitializeComponent()`.
- **Data Binding:** Toda la lógica visual, comandos y estado debe resolverse mediante `{Binding}` hacia las propiedades del ViewModel correspondiente en el archivo `.axaml`.

## 4. Services (Servicios)
- **Propósito:** Lógica de negocio pura, acceso a la base de datos (repositorios) o llamadas a APIs externas.
- **Implementación:** Utiliza `AppDbContext` aquí para interactuar con la base de datos mediante Entity Framework Core. Siempre define una Interfaz (Ej: `ICambioService`) y su implementación (`CambioService`).

## Pasos para generar código para una nueva pantalla:
1. Crea el **Model** (si es necesario) y actualiza `AppDbContext`.
2. Crea la interfaz y la implementación en **Services** para la lógica de datos.
3. Crea el **ViewModel** en la carpeta `ViewModels`, inyectando el servicio necesario y definiendo propiedades observables y comandos.
4. Crea la **View** (`.axaml` y `.axaml.cs`) en la carpeta `Views` y enlaza el `DataContext` al ViewModel.