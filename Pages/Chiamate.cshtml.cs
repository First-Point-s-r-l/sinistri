using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UfficioSinistri.Data;
using UfficioSinistri.Models;

using System.Text;
using System.IO;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace UfficioSinistri.Pages
{
    [Authorize]
    public class ChiamateModel(AppDbContext db, ILogger<ChiamateModel> logger, IWebHostEnvironment env) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<ChiamateModel> _logger = logger;
        private readonly IWebHostEnvironment _env = env;

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
        /// Esportazione PDF (lista o singolo) con template “Dettaglio Sinistro” corretto
        /// </summary>
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation("Avvio export PDF. Id singolo: {SinistroId}", id);

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
                        page.Margin(0);
                        page.PageColor(Colors.Grey.Lighten5);

                        // Fascia blu in alto
                        page.Background().Background(Colors.Blue.Darken2);

                        page.Content().Padding(20).Column(col =>
                        {
                            // Titolo con padding sotto
                            col.Item().Element(el =>
                            {
                                el.PaddingBottom(10)
                                  .Text(id.HasValue
                                     ? $"Dettaglio Sinistro {id}"
                                     : "Elenco Sinistri")
                                  .FontSize(24)
                                  .FontColor(Colors.Blue.Darken2)
                                  .Bold();
                            });

                            // Scheda bianca
                            col.Item().Element(card =>
                            {
                                card
                                    .Background(Colors.White)
                                    .Border(1)
                                    //.BorderRadius(8)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(20)
                                    .Column(fields =>
                                    {
                                        // Helper per il rendering di ciascun record
                                        for (int i = 0; i < list.Count; i++)
                                        {
                                            var c = list[i];

                                            void AddField(string label, string value)
                                            {
                                                // riga label+valore
                                                fields.Item().Row(r =>
                                                {
                                                    r.ConstantItem(150)
                                                     .Text(label)
                                                     .FontSize(12)
                                                     .SemiBold()
                                                     .FontColor(Colors.Blue.Darken2);

                                                    r.RelativeItem()
                                                     .Text(value ?? "")
                                                     .FontSize(12)
                                                     .FontColor(Colors.Black);
                                                });

                                                // linea di separazione
                                                fields.Item()
                                                      .Height(1)
                                                      .Background(Colors.Grey.Lighten2);

                                                // piccolo spazio
                                                fields.Item()
                                                      .Height(5);
                                            }

                                            AddField("HistoryCallId", c.HistoryCallId);
                                            AddField("Numero", c.Numero);
                                            AddField("Nome Comunicato", c.NomeComunicato);
                                            AddField("Corrispondenza Rubrica", c.CorrispondenzaRubrica);
                                            AddField("Targa", c.Targa);
                                            AddField("Evento", c.Evento);
                                            AddField("Riassunto", c.Riassunto);
                                            AddField("Testo", Truncate(c.Testo, 500));

                                            // spazio fra le schede se più di uno
                                            if (i < list.Count - 1)
                                            {
                                                fields.Item().Height(20);
                                            }
                                        }

                                    });
                            });
                        });

                        // Footer con paginazione
                        page.Footer().AlignCenter()
                            .Text(txt =>
                            {
                                txt.Span("Pagina ").FontSize(10);
                                txt.CurrentPageNumber().FontSize(10);
                                txt.Span(" di ").FontSize(10);
                                txt.TotalPages().FontSize(10);
                            });
                    });
                })
                .GeneratePdf();

                _logger.LogInformation("Export PDF completato ({Count} schede)", list.Count);
                return File(
                    pdf,
                    "application/pdf",
                    id.HasValue ? $"Sinistro_{id}.pdf" : "Sinistri.pdf"
                );
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
