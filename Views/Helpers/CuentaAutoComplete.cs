using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CasaCambio.Shared.DTOs;
using SistemaCambio.ViewModels.Models;

namespace SistemaCambio.Views.Helpers
{
    /// <summary>
    /// Configura un AutoCompleteBox como selector híbrido de cuenta:
    /// clic despliega la lista completa, tipear filtra por nombre (Contains, case-insensitive).
    /// La última selección válida se guarda en acb.Tag para recuperación defensiva
    /// (Avalonia 11.0.6 puede anular SelectedItem al abrir el dropdown con FilterMode.Custom).
    /// </summary>
    public static class CuentaAutoComplete
    {
        public static void Configurar(AutoCompleteBox acb)
        {
            acb.MinimumPrefixLength = 0;
            acb.MinimumPopulateDelay = TimeSpan.Zero;
            acb.IsTextCompletionEnabled = false;
            // Asignar ItemFilter fuerza FilterMode = Custom automáticamente
            acb.ItemFilter = (search, item) => Filtrar(acb, search, item);

            // Clic sobre el texto => abrir lista completa.
            // Se restringe al PART_TextBox para no reabrir cuando el clic
            // viene del ListBox del popup (el evento burbujea desde el PopupRoot).
            acb.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
            {
                if (e.Source is Avalonia.Visual v &&
                    v.FindAncestorOfType<TextBox>(includeSelf: true) is { Name: "PART_TextBox" })
                    AbrirDropDown(acb);
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            // Recordar la última selección confirmada
            acb.SelectionChanged += (_, _) =>
            {
                if (acb.SelectedItem is CuentaMonedaTag t) acb.Tag = t;
            };

            // Texto sin match al salir => restaurar (mismo patrón que TxtMoneda_LostFocus)
            acb.LostFocus += (_, _) => RestaurarSeleccion(acb);
        }

        public static void AbrirDropDown(AutoCompleteBox acb)
        {
            if (acb.IsDropDownOpen) return;
            // IsDropDownOpen=true dispara TextUpdated -> PopulateDropDown -> RefreshView()
            // con el ItemFilter actual, por lo que la lista se puebla sola.
            acb.IsDropDownOpen = true;
        }

        private static bool Filtrar(AutoCompleteBox acb, string? search, object? item)
        {
            if (item is not CuentaMonedaTag tag) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            // Si el texto es exactamente la cuenta ya elegida (estado al abrir con clic),
            // mostrar la lista completa en lugar de filtrar a un solo ítem.
            if (acb.Tag is CuentaMonedaTag ultima &&
                string.Equals(search, ultima.NombreCuenta, StringComparison.OrdinalIgnoreCase))
                return true;
            return tag.NombreCuenta.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        public static void Seleccionar(AutoCompleteBox acb, CuentaMonedaTag? tag)
        {
            acb.Tag = tag;
            acb.SelectedItem = tag;          // sincroniza el Text automáticamente
            if (tag is null) acb.Text = "";
        }

        /// <summary>SelectedItem, o fallback por coincidencia exacta de texto
        /// (cubre el caso en que Avalonia anula SelectedItem al abrir el dropdown).</summary>
        public static CuentaMonedaTag? ObtenerSeleccion(AutoCompleteBox acb)
        {
            if (acb.SelectedItem is CuentaMonedaTag t) return t;
            var texto = acb.Text?.Trim();
            if (string.IsNullOrEmpty(texto)) return null;
            return (acb.ItemsSource as IEnumerable<CuentaMonedaTag>)?
                .FirstOrDefault(x => string.Equals(x.NombreCuenta, texto, StringComparison.OrdinalIgnoreCase));
        }

        private static void RestaurarSeleccion(AutoCompleteBox acb)
        {
            if (acb.IsDropDownOpen) return;                 // LostFocus hacia el popup propio
            var actual = ObtenerSeleccion(acb);
            if (actual is not null) { Seleccionar(acb, actual); return; }

            // Texto sin match: volver a la última válida si sigue en la lista vigente
            var items = (acb.ItemsSource as IEnumerable<CuentaMonedaTag>)?.ToList() ?? new List<CuentaMonedaTag>();
            if (acb.Tag is CuentaMonedaTag ultima && items.Contains(ultima))
                Seleccionar(acb, ultima);
            else
                Seleccionar(acb, null);
        }

        // ── Construcción de listas (compartida por Compra y Venta) ──

        /// <summary>Todas las cuentas operables (excluye la cuenta contable "Externo"),
        /// con tags que llevan la moneda indicada.</summary>
        public static List<CuentaMonedaTag> ConstruirTags(IEnumerable<CuentaDto> cuentas, string moneda) =>
            cuentas.Where(c => c.Tipo != "Externo")
                   .OrderBy(c => c.Nombre)
                   .Select(c => new CuentaMonedaTag { CuentaId = c.Id, Moneda = moneda, NombreCuenta = c.Nombre })
                   .ToList();

        /// <summary>Primera caja Tipo=="Efectivo" con saldo en la moneda (ej. "EFECTIVO USD"), o null.</summary>
        public static CuentaMonedaTag? PrimeraCajaEfectivo(
            IEnumerable<CuentaDto> cuentas, string moneda, List<CuentaMonedaTag> tags)
        {
            var caja = cuentas.Where(c => c.Tipo == "Efectivo")
                              .OrderBy(c => c.Nombre)
                              .FirstOrDefault(c => c.Saldos.Any(s => s.Moneda == moneda));
            return caja is null ? null : tags.FirstOrDefault(t => t.CuentaId == caja.Id);
        }
    }
}
