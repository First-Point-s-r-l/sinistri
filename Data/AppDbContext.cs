using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Models;
using UfficioSinistri.Services;

namespace UfficioSinistri.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options, TenantProvider tenant) : DbContext(options)
    {
        private readonly TenantProvider _tenant = tenant;

        public DbSet<Utente> Utenti { get; set; }
        public DbSet<Sinistro> Sinistri { get; set; }
        public DbSet<SinistroNota> SinistroNote { get; set; }

        public DbSet<Allegato> Allegati { get; set; }

        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Definire la chiave primaria per Sinistro
            modelBuilder.Entity<Sinistro>()
                .HasKey(s => s.TranscriptionResultId);

            // Relazione 1-N Sinistro-Allegati
            modelBuilder.Entity<Allegato>()
                .HasOne(a => a.Sinistro)
                .WithMany(s => s.Allegati)
                .HasForeignKey(a => a.SinistroId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtro globale su AziendaId per Sinistri e Allegati
            modelBuilder.Entity<Sinistro>()
                .HasQueryFilter(s =>
                    _tenant.AziendaId == Guid.Empty
                    || s.AziendaId == _tenant.AziendaId);

            modelBuilder.Entity<Allegato>()
                .HasQueryFilter(a =>
                    _tenant.AziendaId == Guid.Empty
                    || a.AziendaId == _tenant.AziendaId);

            // I campi OperatoreId e NoteOp sono gestiti tramite convenzioni EF
            // Il campo DataChiusura è gestito tramite convenzioni EF
        }
    }
}
