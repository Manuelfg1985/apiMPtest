using System.Collections.Concurrent;

namespace MiPos.API.Models;

public static class CobrosRepository
{
    private static readonly ConcurrentDictionary<string, CobroModel> _cobros = new();

    public static void Guardar(CobroModel cobro)
    {
        _cobros[cobro.Id] = cobro;
    }

    public static CobroModel? ObtenerPorId(string id)
    {
        _cobros.TryGetValue(id, out var cobro);
        return cobro;
    }

    public static bool ActualizarEstado(string id, string nuevoEstado)
    {
        if (_cobros.TryGetValue(id, out var cobro))
        {
            cobro.Estado = nuevoEstado;
            if (nuevoEstado == "APROBADO")
            {
                cobro.FechaPago = DateTime.UtcNow;
            }
            return true;
        }
        return false;
    }
}