using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Data;
using UfficioSinistri.Models;
using UfficioSinistri.Services;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class DettaglioModel(AppDbContext db, TenantProvider tenant) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly TenantProvider _tenant = tenant;

        [BindProperty]
        public required Chiamata Chiamata { get; set; }

        // id diventa nullable, perché arriva da query string
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
                return BadRequest();   // o RedirectToPage("/Chiamate") se preferisci

            // Esegue la SP passandole anche l'id
            var results = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0,@p1,@p2,@p3,@p4,@p5,@p6",
                    _tenant.AziendaId,
                    /*Numero*/    (string?)null,
                    /*Targa*/     (string?)null,
                    /*Termini*/   (string?)null,
                    /*DataFrom*/  (DateTime?)null,
                    /*DataTo*/    (DateTime?)null,
                    /*Id*/        id
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF


            // Prendiamo il primo risultato, se c'è
            var entity = results.FirstOrDefault();
            if (entity == null)
                return NotFound();

            Chiamata = entity;
            return Page();
        }
    }
}
