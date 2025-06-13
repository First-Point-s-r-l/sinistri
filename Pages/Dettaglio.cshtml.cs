using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UfficioSinistri.Data;
using UfficioSinistri.Models;
using UfficioSinistri.Services;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class DettaglioModel(AppDbContext db, TenantProvider tenant, ILogger<ChiamateModel> logger) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly TenantProvider _tenant = tenant;
        private readonly ILogger<ChiamateModel> _logger = logger;

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

        /// <summary>
        /// Export CSV del singolo sinistro
        /// </summary>
        public async Task<IActionResult> OnGetExportCsvAsync(Guid? id)
        {
            _logger.LogInformation(
                "Export CSV; filtri=id={Id}", id);

            // RIUSIAMO A RIUTILIZZARE LA SP CON SOLO ID E AZIENDA
            var list = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0,@p1,@p2,@p3,@p4,@p5,@p6",
                    _tenant.AziendaId,
                    (string?)null,   // Numero
                    (string?)null,   // Targa
                    (string?)null,   // Termini
                    (DateTime?)null, // DataFrom
                    (DateTime?)null, // DataTo
                    id               // Id = singolo
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF

            // 2) Se id specificato, filtro in memoria
            var c = list.FirstOrDefault();
            if (c == null) return NotFound();

            _logger.LogInformation(
                "Export CSV; trovato={Count} record", list.Count);

            // 3) Creiamo il CSV
            var sb = new StringBuilder();
            sb.AppendLine("HistoryCallId,Numero,NomeComunicato,CorrispondenzaRubrica,Targa,Evento,Riassunto,Testo");
            static string esc(string s) =>
                string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
            sb.AppendLine(
                $"{esc(c.HistoryCallId)}," +
                $"{esc(c.Numero)}," +
                $"{esc(c.NomeComunicato)}," +
                $"{esc(c.CorrispondenzaRubrica)}," +
                $"{esc(c.Targa)}," +
                $"{esc(c.Evento)}," +
                $"{esc(c.Riassunto)}," +
                $"{esc(c.Testo)}"
            );

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = id.HasValue
               ? $"Sinistro_{id.Value}.csv"
               : "Sinistri.csv";

            _logger.LogInformation(
                "Export CSV; file={FileName}, size={Size} bytes", filename, bytes.Length);

            // 4) Restituiamo il file
            return File(bytes, "text/csv", $"Sinistro_{c.HistoryCallId}.csv");
        }

        /// <summary>
        /// Export PDF del singolo sinistro
        /// </summary>
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation(
                "Export PDF; filtri=id={Id}", id);

            // 1) Eseguiamo la SP per ottenere il sinistro
            var list = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0,@p1,@p2,@p3,@p4,@p5,@p6",
                    _tenant.AziendaId,
                    (string?)null,
                    (string?)null,
                    (string?)null,
                    (DateTime?)null,
                    (DateTime?)null,
                    id
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF

            // 2) Se id specificato, filtro in memoria
            var c = list.FirstOrDefault();
            if (c == null) return NotFound();

            // 3) Genero il PDF
            var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            byte[] pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);

                    // HEADER
                    page.Header().Row(r =>
                    {
                        r.ConstantItem(60).Height(40)
                         .Image("wwwroot/images/LogoFP.png").FitArea();
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Dettaglio Sinistro {c.Id}")
                               .FontSize(14).Bold();
                            col.Item().Text($"Estratto: {estrazione}")
                               .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // TABELLA
                    page.Content().Padding(10).Column(col =>
                    {
                        void Row(string label, string? value) =>
                            col.Item().Row(r2 =>
                            {
                                r2.ConstantItem(150).Text(label).SemiBold();
                                r2.RelativeItem().Text(value ?? "");
                            });

                        Row("HistoryCallId", c.HistoryCallId);
                        Row("Numero", c.Numero);
                        Row("Nome Comunicato", c.NomeComunicato);
                        Row("Rubrica", c.CorrispondenzaRubrica);
                        Row("Targa", c.Targa);
                        Row("Evento", c.Evento);
                        Row("Riassunto", c.Riassunto);
                        Row("Testo", c.Testo);
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Pagina ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" di ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            })
            .GeneratePdf();

            var filename = id.HasValue
               ? $"Sinistro_{id.Value}.pdf"
               : "Sinistri.pdf";

            _logger.LogInformation(
                "Export PDF; file={FileName}, size={Size} bytes", filename, pdf.Length);

            return File(pdf, "application/pdf", $"Sinistro_{c.HistoryCallId}.pdf");
        }
    }
}
