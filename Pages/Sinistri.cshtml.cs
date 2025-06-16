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
using UfficioSinistri.Models;
using UfficioSinistri.Services;

namespace UfficioSinistri.Pages
{
    [Authorize]
    public class SinistriModel(
        AppDbContext db,
        ILogger<SinistriModel> logger,
        IWebHostEnvironment env,
        TenantProvider tenant) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<SinistriModel> _logger = logger;
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
        /// AJAX: restituisce JSON con i sinistri filtrati
        /// </summary>
        public async Task<JsonResult> OnGetFilterAsync()
        {
            _logger.LogInformation(
                "Sp_Sinistri_GetList; filters: Numero={Numero}, Targa={Targa}, Termini={Termini}, From={From}, To={To}",
                FilterNumero, FilterTarga, FilterTermini, FilterDataFrom, FilterDataTo);

            var results = await _db.Sinistri
                .FromSqlRaw(
                    "EXEC dbo.Sp_Sinistri_GetList @p0,@p1,@p2,@p3,@p4,@p5",
                    _tenant.AziendaId,
                    FilterNumero,
                    FilterTarga,
                    FilterTermini,
                    FilterDataFrom,
                    FilterDataTo
                )
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync();

            var json = results.Select(s => new {
                id = s.TranscriptionResultId,
                s.HistoryCallId,
                s.Numero,
                s.Nome,
                s.Cognome,
                s.Targa,
                s.Evento,
                s.Riassunto,
                data = s.DataChiamata,
                gestita = s.Gestita,
                hasAllegati = s.HasAllegati
            });

            return new JsonResult(json);
        }

        /// <summary>
        /// Export CSV dei dati filtrati (o singolo se id.HasValue)
        /// </summary>
        public async Task<FileResult> OnGetExportCsvAsync(Guid? id)
        {
            _logger.LogInformation(
                "Export CSV; filters: {Numero},{Targa},{Termini},{From},{To}, id={Id}",
                FilterNumero, FilterTarga, FilterTermini, FilterDataFrom, FilterDataTo, id);

            // recupera la lista completa
            var all = await _db.Sinistri
                .FromSqlRaw(
                    "EXEC dbo.Sp_Sinistri_GetList @p0,@p1,@p2,@p3,@p4,@p5",
                    _tenant.AziendaId,
                    FilterNumero, FilterTarga, FilterTermini,
                    FilterDataFrom, FilterDataTo
                )
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync();

            // applica filtro per id in memoria
            var list = id.HasValue
                ? [.. all.Where(s => s.TranscriptionResultId == id.Value)]
                : all;

            // crea CSV
            var sb = new StringBuilder();
            sb.AppendLine("HistoryCallId,Numero,Nome,Cognome,Targa,Evento,Riassunto,DataChiamata,Gestita,HasAllegati");
            foreach (var s in list)
            {
                static string esc(string? t) =>
                    string.IsNullOrEmpty(t) ? "" : $"\"{t.Replace("\"", "\"\"")}\"";

                sb.AppendLine(
                    $"{esc(s.HistoryCallId)}," +
                    $"{esc(s.Numero)}," +
                    $"{esc(s.Nome)}," +
                    $"{esc(s.Cognome)}," +
                    $"{esc(s.Targa)}," +
                    $"{esc(s.Evento)}," +
                    $"{esc(s.Riassunto)}," +
                    $"{s.DataChiamata:O}," +
                    $"{(s.Gestita ? "1" : "0")}," +
                    $"{(s.HasAllegati ? "1" : "0")}"
                );
            }

            var filename = id.HasValue
                ? $"Sinistro_{id.Value}.csv"
                : "Sinistri.csv";

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
        }

        /// <summary>
        /// Export PDF dei dati filtrati (o singolo se id.HasValue)
        /// </summary>
        public async Task<FileResult> OnGetExportPdfAsync(Guid? id, string[] headers)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _logger.LogInformation(
                "Export PDF; filters: {Numero},{Targa},{Termini},{From},{To}, id={Id}",
                FilterNumero, FilterTarga, FilterTermini, FilterDataFrom, FilterDataTo, id);

            // recupera la lista completa
            var all = await _db.Sinistri
                .FromSqlRaw(
                    "EXEC dbo.Sp_Sinistri_GetList @p0,@p1,@p2,@p3,@p4,@p5",
                    _tenant.AziendaId,
                    FilterNumero, FilterTarga, FilterTermini,
                    FilterDataFrom, FilterDataTo
                )
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync();

            // filtro per id in memoria
            var list = id.HasValue
                ? [.. all.Where(s => s.TranscriptionResultId == id.Value)]
                : all;

            // timestamp estrazione
            var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            // genera PDF
            var pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);

                    // HEADER: logo + titolo + data
                    page.Header().Row(r =>
                    {
                        r.ConstantItem(60).Height(40)
                         .Image("wwwroot/images/LogoFP.png")
                         .FitArea();

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
                        // colonne
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); // HistoryCallId
                            cols.RelativeColumn(1); // Numero
                            cols.RelativeColumn(2); // Nome
                            cols.RelativeColumn(2); // Cognome
                            cols.RelativeColumn(1); // Targa
                            cols.RelativeColumn(2); // Evento
                            cols.RelativeColumn(2); // Riassunto
                            cols.RelativeColumn(3); // DataChiamata
                            cols.RelativeColumn(1); // Gestita
                            cols.RelativeColumn(1); // HasAllegati
                        });
                        table.Header(h =>
                        {
                            foreach (var htext in headers)
                                h.Cell()
                                 .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(2)
                                 .Text(htext)
                                 .FontSize(7).SemiBold();
                        });

                        // dati
                        foreach (var s in list)
                        {
                            void AddCell(string text, int fontSize = 6)
                            {
                                table.Cell()
                                     .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3)
                                     .Text(text)
                                     .FontSize(fontSize);
                            }

                            AddCell(s.HistoryCallId, 8);
                            AddCell(s.Numero ?? "");
                            AddCell(s.Nome ?? "");
                            AddCell(s.Cognome ?? "");
                            AddCell(s.Targa ?? "");
                            AddCell(s.Evento ?? "");
                            AddCell(s.Riassunto ?? "");
                            AddCell(s.DataChiamata.ToString("dd/MM/yyyy HH:mm"), 6);
                            AddCell(s.Gestita ? "✓" : "", 8);
                            AddCell(s.HasAllegati ? "📎" : "", 8);
                        }
                    });

                    // FOOTER
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

            return File(pdf, "application/pdf", filename);
        }

        // helper per troncamento testo
        private static string Truncate(string? text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength
                ? text
                : text[..maxLength].TrimEnd() + "...";
        }
    }
}
