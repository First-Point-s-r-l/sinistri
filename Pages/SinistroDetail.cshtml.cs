// Pages/SinistroDetail.cshtml.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
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
    public class SinistroDetailModel(
        AppDbContext db,
        TenantProvider tenant,
        ILogger<SinistroDetailModel> logger) : PageModel
    {
        private readonly AppDbContext _db = db;
        private readonly TenantProvider _tenant = tenant;
        private readonly ILogger<SinistroDetailModel> _logger = logger;

        // Il sinistro caricato
        public Sinistro? Sinistro { get; set; }

        // Gli eventuali allegati
        public List<Allegato> Allegati { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
                return BadRequest();

            // Apertura connessione
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "Sp_Sinistri_GetById";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
            cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id.Value));

            using var reader = await cmd.ExecuteReaderAsync();

            // 1) Leggi il sinistro
            if (await reader.ReadAsync())
            {
                Sinistro = new Sinistro
                {
                    TranscriptionResultId = reader.GetGuid(reader.GetOrdinal("TranscriptionResultId")),
                    AziendaId = reader.GetGuid(reader.GetOrdinal("AziendaId")),        // ora presente
                    HistoryCallId = reader.GetString(reader.GetOrdinal("HistoryCallId")),
                    Numero = reader.IsDBNull(reader.GetOrdinal("Numero")) ? null : reader.GetString(reader.GetOrdinal("Numero")),
                    Nome = reader.IsDBNull(reader.GetOrdinal("Nome")) ? null : reader.GetString(reader.GetOrdinal("Nome")),
                    Cognome = reader.IsDBNull(reader.GetOrdinal("Cognome")) ? null : reader.GetString(reader.GetOrdinal("Cognome")),
                    Targa = reader.IsDBNull(reader.GetOrdinal("Targa")) ? null : reader.GetString(reader.GetOrdinal("Targa")),
                    DataChiamata = reader.GetDateTime(reader.GetOrdinal("DataChiamata")),
                    Gestita = reader.GetBoolean(reader.GetOrdinal("Gestita")),
                    HasAllegati = reader.GetBoolean(reader.GetOrdinal("HasAllegati")),
                    Trascrizione = reader.IsDBNull(reader.GetOrdinal("Trascrizione")) ? null : reader.GetString(reader.GetOrdinal("Trascrizione")),
                    Riassunto = reader.IsDBNull(reader.GetOrdinal("Riassunto")) ? null : reader.GetString(reader.GetOrdinal("Riassunto")),
                    Evento = reader.IsDBNull(reader.GetOrdinal("Evento")) ? null : reader.GetString(reader.GetOrdinal("Evento")),
                    Assistenza = reader.IsDBNull(reader.GetOrdinal("Assistenza")) ? null : reader.GetString(reader.GetOrdinal("Assistenza")),
                    PropostaAI = reader.IsDBNull(reader.GetOrdinal("PropostaAI")) ? null : reader.GetString(reader.GetOrdinal("PropostaAI")),
                    SelezioneUtente = reader.IsDBNull(reader.GetOrdinal("SelezioneUtente")) ? null : reader.GetString(reader.GetOrdinal("SelezioneUtente"))
                };
            }
            else
            {
                return NotFound();
            }

            // 2) Leggi il secondo result-set: gli allegati
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    Allegati.Add(new Allegato
                    {
                        AllegatoId = reader.GetGuid(reader.GetOrdinal("AllegatoId")),
                        NomeFile = reader.GetString(reader.GetOrdinal("NomeFile")),
                        Estensione = reader.IsDBNull(reader.GetOrdinal("Estensione")) ? null : reader.GetString(reader.GetOrdinal("Estensione")),
                        DataCaricamento = reader.GetDateTime(reader.GetOrdinal("DataCaricamento"))
                    });
                }
            }

            return Page();
        }

        /// <summary>
        /// Scarica il file binario dell’allegato
        /// </summary>
        public async Task<IActionResult> OnGetDownloadAttachmentAsync(Guid id)
        {
            // Recupera l’allegato (incluso FileData) filtrando per tenant
            var allegato = await _db.Allegati
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.AllegatoId == id &&
                    a.AziendaId == _tenant.AziendaId);

            if (allegato == null) return NotFound();

            // restituisci il file
            var mime = GetMimeType(allegato.Estensione);
            var fileName = Path.GetFileName(allegato.NomeFile) +
                           (string.IsNullOrEmpty(allegato.Estensione) ? "" : "." + allegato.Estensione.TrimStart('.'));

            return File(allegato.FileData!, mime, fileName);
        }

        private static string GetMimeType(string? ext)
        {
            return ext?.ToLowerInvariant() switch
            {
                "pdf" => "application/pdf",
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "txt" => "text/plain",
                "doc" => "application/msword",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "xls" => "application/vnd.ms-excel",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Export CSV del singolo sinistro
        /// </summary>
        public async Task<IActionResult> OnGetExportCsv(Guid id)
        {
            // ricarica il sinistro e gli allegati
            await OnGetAsync(id);
            if (Sinistro == null)
                return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Field,Value");
            void Row(string label, string? value) =>
                sb.AppendLine($"{label},\"{(value?.Replace("\"", "\"\"") ?? "")}\"");

            Row("HistoryCallId", Sinistro.HistoryCallId);
            Row("Numero", Sinistro.Numero);
            Row("Nome", Sinistro.Nome);
            Row("Cognome", Sinistro.Cognome);
            Row("Targa", Sinistro.Targa);
            Row("DataChiamata", Sinistro.DataChiamata.ToString("o"));
            Row("Gestita", Sinistro.Gestita.ToString());
            Row("HasAllegati", Sinistro.HasAllegati.ToString());
            Row("Trascrizione", Sinistro.Trascrizione);
            Row("Riassunto", Sinistro.Riassunto);
            Row("Evento", Sinistro.Evento);
            Row("Assistenza", Sinistro.Assistenza);
            Row("PropostaAI", Sinistro.PropostaAI);
            Row("SelezioneUtente", Sinistro.SelezioneUtente);

            // Allegati (solo metadata)
            sb.AppendLine();
            sb.AppendLine("AllegatoId,NomeFile,Estensione,DataCaricamento");
            foreach (var a in Allegati)
            {
                sb.AppendLine($"{a.AllegatoId},{a.NomeFile},{a.Estensione},{a.DataCaricamento:o}");
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"Sinistro_{Sinistro.TranscriptionResultId}.csv";
            return File(csvBytes, "text/csv", filename);
        }

        /// <summary>
        /// Export PDF del singolo sinistro
        /// </summary>
        public async Task<IActionResult> OnGetExportPdf(Guid id)
        {
            await OnGetAsync(id);
            if (Sinistro == null)
                return NotFound();

            QuestPDF.Settings.License = LicenseType.Community;
            var estrazione = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            var pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);

                    // Header
                    page.Header().Row(r =>
                    {
                        r.ConstantItem(60).Height(40)
                          .Image("wwwroot/images/LogoFP.png")
                          .FitArea();

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Dettaglio Sinistro {Sinistro.TranscriptionResultId}")
                              .FontSize(14).Bold();
                            c.Item().Text($"Estratto: {estrazione}")
                              .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Contenuto
                    page.Content().Padding(10).Column(col =>
                    {
                        void Row(string label, string? value) =>
                            col.Item().Row(r2 =>
                            {
                                r2.ConstantItem(150).Text(label).SemiBold();
                                r2.RelativeItem().Text(value ?? "");
                            });

                        Row("HistoryCallId", Sinistro.HistoryCallId);
                        Row("Numero", Sinistro.Numero);
                        Row("Nome", Sinistro.Nome);
                        Row("Cognome", Sinistro.Cognome);
                        Row("Targa", Sinistro.Targa);
                        Row("DataChiamata", Sinistro.DataChiamata.ToString("dd/MM/yyyy HH:mm"));
                        Row("Gestita", Sinistro.Gestita ? "✓" : "No");
                        Row("HasAllegati", Sinistro.HasAllegati ? "📎" : "No");
                        Row("Trascrizione", Sinistro.Trascrizione);
                        Row("Riassunto", Sinistro.Riassunto);
                        Row("Evento", Sinistro.Evento);
                        Row("Assistenza", Sinistro.Assistenza);
                        Row("PropostaAI", Sinistro.PropostaAI);
                        Row("SelezioneUtente", Sinistro.SelezioneUtente);

                        if (Allegati.Count > 0)
                        {
                            col.Item().PaddingTop(10).Text("Allegati:").SemiBold();
                            foreach (var a in Allegati)
                            {
                                col.Item().Text($"• {a.NomeFile} ({a.Estensione}), caricato il {a.DataCaricamento:dd/MM/yyyy HH:mm}");
                            }
                        }
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(f =>
                    {
                        f.Span("Pagina ").FontSize(8);
                        f.CurrentPageNumber().FontSize(8);
                        f.Span(" di ").FontSize(8);
                        f.TotalPages().FontSize(8);
                    });
                });
            })
            .GeneratePdf();

            var filename = $"Sinistro_{Sinistro.TranscriptionResultId}.pdf";
            return File(pdf, "application/pdf", filename);
        }
    }
}
