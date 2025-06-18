// Pages/SinistroDetail.cshtml.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Compression;
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

        // Nuova classe helper per i file
        public class FileAttachment
        {
            public string Name { get; set; } = default!;
            public string FullPath { get; set; } = default!;
            public string Extension { get; set; } = default!;
        }

        // Lista dei file sul disco
        public List<FileAttachment> Attachments { get; set; } = [];


        private readonly AppDbContext _db = db;
        private readonly TenantProvider _tenant = tenant;
        private readonly ILogger<SinistroDetailModel> _logger = logger;

        // Il sinistro caricato
        public Sinistro? Sinistro { get; set; }

        // Gli eventuali allegati
        public List<Allegato> Allegati { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null) return BadRequest();

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

            // Solo se il flag HasAllegati è true vado a guardare sulla cartella
            if (Sinistro.HasAllegati)
            {
                // –– NUOVA LOGICA PER GLI ALLEGATI SU DISCO ––
                var folder = Path.Combine(@"C:\ImmaginiSinistri", id.Value.ToString());
                if (Directory.Exists(folder))
                {
                    Attachments = [.. Directory
                    .EnumerateFiles(folder)
                    .Select(path => new FileAttachment
                    {
                        Name = Path.GetFileName(path),
                        FullPath = path,
                        Extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant()
                    })];
                }
                else
                {
                    // Logga l’anomalia: flag a true ma cartella mancante
                    _logger.LogWarning(
                      "Sinistro {SinistroId} ha HasAllegati=true ma la cartella fisica non esiste: {Folder}",
                      id, folder);
                    // lasciamo Attachments vuota
                }
            }
            else
            {
                // facoltativo: garantisco che sia vuota
                Attachments = [];
            }

            return Page();
        }


        /*
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
        */

        /*
        /// <summary>
        /// Scarica un singolo allegato
        /// </summary>
        public IActionResult OnGetDownloadAsync(string fileName, Guid id)
        {
            var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
            var full = Path.Combine(folder, fileName);
            if (!System.IO.File.Exists(full))
                return NotFound();

            var mime = GetMime(fileName);
            var bytes = System.IO.File.ReadAllBytes(full);
            return File(bytes, mime, fileName);
        }

        /// <summary>
        /// ZIP dei selezionati
        /// </summary>
        public async Task<IActionResult> OnPostDownloadSelectedAsync(
            Guid id,
            [FromForm(Name = "selected")] string[] selected)
        {
            // 1) Ricarica il dettaglio, così popoli Model.Sinistro e Allegati
            var result = await OnGetAsync(id);
            if (result is not PageResult || Sinistro == null)
                return NotFound();

            // 2) Controlla il flag e la cartella
            if (!Sinistro.HasAllegati)
            {
                TempData["Alert"] = "Nessun allegato disponibile.";
                return RedirectToPage();
            }

            var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
            if (!Directory.Exists(folder))
            {
                TempData["Alert"] = "Cartella degli allegati non trovata!";
                return RedirectToPage();
            }

            // 3) Crea lo ZIP in memoria
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in selected.Distinct())
                {
                    var safe = Path.GetFileName(name);
                    var file = Path.Combine(folder, safe);
                    if (!System.IO.File.Exists(file)) continue;

                    var entry = zip.CreateEntry(safe, CompressionLevel.Optimal);
                    using var fs = System.IO.File.OpenRead(file);
                    using var es = entry.Open();
                    await fs.CopyToAsync(es);
                }
            }

            // 4) Torna all’inizio e restituisci
            ms.Position = 0;
            return File(ms.ToArray(),
                        "application/zip",
                        $"Allegati_{id}.zip");
        }


        private static string GetMime(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }
        */


        /// <summary>
        /// Scarica un singolo allegato
        /// </summary>
        public IActionResult OnGetDownloadAsync(string fileName, Guid id)
        {
            var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
            var full = Path.Combine(folder, fileName);
            if (!System.IO.File.Exists(full))
                return NotFound();

            var mime = GetMime(fileName);
            var bytes = System.IO.File.ReadAllBytes(full);
            return File(bytes, mime, fileName);
        }

        /// <summary>
        /// Zippa e scarica tutti i file selezionati
        /// </summary>
        public IActionResult OnPostDownloadSelectedAsync(string[] selected, Guid id)
        {
            var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var name in selected)
                {
                    var full = Path.Combine(folder, name);
                    if (!System.IO.File.Exists(full)) continue;
                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    using var es = entry.Open();
                    using var fs = System.IO.File.OpenRead(full);
                    fs.CopyTo(es);
                }
            }
            ms.Position = 0;
            return File(ms.ToArray(), "application/zip", $"Allegati_{id}.zip");
        }

        private static string GetMime(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }


        /// <summary>
        /// Export CSV del singolo sinistro
        /// </summary>
        public async Task<IActionResult> OnGetExportCsv(Guid id)
        {
            // Ricarica il sinistro e gli allegati
            await OnGetAsync(id);
            if (Sinistro == null)
                return NotFound();

            var sb = new StringBuilder();

            // 1) Header principale
            var headers = new[]
            {
                "HistoryCallId",
                "Numero",
                "Nome",
                "Cognome",
                "Targa",
                "DataChiamata",
                "Gestita",
                "HasAllegati",
                "Trascrizione",
                "Riassunto",
                "Evento",
                "Assistenza",
                "PropostaAI",
                "SelezioneUtente"
            };
            sb.AppendLine(string.Join(',', headers));

            // 2) Valori principale (escape di eventuali virgolette)
            static string Esc(string? v) =>
                string.IsNullOrEmpty(v)
                    ? ""
                    : $"\"{v.Replace("\"", "\"\"")}\"";

            var values = new[]
            {
                Esc(Sinistro.HistoryCallId),
                Esc(Sinistro.Numero),
                Esc(Sinistro.Nome),
                Esc(Sinistro.Cognome),
                Esc(Sinistro.Targa),
                Esc(Sinistro.DataChiamata.ToString("o")),
                Sinistro.Gestita.ToString(),
                Sinistro.HasAllegati.ToString(),
                Esc(Sinistro.Trascrizione),
                Esc(Sinistro.Riassunto),
                Esc(Sinistro.Evento),
                Esc(Sinistro.Assistenza),
                Esc(Sinistro.PropostaAI),
                Esc(Sinistro.SelezioneUtente)
            };
            sb.AppendLine(string.Join(',', values));

            // 3) Riga vuota di separazione
            sb.AppendLine();

            // 4) Allegati: header + dati
            sb.AppendLine("AllegatoId,NomeFile,Estensione,DataCaricamento");
            foreach (var a in Allegati)
            {
                var line = new[]
                {
            a.AllegatoId.ToString(),
            Esc(a.NomeFile),
            Esc(a.Estensione),
            a.DataCaricamento.ToString("o")
        };
                sb.AppendLine(string.Join(',', line));
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
            // Ricarica i dati
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
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    //page.PageColor(Colors.Grey.Lighten5);

                    // ─────────────────────────────────────────────────────── Intestazione ──
                    page.Header().PaddingBottom(10).Row(row =>
                    {
                        // Logo
                        row.ConstantItem(80).Height(40)
                           .Background(Colors.White)
                           .Image("wwwroot/images/LogoFP.png")
                           .FitArea();

                        // Titolo + data
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Dettaglio Sinistro")
                                     .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"ID: {Sinistro.TranscriptionResultId}")
                                     .FontSize(10).FontColor(Colors.Grey.Darken2);
                            col.Item().Text($"Estratto: {estrazione}")
                                     .FontSize(8).Italic().FontColor(Colors.Grey.Lighten1);
                        });
                    });

                    // ─────────────────────────────────────────────────────────── Dettaglio ──
                    page.Content().Padding(5).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(150); // etichette
                            cd.RelativeColumn();    // valori
                        });

                        // helper per riga compatta
                        void AddRow(string label, string? value)
                        {
                            // cella etichetta
                            var cell1 = table.Cell();
                            cell1.Background(Colors.White)
                                 .Border(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(5)
                                 .Text(label)
                                 .SemiBold()
                                 .FontSize(10)
                                 .FontColor(Colors.Blue.Darken2);

                            // cella valore
                            var cell2 = table.Cell();
                            cell2.Background(Colors.White)
                                 .Border(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(5)
                                 .Text(value ?? "-")
                                 .FontSize(10)
                                 .FontColor(Colors.Black);
                        }

                        // helper per campo multilinea
                        void AddBigField(string label, string? value)
                        {
                            // etichetta
                            var cell1 = table.Cell();
                            cell1.Background(Colors.Grey.Lighten4)
                                 .Border(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(5)
                                 .Text(label)
                                 .SemiBold()
                                 .FontSize(10)
                                 .FontColor(Colors.Blue.Darken2);

                            // contenuto multilinea
                            var cell2 = table.Cell();
                            cell2.Background(Colors.White)
                                 .Border(1).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(5)
                                 .Column(col =>
                                 {
                                     col.Spacing(2);
                                     foreach (var line in (value ?? "-").Split('\n'))
                                     {
                                         col.Item()
                                            .Text(line)
                                            .FontSize(9)
                                            .FontColor(Colors.Black);
                                     }
                                 });
                        }

                        // campi semplici
                        AddRow("HistoryCallId", Sinistro.HistoryCallId);
                        AddRow("Numero", Sinistro.Numero);
                        AddRow("Nome", Sinistro.Nome);
                        AddRow("Cognome", Sinistro.Cognome);
                        AddRow("Targa", Sinistro.Targa);
                        AddRow("Data Chiamata", Sinistro.DataChiamata.ToString("dd/MM/yyyy HH:mm"));
                        AddRow("Gestita", Sinistro.Gestita ? "✓" : "✖");
                        AddRow("Allegati", Sinistro.HasAllegati ? "📎" : "—");

                        // campi multilinea
                        AddBigField("Trascrizione", Sinistro.Trascrizione);
                        AddBigField("Riassunto", Sinistro.Riassunto);
                        AddBigField("Evento", Sinistro.Evento);
                        AddBigField("Assistenza", Sinistro.Assistenza);
                        AddBigField("Proposta AI", Sinistro.PropostaAI);
                        AddBigField("Selezione Utente", Sinistro.SelezioneUtente);
                    });

                    // ────────────────────────────────────────────────────────── Allegati ──
                    if (Allegati.Count > 0)
                    {
                        page.Content().PaddingTop(10).Text("Allegati").SemiBold().FontSize(12);

                        page.Content().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(20);   // icona
                                cd.RelativeColumn(3);    // nome
                                cd.RelativeColumn(2);    // estensione
                                cd.RelativeColumn(2);    // data
                            });

                            // header
                            tbl.Header(header =>
                            {
                                header.Cell().Padding(4).Background(Colors.Blue.Darken2)
                                      .Text(" ").FontColor(Colors.White);
                                header.Cell().Padding(4).Background(Colors.Blue.Darken2)
                                      .Text("Nome File").FontColor(Colors.White).SemiBold();
                                header.Cell().Padding(4).Background(Colors.Blue.Darken2)
                                      .Text("Estensione").FontColor(Colors.White).SemiBold();
                                header.Cell().Padding(4).Background(Colors.Blue.Darken2)
                                      .Text("Data").FontColor(Colors.White).SemiBold();
                            });

                            // righe dati
                            foreach (var a in Allegati)
                            {
                                tbl.Cell().Padding(4).Text("📎");
                                tbl.Cell().Padding(4).Text(a.NomeFile).FontSize(9);
                                tbl.Cell().Padding(4).Text(a.Estensione ?? "-").FontSize(9);
                                tbl.Cell().Padding(4)
                                   .Text(a.DataCaricamento.ToString("dd/MM/yyyy HH:mm"))
                                   .FontSize(9);
                            }
                        });
                    }

                    // ───────────────────────────────────────────────────────────── Footer ──
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Pagina ").FontSize(8);
                        txt.CurrentPageNumber().FontSize(8);
                        txt.Span(" di ").FontSize(8);
                        txt.TotalPages().FontSize(8);
                        txt.Span($"  •  Generated {DateTime.Now:dd/MM/yyyy HH:mm}")
                           .FontSize(7).FontColor(Colors.Grey.Lighten1);
                    });
                });
            })
            .GeneratePdf();

            var filename = $"Sinistro_{Sinistro.TranscriptionResultId}.pdf";
            return File(pdf, "application/pdf", filename);
        }

    }

}
