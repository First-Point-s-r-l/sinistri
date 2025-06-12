using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Models;
using UfficioSinistri.Services;

namespace UfficioSinistri.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options, TenantProvider tenant) : DbContext(options)
    {
        private readonly TenantProvider _tenant = tenant;

        public DbSet<Utente> Utenti { get; set; }
        public DbSet<Chiamata> Chiamate { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Filtro automatico multi-azienda per le chiamate
            modelBuilder.Entity<Chiamata>()
                .HasQueryFilter(c => c.AziendaId == _tenant.AziendaId);
        }
    }
}
