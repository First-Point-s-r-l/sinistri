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
        /*
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation("Avvio export PDF. Id singolo: {SinistroId}", id);

            try
            {
                var query = _db.Chiamate.AsQueryable();
                if (id.HasValue)
                {
                    query = query.Where(c => c.Id == id);
                }
                var list = await query.ToListAsync();

                // Data/ora estrazione
                var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                byte[] pdf = Document.Create(doc =>
                {
                    doc.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.PageColor(Colors.White);

                        // Header: logo + titolo + data
                        page.Header().Row(r =>
                        {
                            // Logo
                            r.ConstantItem(80)
                             .Height(50)
                             .Image("wwwroot/images/LogoFP.png")  // usa l’overload statico
                             .FitArea();                          // scala in modo ottimale :contentReference[oaicite:0]{index=0}

                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(id.HasValue ? $"Dettaglio Sinistro {id}" : "Elenco Sinistri")
                                         .FontSize(18).Bold();
                                c.Item().Text($"Estratto: {estrazione}")
                                         .FontSize(10).FontColor(Colors.Grey.Darken1);
                            });
                        });

                        page.Content().PaddingTop(10).Table(table =>
                        {
                            // Definizione colonne
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);    // HistoryCallId
                                cols.RelativeColumn(1);    // Numero
                                cols.RelativeColumn(2);    // NomeComunicato
                                cols.RelativeColumn(2);    // CorrispondenzaRubrica
                                cols.RelativeColumn(1);    // Targa
                                cols.RelativeColumn(2);    // Evento
                                cols.RelativeColumn(3);    // Riassunto
                                cols.RelativeColumn(3);    // Testo (troncato)
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Text("HistoryCallId").Bold();
                                header.Cell().Text("Numero").Bold();
                                header.Cell().Text("Nome Comunicato").Bold();
                                header.Cell().Text("Rubrica").Bold();
                                header.Cell().Text("Targa").Bold();
                                header.Cell().Text("Evento").Bold();
                                header.Cell().Text("Riassunto").Bold();
                                header.Cell().Text("Testo").Bold();
                            });

                            // Righe
                            foreach (var c in list)
                            {
                                table.Cell().Text(c.HistoryCallId);
                                table.Cell().Text(c.Numero ?? "");
                                table.Cell().Text(c.NomeComunicato ?? "");
                                table.Cell().Text(c.CorrispondenzaRubrica ?? "");
                                table.Cell().Text(c.Targa ?? "");
                                table.Cell().Text(c.Evento ?? "");
                                table.Cell().Text(c.Riassunto ?? "");
                                table.Cell().Text(c.Testo ?? "");
                            }
                        });

                        // Footer
                        page.Footer().AlignCenter().Text(txt =>
                        {
                            txt.Span("Pagina ").FontSize(10);
                            txt.CurrentPageNumber().FontSize(10);
                            txt.Span(" di ").FontSize(10);
                            txt.TotalPages().FontSize(10);
                        });
                    });
                })
                .GeneratePdf();

                return File(pdf, "application/pdf",
                    id.HasValue ? $"Sinistro_{id}.pdf" : "Sinistri.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'export PDF");
                throw;
            }
        }
        */
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation("Avvio export PDF. Id singolo: {SinistroId}", id);

            try
            {
                // Carico i dati
                var query = _db.Chiamate.AsQueryable();
                if (id.HasValue)
                    query = query.Where(c => c.Id == id);
                var list = await query.ToListAsync();

                // Timestamp di estrazione
                var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                // Generazione PDF
                byte[] pdf = Document.Create(doc =>
                {
                    doc.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.PageColor(Colors.White);

                        // HEADER: logo + titolo + data
                        page.Header().Row(r =>
                        {
                            r.ConstantItem(60)
                             .Height(40)
                             .Image("wwwroot/images/LogoFP.png")
                             .FitArea();

                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(id.HasValue
                                        ? $"Dettaglio Sinistro {id}"
                                        : "Elenco Sinistri")
                                    .FontSize(14)
                                    .Bold();

                                c.Item().Text($"Estratto: {estrazione}")
                                    .FontSize(8)
                                    .FontColor(Colors.Grey.Darken1);
                            });
                        });

                        // CONTENUTO: tabella
                        page.Content().PaddingVertical(5).Table(table =>
                        {
                            // Definizione colonne
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);    // HistoryCallId
                                cols.RelativeColumn(1);    // Numero
                                cols.RelativeColumn(2);    // Nome Comunicato
                                cols.RelativeColumn(2);    // Rubrica
                                cols.RelativeColumn(1);    // Targa
                                cols.RelativeColumn(2);    // Evento
                                cols.RelativeColumn(2);    // Riassunto
                                cols.RelativeColumn(3);    // Testo
                            });

                            // Intestazione
                            table.Header(header =>
                            {
                                string[] titles = [
                            "HistoryCallId", "Numero",
                            "Nome Comunicato", "Rubrica", "Targa",
                            "Evento", "Riassunto", "Testo"
                        ];

                                foreach (var t in titles)
                                    header.Cell()
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .Padding(2)
                                          .Text(t)
                                          .FontSize(7)
                                          .SemiBold();
                            });

                            // Dati
                            foreach (var c in list)
                            {
                                // Id: primo segmento del GUID
                                table.Cell()
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Id.ToString().Split('-')[0])
                                     .FontSize(8);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Numero ?? "").FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.NomeComunicato ?? "").FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.CorrispondenzaRubrica ?? "").FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Targa ?? "").FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Evento ?? "").FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Riassunto).FontSize(6);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(c.Testo).FontSize(6);
                            }
                        });

                        // FOOTER: paginazione
                        page.Footer().AlignCenter().Text(txt =>
                        {
                            txt.Span("Pagina ").FontSize(8);
                            txt.CurrentPageNumber().FontSize(8);
                            txt.Span(" di ").FontSize(8);
                            txt.TotalPages().FontSize(8);
                        });
                    });
                })
                .GeneratePdf();

                _logger.LogInformation("Export PDF completato ({Count} righe)", list.Count);
                return File(
                    pdf,
                    "application/pdf",
                    id.HasValue ? $"Sinistro_{id}.pdf" : "Sinistri.pdf"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'export PDF");
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