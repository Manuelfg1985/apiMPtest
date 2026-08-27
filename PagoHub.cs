using Microsoft.AspNetCore.SignalR;

namespace MiPos.API.Hubs
{
    public class PagoHub : Hub
    {
        // Método opcional para unirse a una sala específica por ID de transacción
        public async Task UnirseASala(string transactionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, transactionId);
        }
    }
}