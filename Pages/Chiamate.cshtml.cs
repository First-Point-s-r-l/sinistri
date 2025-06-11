using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UfficioSinistri.Data;
using UfficioSinistri.Models;

using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace UfficioSinistri.Pages
{
    [Authorize]
    public class ChiamateModel(AppDbContext db, ILogger<ChiamateModel> logger) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<ChiamateModel> _logger = logger;

        // Lista di chiamate da visualizzare nella pagina
        public IList<Chiamata>? Chiamate { get; private set; } = [];

        public async Task OnGetAsync()
        {
            // Grazie al filtro globale su AziendaId, qui otteniamo solo i sinistri della sessione
            Chiamate = await _db.Chiamate
                                .AsNoTracking()
                                .ToListAsync();
        }

        /// <summary>
        /// Esportazione CSV (tutti o singolo, se passi id) 
        /// </summary>
        public async Task<IActionResult> OnGetExportCsvAsync(Guid? id)
        {
            _logger.LogInformation("Avvio export CSV. Id singolo: {SinistroId}", id);

            try
            {
                // 1) Carico i dati
                var query = _db.Chiamate.AsQueryable();
                if (id.HasValue)
                {
                    query = query.Where(c => c.Id == id);
                    _logger.LogDebug("Filtro per Id {SinistroId}", id);
                }

                var list = await query.ToListAsync();

                // 2) Building CSV
                var sb = new StringBuilder();
                sb.AppendLine("HistoryCallId,Numero,NomeComunicato,CorrispondenzaRubrica,Targa,Testo,Evento,Riassunto");
                foreach (var c in list)
                {
                    static string esc(string s) =>
                        string.IsNullOrEmpty(s)
                          ? ""
                          : $"\"{s.Replace("\"", "\"\"")}\"";

                    sb.AppendLine($"{esc(c.HistoryCallId)},{esc(c.Numero)},{esc(c.NomeComunicato)},{esc(c.CorrispondenzaRubrica)},{esc(c.Targa)},{esc(c.Testo)},{esc(c.Evento)},{esc(c.Riassunto)}");
                }

                // 3) Ritorno file
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                _logger.LogInformation("Export CSV completato. Totale righe: {Count}", list.Count);
                return File(bytes, "text/csv", id.HasValue ? $"Sinistro_{id}.csv" : "Sinistri.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore during CSV export");
                throw; // oppure return StatusCode(500);
            }
        }


        /// <summary>
        /// Esportazione PDF (lista o singolo)
        /// </summary>
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            _logger.LogInformation("Avvio export PDF. Id singolo: {SinistroId}", id);
            QuestPDF.Settings.License = LicenseType.Community;
            try
            {
                // 1) Carico i dati
                var query = _db.Chiamate.AsQueryable();
                if (id.HasValue)
                {
                    query = query.Where(c => c.Id == id);
                    _logger.LogDebug("Filtro per Id {SinistroId}", id);
                }

                var list = await query.ToListAsync();

                // 2) Genero PDF
                byte[] pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);

                        page.Header()
                            .Text(id.HasValue ? $"Sinistro {id}" : "Elenco Sinistri")
                            .FontSize(16).Bold();

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2);
                                cd.RelativeColumn(3);
                                cd.RelativeColumn(3);
                                cd.RelativeColumn(3);
                                cd.RelativeColumn(5);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Id").Bold();
                                header.Cell().Text("HistoryCallId").Bold();
                                header.Cell().Text("Numero").Bold();
                                header.Cell().Text("Targa").Bold();
                                header.Cell().Text("Riassunto").Bold();
                            });

                            foreach (var c in list)
                            {
                                table.Cell().Text(c.Id.ToString());
                                table.Cell().Text(c.HistoryCallId);
                                table.Cell().Text(c.Numero ?? "");
                                table.Cell().Text(c.Targa ?? "");
                                table.Cell().Text(Truncate(c.Riassunto, 200));
                            }
                        });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Pagina ");
                                text.CurrentPageNumber();
                                text.Span(" di ");
                                text.TotalPages();
                            });
                    });
                })
                .GeneratePdf();

                _logger.LogInformation("Export PDF completato. Pagine generate: {Pages}", /* non esposto da QuestPDF, ma conti righe */ list.Count);
                return File(pdf, "application/pdf", id.HasValue ? $"Sinistro_{id}.pdf" : "Sinistri.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore during PDF export");
                throw;
            }
        }

        // --- helper per troncare il testo nelle tabelle PDF ---
        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= maxLength
                ? text
                : text[..maxLength].TrimEnd() + "...";
        }
    }
}
