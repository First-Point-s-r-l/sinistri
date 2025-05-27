using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Models;

namespace UfficioSinistri.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Utente> Utenti { get; set; }
        //public DbSet<Chiamata> Chiamate { get; set; }
    }
}
