using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Data;
using UfficioSinistri.Models;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class ChiamateModel : PageModel
    {
        private readonly AppDbContext _db;
        public IList<Chiamata> Chiamate { get; private set; }

        public ChiamateModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnGetAsync()
        {
            // Grazie al filtro globale su AziendaId, qui otteniamo solo i sinistri della sessione
            Chiamate = await _db.Chiamate
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
