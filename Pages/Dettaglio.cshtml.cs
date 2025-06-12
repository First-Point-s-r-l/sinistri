using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;
using UfficioSinistri.Data;
using UfficioSinistri.Models;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class DettaglioModel(AppDbContext db) : PageModel
    {
        private readonly AppDbContext _db = db;

        [BindProperty]
        public Chiamata Chiamata { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            // Se non esiste o non appartiene all'azienda, restituisce 404
            Chiamata = await _db.Chiamate.FindAsync(id);
            if (Chiamata == null)
                return NotFound();
            return Page();
        }
    }
}

