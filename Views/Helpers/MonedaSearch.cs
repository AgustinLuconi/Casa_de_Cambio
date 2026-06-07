using System;
using System.Collections.Generic;
using System.Linq;
using CasaCambio.Shared.DTOs;

namespace SistemaCambio.Views.Helpers;

public static class MonedaSearch
{
    public static MonedaDto? BuscarPorNombre(string texto, IEnumerable<MonedaDto> disponibles)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var query = texto.Trim();
        var lista = disponibles.ToList();

        var exacto = lista.FirstOrDefault(m =>
            string.Equals(m.Nombre, query, StringComparison.OrdinalIgnoreCase));
        if (exacto != null) return exacto;

        var prefijo = lista.Where(m =>
            m.Nombre.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        return prefijo.Count == 1 ? prefijo[0] : null;
    }
}
