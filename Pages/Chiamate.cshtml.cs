using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UfficioSinistri.Data;
using UfficioSinistri.Services;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class ChiamateModel(
        AppDbContext db,
        ILogger<ChiamateModel> logger,
        IWebHostEnvironment env,
        TenantProvider tenant) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<ChiamateModel> _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly TenantProvider _tenant = tenant;

        // filtri bindati per AJAX e export
        [BindProperty(SupportsGet = true)]
        public string? FilterNumero { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterTarga { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterTermini { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDataFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDataTo { get; set; }

        /// <summary>
        /// Inizializza la pagina con i filtri applicati, se presenti.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnGetFilterAsync()
        {
            _logger.LogInformation("Filtri applicati: Numero={Numero}, Targa={Targa}, Termini={Termini}, " +
                                   "DataFrom={DataFrom}, DataTo={DataTo}", FilterNumero, FilterTarga,
                                   FilterTermini, FilterDataFrom, FilterDataTo);
            _logger.LogInformation("AziendaId={AziendaId}", _tenant.AziendaId);

            // il filtro globale su Chiamate farà già c.AziendaId == _tenant.AziendaId
            var results = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0, @p1, @p2, @p3, @p4, @p5",
                    _tenant.AziendaId,
                    FilterNumero,
                    FilterTarga,
                    FilterTermini,
                    FilterDataFrom,
                    FilterDataTo
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF

            var json = results.Select(c => new {
                c.Id,
                c.HistoryCallId,
                c.Numero,
                c.NomeComunicato,
                c.CorrispondenzaRubrica,
                c.Targa,
                c.Evento,
                c.Riassunto
            });

            return new JsonResult(json);
        }


        /// <summary>
        /// Export CSV dei dati filtrati (o singolo, se id.HasValue)
        /// </summary>
        public async Task<IActionResult> OnGetExportCsvAsync(Guid? id)
        {
            _logger.LogInformation(
                "Export CSV; filtri={Numero},{Targa},{Termini},{From},{To} id={Id}",
                FilterNumero, FilterTarga, FilterTermini, FilterDataFrom, FilterDataTo, id);

            // 1) Materializzo tutta la lista dai parametri di filtro
            var all = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0,@p1,@p2,@p3,@p4,@p5",
                    _tenant.AziendaId,
                    FilterNumero,
                    FilterTarga,
                    FilterTermini,
                    FilterDataFrom,
                    FilterDataTo
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF

            // 2) Se id specificato, filtro in memoria
            var list = id.HasValue
                ? [.. all.Where(c => c.Id == id.Value)]
                : all;

            // 3) Genero il CSV
            var sb = new StringBuilder();
            sb.AppendLine("HistoryCallId,Numero,NomeComunicato,CorrispondenzaRubrica,Targa,Evento,Riassunto");
            foreach (var c in list)
            {
                static string esc(string s) =>
                    string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

                sb.AppendLine(
                    $"{esc(c.HistoryCallId)},{esc(c.Numero)},{esc(c.NomeComunicato)}," +
                    $"{esc(c.CorrispondenzaRubrica)},{esc(c.Targa)},{esc(c.Evento)}," +
                    $"{esc(c.Riassunto)}"
                );
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = id.HasValue
                ? $"Sinistro_{id.Value}.csv"
                : "Sinistri.csv";

            return File(bytes, "text/csv", filename);
        }

        /// <summary>
        /// Export PDF dei dati filtrati (o singolo, se id.HasValue)
        /// </summary>
        public async Task<IActionResult> OnGetExportPdfAsync(Guid? id)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation(
                "Export PDF; filtri={Numero},{Targa},{Termini},{From},{To} id={Id}",
                FilterNumero, FilterTarga, FilterTermini, FilterDataFrom, FilterDataTo, id);

            // 1) Materializzo tutta la lista dai parametri di filtro
            var all = await _db.Chiamate
                .FromSqlRaw(
                    "EXEC dbo.Sp_Chiamate_Filtra @p0,@p1,@p2,@p3,@p4,@p5",
                    _tenant.AziendaId,
                    FilterNumero,
                    FilterTarga,
                    FilterTermini,
                    FilterDataFrom,
                    FilterDataTo
                )
                .IgnoreQueryFilters()    //  rimuove il filtro globale per poter eseguire la SP
                .AsNoTracking()
                .ToListAsync();          //  materializziamo qui, senza altre Where EF

            // 2) Se id specificato, filtro in memoria
            var list = id.HasValue
                ? [.. all.Where(c => c.Id == id.Value)]
                : all;

            // 3) Genero il PDF
            var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            byte[] pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);

                    // HEADER
                    page.Header().Row(r =>
                    {
                        r.ConstantItem(60).Height(40)
                         .Image("wwwroot/images/LogoFP.png").FitArea();

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(id.HasValue
                                     ? $"Dettaglio Sinistro {id.Value}"
                                     : "Elenco Sinistri")
                              .FontSize(14).Bold();
                            c.Item().Text($"Estratto: {estrazione}")
                              .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // TABELLA
                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            string[] strings = [
                        "HistoryCallId","Numero","Nome Comunicato",
                        "Rubrica","Targa","Evento","Riassunto","Testo"
                    ];
                            var titles = strings;
                            foreach (var t in titles)
                                header.Cell()
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Padding(2)
                                      .Text(t)
                                      .FontSize(7)
                                      .SemiBold();
                        });

                        foreach (var c in list)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.HistoryCallId).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.Numero ?? "").FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.NomeComunicato ?? "").FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.CorrispondenzaRubrica ?? "").FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.Targa ?? "").FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.Evento ?? "").FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.Riassunto).FontSize(6);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3).Text(c.Testo).FontSize(6);
                        }
                    });

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

            var filename = id.HasValue
                ? $"Sinistro_{id.Value}.pdf"
                : "Sinistri.pdf";

            return File(pdf, "application/pdf", filename);
        }


        /// <summary>
        /// Truncate a string to a maximum length and append ellipsis if truncated.
        /// </summary>
        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength
                ? text
                : text[..maxLength].TrimEnd() + "...";
        }
    }
}
