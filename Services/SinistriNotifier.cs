using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UfficioSinistri.Hubs;

namespace UfficioSinistri.Services
{
    public class SinistriNotifier(
        ILogger<SinistriNotifier> logger,
        IServiceProvider services,
        IConfiguration config) : IHostedService, IDisposable
    {
        private readonly ILogger<SinistriNotifier> _logger = logger;
        private readonly IServiceProvider _services = services;
        private readonly string _connectionString = config.GetConnectionString("DefaultConnection")!;

        private SqlConnection? _connection;
        private SqlCommand? _command;
        private SqlDependency? _dependency;
        private DateTime _lastCheck = DateTime.UtcNow;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Avvia il listener SQL Server
            SqlDependency.Start(_connectionString);
            RegisterDependency();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Ferma il listener SQL Server
            SqlDependency.Stop(_connectionString);
            return Task.CompletedTask;
        }

        private void RegisterDependency()
        {
            _logger.LogInformation("RegisterDependency: inizio registrazione SqlDependency");

            if (_dependency != null)
                _dependency.OnChange -= OnDependencyChange;

            _command?.Dispose();
            _connection?.Dispose();

            _connection = new SqlConnection(_connectionString);
            _command = new SqlCommand(
                @"SELECT TranscriptionResultId, AziendaId, Data
              FROM dbo.Sinistri",
                _connection);

            _dependency = new SqlDependency(_command);
            _dependency.OnChange += OnDependencyChange;

            _connection.Open();
            using var reader = _command.ExecuteReader(CommandBehavior.CloseConnection);

            _logger.LogInformation("RegisterDependency: SqlDependency registrato e comando eseguito");
        }

        private void OnDependencyChange(object? sender, SqlNotificationEventArgs e)
        {
            _logger.LogInformation("OnDependencyChange: evento ricevuto Info={Info}, Source={Source}, Type={Type}",
                e.Info, e.Source, e.Type);

            var since = _lastCheck;
            _lastCheck = DateTime.UtcNow;
            _logger.LogInformation("OnDependencyChange: controllo modifiche da {Since} a {Now}", since, _lastCheck);

            var changedTenants = new HashSet<Guid>();
            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(
                @"SELECT DISTINCT AziendaId
              FROM dbo.Sinistri
              WHERE Data > @since",
                conn))
            {
                cmd.Parameters.AddWithValue("@since", since);
                conn.Open();
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var azienda = rdr.GetGuid(rdr.GetOrdinal("AziendaId"));
                    changedTenants.Add(azienda);
                }
            }

            if (changedTenants.Count == 0)
            {
                _logger.LogWarning("OnDependencyChange: nessun tenant interessato dalle modifiche");
            }
            else
            {
                _logger.LogInformation("OnDependencyChange: tenant cambiati = {Tenants}",
                    string.Join(", ", changedTenants));
            }

            using var scope = _services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<SinistriHub>>();

            foreach (var tenantId in changedTenants)
            {
                _logger.LogInformation("OnDependencyChange: invio SinistriAggiornati al gruppo {Tenant}", tenantId);
                // per verificare che arrivi davvero al client
                //hub.Clients.All.SendAsync("SinistriAggiornati");
                hub.Clients.Group(tenantId.ToString())
                   .SendAsync("SinistriAggiornati");
            }

            RegisterDependency();
        }

        public void Dispose()
        {
            // Scollego handler e libero risorse
            if (_dependency is not null)
                _dependency.OnChange -= OnDependencyChange;

            _command?.Dispose();
            _connection?.Dispose();

            // Best practice per Dispose
            GC.SuppressFinalize(this);
        }
    }
}
