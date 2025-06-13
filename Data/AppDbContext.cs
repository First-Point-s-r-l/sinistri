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

        public DbSet<Allegato> Allegati { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Filtro globale su AziendaId per chiamate e allegati
            modelBuilder.Entity<Chiamata>()
                .HasQueryFilter(c =>
                    _tenant.AziendaId == Guid.Empty
                    || c.AziendaId == _tenant.AziendaId);

            modelBuilder.Entity<Allegato>()
                .HasQueryFilter(a =>
                    _tenant.AziendaId == Guid.Empty
                    || a.AziendaId == _tenant.AziendaId);
        }
    }
}
