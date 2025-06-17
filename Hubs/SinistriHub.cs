using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using UfficioSinistri.Services;

namespace UfficioSinistri.Hubs
{
    public class SinistriHub(ILogger<SinistriNotifier> logger) : Hub
    {
        private readonly ILogger<SinistriNotifier> _logger = logger;

        public override async Task OnConnectedAsync()
        {
            // Leggo l'AziendaId direttamente dal claim dell'utente:
            var aziendaClaim = Context.User?.FindFirst("AziendaId")?.Value;
            if (!string.IsNullOrEmpty(aziendaClaim))
            {

                await Groups.AddToGroupAsync(Context.ConnectionId, aziendaClaim);
            }
            else
            {
                // Se non ho l'AziendaId, non posso connettermi al gruppo
                // Se vuoi, logga l’assenza del claim per debug:
                _logger?.LogWarning("SignalR: aziendaId mancante per connection {Conn}", Context.ConnectionId);
                throw new HubException("AziendaId non trovato nei claims dell'utente.");
            }

            await base.OnConnectedAsync();
        }
    }
}
