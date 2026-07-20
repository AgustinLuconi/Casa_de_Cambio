using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;
using System;

namespace SistemaCambio.Views
{
    public partial class CompraWindow : Window
    {
        private readonly Control[] _orden;
        private bool _actualizandoDesdeCombo;

        public CompraWindow()
        {
            InitializeComponent();
            CuentaAutoComplete.Configurar(cmbCuentaAcredita);
            CuentaAutoComplete.Configurar(cmbCuentaDebita);
            _orden = [cmbMoneda, txtMonedaExtranjera, cmbCuentaAcredita,
                      txtIngresa, txtCotizacion, cmbCuentaDebita,
                      txtObservaciones, cmbTipoOperacion, btnAceptar];
            AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();
            var dialogService = new WindowDialogService(this);
            var viewModel = new CompraViewModel(apiClient, offlineService, dialogService);
            DataContext = viewModel;

            viewModel.SolicitarCierre += Close;

            // Sincroniza la selección de cuenta del ViewModel con los AutoCompleteBox
            // (no se puede bindear CuentaMonedaTag por TwoWay a Configurar/Seleccionar sin pasar por el helper).
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CompraViewModel.CuentasAcredita))
                    cmbCuentaAcredita.ItemsSource = viewModel.CuentasAcredita;
                if (e.PropertyName == nameof(CompraViewModel.CuentaAcredita))
                    CuentaAutoComplete.Seleccionar(cmbCuentaAcredita, viewModel.CuentaAcredita);
                if (e.PropertyName == nameof(CompraViewModel.CuentasDebita))
                    cmbCuentaDebita.ItemsSource = viewModel.CuentasDebita;
                if (e.PropertyName == nameof(CompraViewModel.CuentaDebita))
                    CuentaAutoComplete.Seleccionar(cmbCuentaDebita, viewModel.CuentaDebita);
                if (e.PropertyName == nameof(CompraViewModel.MonedaNombre))
                {
                    _actualizandoDesdeCombo = true;
                    txtMonedaNombre.Text = viewModel.MonedaNombre;
                    _actualizandoDesdeCombo = false;
                }
            };

            cmbCuentaAcredita.LostFocus += (_, _) =>
            {
                if (DataContext is CompraViewModel vm)
                    vm.CuentaAcredita = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaAcredita);
            };
            cmbCuentaDebita.LostFocus += (_, _) =>
            {
                if (DataContext is CompraViewModel vm)
                    vm.CuentaDebita = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaDebita);
            };
        }

        // ── Sincronización combo/textbox de Moneda (UI, no negocio) ───
        // cmbMoneda bindea ItemsSource/SelectedItem directo al VM (ver .axaml);
        // acá solo se resuelve el eco con el TextBox de nombre libre.

        private void TxtMoneda_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_actualizandoDesdeCombo) return;
            if (DataContext is not CompraViewModel vm) return;
            var match = MonedaSearch.BuscarPorNombre(txtMonedaNombre.Text ?? "", vm.MonedasDisponibles);
            if (match == null) return;
            if (cmbMoneda.SelectedItem != match)
                cmbMoneda.SelectedItem = match;
        }

        private void TxtMoneda_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CompraViewModel vm || vm.MonedaSeleccionada == null) return;
            var match = MonedaSearch.BuscarPorNombre(txtMonedaNombre.Text ?? "", vm.MonedasDisponibles);
            if (match == null)
            {
                _actualizandoDesdeCombo = true;
                txtMonedaNombre.Text = vm.MonedaSeleccionada.Nombre;
                _actualizandoDesdeCombo = false;
            }
        }

        // ── UX de campos numéricos ─────────────────────────────────────

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is not TextBox tb) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (MontoHelper.Parsear(tb.Text) == 0)
                    tb.Clear();
                else
                    tb.SelectAll();
            });
        }

        public void TextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
                tb.Text = "0";
        }

        private void BotonCancelar_Click(object? sender, RoutedEventArgs e) => Close();

        // ── Navegación por teclado ───────────────────────────────────

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (e.Source is Control esc &&
                    esc.FindAncestorOfType<AutoCompleteBox>(includeSelf: true) is { IsDropDownOpen: true } acbEsc)
                { acbEsc.IsDropDownOpen = false; e.Handled = true; return; }
                Close(); e.Handled = true; return;
            }
            // Enter avanza al siguiente campo (como Tab/flecha abajo) en vez de disparar Aceptar directo,
            // para que un Enter apretado sin querer no envíe la operación sin revisar.
            if (e.Key != Key.Down && e.Key != Key.Up && e.Key != Key.Enter) return;
            if (e.Source is ComboBox cb && cb.IsDropDownOpen) return;
            if (e.Source is Control c &&
                c.FindAncestorOfType<AutoCompleteBox>(includeSelf: true) is { IsDropDownOpen: true }) return;
            if (e.Source is not (TextBox or ComboBox)) return;
            MoverFoco(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }

        private void MoverFoco(int delta)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            // El foco real puede estar en el TextBox interno de un AutoCompleteBox
            Control? actual = focused is not null && Array.IndexOf(_orden, focused) >= 0
                ? focused
                : focused?.FindAncestorOfType<AutoCompleteBox>(includeSelf: true);
            var idx = Array.IndexOf(_orden, actual);
            if (idx < 0) return;
            var next = idx + delta;
            if (next >= 0 && next < _orden.Length)
                _orden[next].Focus();
        }
    }
}
