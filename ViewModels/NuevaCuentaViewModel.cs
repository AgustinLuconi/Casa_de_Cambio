using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels.Models;

namespace SistemaCambio.ViewModels
{
    public partial class NuevaCuentaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly int? _cuentaIdAEditar;

        // La carga de monedas reasigna SaldosIniciales: el modo edición debe esperarla
        // antes de mergear los saldos de la cuenta, o el merge podría ser pisado.
        private Task _cargaMonedas = Task.CompletedTask;

        [ObservableProperty] private string _nombre = "";
        // El combo arranca SIN selección: la sección de saldos permanece oculta
        // hasta que el usuario elija un tipo.
        [ObservableProperty] private string? _tipoSeleccionado;

        [ObservableProperty] private bool _borderSaldosVisible;
        [ObservableProperty] private bool _esEfectivo;
        [ObservableProperty] private bool _esCliente;

        [ObservableProperty] private List<FiltroItem<string>> _monedasEfectivo = new();
        [ObservableProperty] private FiltroItem<string>? _monedaEfectivoSeleccionada;
        [ObservableProperty] private string _saldoEfectivoTexto = "0.00";

        [ObservableProperty] private bool _guardarHabilitado = true;
        [ObservableProperty] private string? _tooltipGuardar;

        public ObservableCollection<SaldoInicialItem> SaldosIniciales { get; } = new();

        /// <summary>Expuesto para que el code-behind decida título/ícono de la ventana (no cambia en la vida de la ventana).</summary>
        public bool EsEdicion { get; }

        public ICommand GuardarCommand { get; }

        public event Action? SolicitarCierre;

        public NuevaCuentaViewModel(ICasaCambioApiClient apiClient, int? cuentaIdAEditar = null)
        {
            _apiClient = apiClient;
            _cuentaIdAEditar = cuentaIdAEditar;
            EsEdicion = cuentaIdAEditar.HasValue;

            GuardarCommand = new AsyncRelayCommand(GuardarAsync);

            _cargaMonedas = CargarMonedasAsync();
            _ = VerificarDiaCerradoAsync();
            if (cuentaIdAEditar.HasValue)
                _ = CargarDatosCuentaEdicionAsync();
        }

        partial void OnTipoSeleccionadoChanged(string? value)
        {
            BorderSaldosVisible = value != null;
            EsEfectivo = value == "Efectivo";
            EsCliente = value == "Cliente";
        }

        private async Task VerificarDiaCerradoAsync()
        {
            try
            {
                bool cerrado = await _apiClient.ObtenerEstadoDiaCerradoAsync();
                if (cerrado)
                {
                    GuardarHabilitado = false;
                    TooltipGuardar = "El día está cerrado. Reabra la caja para modificar cuentas.";
                }
            }
            catch (Exception ex) { AppLogger.Warn("VerificarDiaCerradoAsync", ex); }
        }

        private async Task CargarMonedasAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                var saldos = monedas.Select(m => new SaldoInicialItem { Moneda = m.Codigo, Nombre = m.Nombre, Saldo = 0m }).ToList();
                if (!saldos.Any())
                {
                    saldos = new List<SaldoInicialItem>
                    {
                        new() { Moneda = "ARS", Nombre = "Peso Argentino" },
                        new() { Moneda = "USD", Nombre = "Dolar" },
                        new() { Moneda = "EUR", Nombre = "Euro" }
                    };
                }
                SaldosIniciales.Clear();
                foreach (var s in saldos) SaldosIniciales.Add(s);

                MonedasEfectivo = saldos.OrderBy(s => s.Moneda)
                    .Select(s => new FiltroItem<string> { Nombre = $"{s.Moneda} — {s.Nombre}", Valor = s.Moneda })
                    .ToList();
                if (MonedasEfectivo.Count > 0) MonedaEfectivoSeleccionada = MonedasEfectivo[0];
            }
            catch (Exception ex) { AppLogger.Warn("CargarMonedasAsync", ex); }
        }

        private async Task CargarDatosCuentaEdicionAsync()
        {
            if (_cuentaIdAEditar == null) return;

            try
            {
                await _cargaMonedas;   // esperar el catálogo antes de mergear (evita carrera)
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cuenta = cuentas.FirstOrDefault(c => c.Id == _cuentaIdAEditar);
                if (cuenta != null)
                {
                    Nombre = cuenta.Nombre;
                    TipoSeleccionado = cuenta.Tipo;

                    foreach (var saldoDB in cuenta.Saldos)
                    {
                        var saldoLocal = SaldosIniciales.FirstOrDefault(s => s.Moneda == saldoDB.Moneda);
                        if (saldoLocal != null)
                        {
                            saldoLocal.Saldo = saldoDB.Saldo;
                            saldoLocal.LimiteDeudaPersonalizado = saldoDB.LimiteDeudaPersonalizado;
                        }
                        else SaldosIniciales.Add(new SaldoInicialItem
                        {
                            Moneda = saldoDB.Moneda, Nombre = saldoDB.Moneda, Saldo = saldoDB.Saldo,
                            LimiteDeudaPersonalizado = saldoDB.LimiteDeudaPersonalizado
                        });
                    }

                    // SaldoInicialItem no implementa INotifyPropertyChanged: mutar Saldo/LimiteDeudaPersonalizado
                    // de items ya existentes no re-pinta el DataGrid. Forzamos un Reset de la colección
                    // (equivalente al ItemsSource = null; ItemsSource = lista del código original).
                    var itemsActuales = SaldosIniciales.ToList();
                    SaldosIniciales.Clear();
                    foreach (var item in itemsActuales) SaldosIniciales.Add(item);

                    if (EsEfectivo)
                    {
                        var saldoUnico = cuenta.Saldos.FirstOrDefault(s => s.Saldo != 0) ?? cuenta.Saldos.FirstOrDefault();
                        if (saldoUnico != null)
                        {
                            var monedaMatch = MonedasEfectivo.FirstOrDefault(m => m.Valor == saldoUnico.Moneda);
                            if (monedaMatch != null) MonedaEfectivoSeleccionada = monedaMatch;
                            SaldoEfectivoTexto = saldoUnico.Saldo.ToString("N2");
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("CargarDatosCuentaEdicionAsync", ex); }
        }

        private async Task GuardarAsync()
        {
            string nombre = (Nombre?.Trim() ?? "").ToUpperInvariant();
            if (string.IsNullOrEmpty(nombre)) return;

            if (TipoSeleccionado == null)
            {
                NotificationService.Warning("Tipo requerido", "Seleccione el tipo de cuenta antes de guardar.");
                return;
            }

            var saldos = new List<SaldoCuentaDto>();
            if (TipoSeleccionado == "Efectivo")
            {
                var monedaSel = MonedaEfectivoSeleccionada?.Valor;
                if (!string.IsNullOrEmpty(monedaSel) && decimal.TryParse(SaldoEfectivoTexto, out var saldoEf))
                    saldos.Add(new SaldoCuentaDto { Moneda = monedaSel, Saldo = saldoEf });
            }
            else
            {
                foreach (var s in SaldosIniciales)
                    saldos.Add(new SaldoCuentaDto
                    {
                        Moneda = s.Moneda,
                        Saldo = s.Saldo,
                        // Solo cuentas Cliente llevan límite específico por divisa
                        LimiteDeudaPersonalizado = TipoSeleccionado == "Cliente" ? s.LimiteDeudaPersonalizado : 0
                    });
            }

            // LimiteDeuda escalar (legacy) ya no se envía: el modelo nuevo es por divisa
            var request = new CrearCuentaRequest { Nombre = nombre, Tipo = TipoSeleccionado, LimiteDeuda = null, Saldos = saldos };

            try
            {
                if (_cuentaIdAEditar.HasValue)
                    await _apiClient.ActualizarCuentaAsync(_cuentaIdAEditar.Value, request);
                else
                    await _apiClient.CrearCuentaAsync(request);
                NotificationService.Success("Cuenta guardada", "Saldos actualizados correctamente.");
                SolicitarCierre?.Invoke();
            }
            catch (Exception ex) { NotificationService.Error("Error al guardar", ex.Message); }
        }
    }
}
