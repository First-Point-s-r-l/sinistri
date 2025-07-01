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
using Microsoft.Extensions.Configuration;
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
        ILogger<SinistroDetailModel> logger,
        IConfiguration config) : PageModel
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
        private readonly string _connectionString = config.GetConnectionString("DefaultConnection")!;

        // Il sinistro caricato
        public Sinistro? Sinistro { get; set; }

        // Gli eventuali allegati
        public List<Allegato> Allegati { get; set; } = [];

        [BindProperty]
        public string? NoteOp { get; set; }
        [BindProperty]
        public bool Gestita { get; set; }

        public List<SinistroNota> NoteStoriche { get; set; } = [];

        [BindProperty]
        public string? NuovaNotaStorica { get; set; }

        public string? OperatoreChiusuraEmail { get; set; }

        // Handler upload multiplo allegati
        [BindProperty]
        public IFormFile[]? allegati { get; set; }

        public async Task CaricaNoteStoricheAsync(Guid sinistroId)
        {
            NoteStoriche.Clear();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "Sp_SinistroNote_GetBySinistro";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@SinistroId", sinistroId));
            cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                NoteStoriche.Add(new SinistroNota
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    SinistroId = reader.GetGuid(reader.GetOrdinal("SinistroId")),
                    AziendaId = reader.GetGuid(reader.GetOrdinal("AziendaId")),
                    OperatoreId = reader.GetGuid(reader.GetOrdinal("OperatoreId")),
                    OperatoreNome = reader.IsDBNull(reader.GetOrdinal("OperatoreNome")) ? null : reader.GetString(reader.GetOrdinal("OperatoreNome")),
                    Testo = reader.GetString(reader.GetOrdinal("Testo")),
                    DataCreazione = reader.GetDateTime(reader.GetOrdinal("DataCreazione"))
                });
            }
        }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null) return BadRequest();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_Sinistri_GetById";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id.Value));

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    Sinistro = new Sinistro
                    {
                        TranscriptionResultId = reader.GetGuid(reader.GetOrdinal("TranscriptionResultId")),
                        AziendaId = reader.GetGuid(reader.GetOrdinal("AziendaId")),
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
                        SelezioneUtente = reader.IsDBNull(reader.GetOrdinal("SelezioneUtente")) ? null : reader.GetString(reader.GetOrdinal("SelezioneUtente")),
                        NoteOp = reader.IsDBNull(reader.GetOrdinal("NoteOp")) ? null : reader.GetString(reader.GetOrdinal("NoteOp")),
                        OperatoreId = reader.IsDBNull(reader.GetOrdinal("OperatoreId")) ? null : reader.GetGuid(reader.GetOrdinal("OperatoreId")),
                        DataChiusura = reader.IsDBNull(reader.GetOrdinal("DataChiusura")) ? null : reader.GetDateTime(reader.GetOrdinal("DataChiusura"))
                    };
                }
                else
                {
                    return NotFound();
                }
            }

            // Recupero la mail dell'operatore che ha chiuso (se presente)
            OperatoreChiusuraEmail = null;
            if (Sinistro?.OperatoreId != null)
            {
                var opId = Sinistro.OperatoreId.Value;
                var utente = await _db.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Id == opId);
                if (utente != null)
                    OperatoreChiusuraEmail = utente.Email;
            }

            // Solo se il flag HasAllegati è true vado a guardare sulla cartella
            if (Sinistro != null && Sinistro.HasAllegati)
            {
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
                    _logger.LogWarning(
                      "Sinistro {SinistroId} ha HasAllegati=true ma la cartella fisica non esiste: {Folder}",
                      id, folder);
                }
            }
            else
            {
                Attachments = [];
            }

            await CaricaNoteStoricheAsync(id.Value);
            return Page();
        }

        /// <summary>
        /// Scarica un singolo allegato
        /// </summary>
        public IActionResult OnGetDownloadAsync(string fileName, Guid id, bool preview = false)
        {
            var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
            var full = Path.Combine(folder, fileName);
            if (!System.IO.File.Exists(full))
                return NotFound();

            var mime = GetMime(fileName);
            var bytes = System.IO.File.ReadAllBytes(full);
            var cd = preview ? null : fileName; // se preview, non forzare download
            if (preview)
                Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
            return File(bytes, mime, cd);
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
            var allegatiFolder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());

            var pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);

                    // HEADER
                    page.Header().PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(80).Height(40)
                           .Background(Colors.White)
                           .Image("wwwroot/images/LogoFP.png")
                           .FitArea();
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

                    // UNICA CHIAMATA CONTENT
                    page.Content().Column(col =>
                    {
                        // Dati principali
                        col.Item().Padding(5).Table(table =>
                        {
                            table.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(150); // etichette
                                cd.RelativeColumn();    // valori
                            });
                            void AddRow(string label, string? value)
                            {
                                var cell1 = table.Cell();
                                cell1.Background(Colors.White)
                                     .Border(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(5)
                                     .Text(label)
                                     .SemiBold()
                                     .FontSize(10)
                                     .FontColor(Colors.Blue.Darken2);
                                var cell2 = table.Cell();
                                cell2.Background(Colors.White)
                                     .Border(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(5)
                                     .Text(value ?? "-")
                                     .FontSize(10)
                                     .FontColor(Colors.Black);
                            }
                            void AddBigField(string label, string? value)
                            {
                                var cell1 = table.Cell();
                                cell1.Background(Colors.Grey.Lighten4)
                                     .Border(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(5)
                                     .Text(label)
                                     .SemiBold()
                                     .FontSize(10)
                                     .FontColor(Colors.Blue.Darken2);
                                var cell2 = table.Cell();
                                cell2.Background(Colors.White)
                                     .Border(1).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(5)
                                     .Column(col2 =>
                                     {
                                         col2.Spacing(2);
                                         foreach (var line in (value ?? "-").Split('\n'))
                                         {
                                             col2.Item()
                                                .Text(line)
                                                .FontSize(9)
                                                .FontColor(Colors.Black);
                                         }
                                     });
                            }
                            AddRow("HistoryCallId", Sinistro.HistoryCallId);
                            AddRow("Numero", Sinistro.Numero);
                            AddRow("Nome", Sinistro.Nome);
                            AddRow("Cognome", Sinistro.Cognome);
                            AddRow("Targa", Sinistro.Targa);
                            AddRow("Data Chiamata", Sinistro.DataChiamata.ToString("dd/MM/yyyy HH:mm"));
                            AddRow("Gestita", Sinistro.Gestita ? "✓" : "✖");
                            AddRow("Allegati", Sinistro.HasAllegati ? "📎" : "—");
                            AddBigField("Trascrizione", Sinistro.Trascrizione);
                            AddBigField("Riassunto", Sinistro.Riassunto);
                            AddBigField("Evento", Sinistro.Evento);
                            AddBigField("Assistenza", Sinistro.Assistenza);
                            AddBigField("Proposta AI", Sinistro.PropostaAI);
                            AddBigField("Selezione Utente", Sinistro.SelezioneUtente);
                        });

                        // Note storiche
                        if (NoteStoriche.Count > 0)
                        {
                            col.Item().PaddingTop(10).Text("Storico note operatore").SemiBold().FontSize(12);
                            col.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn(2); // Operatore
                                    cd.RelativeColumn(2); // Data
                                    cd.RelativeColumn(6); // Testo
                                });
                                tbl.Header(header =>
                                {
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text("Operatore").FontColor(Colors.White).SemiBold();
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text("Data").FontColor(Colors.White).SemiBold();
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text("Nota").FontColor(Colors.White).SemiBold();
                                });
                                foreach (var n in NoteStoriche)
                                {
                                    tbl.Cell().Padding(4).Text(n.OperatoreNome ?? "-").FontSize(9);
                                    tbl.Cell().Padding(4).Text(n.DataCreazione.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                                    tbl.Cell().Padding(4).Text(n.Testo).FontSize(9);
                                }
                            });
                        }

                        // Allegati
                        if (Allegati.Count > 0)
                        {
                            col.Item().PaddingTop(10).Text("Allegati").SemiBold().FontSize(12);
                            col.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(cd =>
                                {
                                    cd.ConstantColumn(30);   // miniatura/icona
                                    cd.RelativeColumn(4);   // nome
                                    cd.RelativeColumn(2);   // data
                                });
                                tbl.Header(header =>
                                {
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text(" ").FontColor(Colors.White);
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text("Nome File").FontColor(Colors.White).SemiBold();
                                    header.Cell().Padding(4).Background(Colors.Blue.Darken2).Text("Data").FontColor(Colors.White).SemiBold();
                                });
                                foreach (var a in Allegati)
                                {
                                    var ext = (a.Estensione ?? "").ToLowerInvariant();
                                    if (ext == "jpg" || ext == "jpeg" || ext == "png" || ext == "gif")
                                    {
                                        var imgPath = Path.Combine(allegatiFolder, a.NomeFile);
                                        if (System.IO.File.Exists(imgPath))
                                        {
                                            var imgBytes = System.IO.File.ReadAllBytes(imgPath);
                                            tbl.Cell().Padding(4).Element(container =>
                                                container.MaxWidth(60).MaxHeight(60).Image(imgBytes)
                                            );
                                        }
                                        else
                                        {
                                            tbl.Cell().Padding(4).Text("[img]");
                                        }
                                    }
                                    else if (ext == "pdf")
                                    {
                                        tbl.Cell().Padding(4).Text("[PDF]");
                                    }
                                    else if (ext == "mp4")
                                    {
                                        tbl.Cell().Padding(4).Text("[VIDEO]");
                                    }
                                    else
                                    {
                                        tbl.Cell().Padding(4).Text("[FILE]");
                                    }
                                    tbl.Cell().Padding(4).Text(a.NomeFile).FontSize(9);
                                    tbl.Cell().Padding(4).Text(a.DataCaricamento.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                                }
                            });
                        }
                    });

                    // FOOTER
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

        public async Task<IActionResult> OnPostSaveNotaOperatoreAsync(Guid id)
        {
            var userIdStr = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            var userName = User.Identity?.Name ?? "Operatore";
            if (!Guid.TryParse(userIdStr, out var operatoreId))
            {
                ModelState.AddModelError(string.Empty, "Impossibile determinare l'operatore autenticato.");
                return RedirectToPage(new { id });
            }

            // Carica lo stato attuale del sinistro
            Sinistro? sinistro = null;
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_Sinistri_GetById";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id));
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    sinistro = new Sinistro
                    {
                        Gestita = reader.GetBoolean(reader.GetOrdinal("Gestita")),
                        NoteOp = reader.IsDBNull(reader.GetOrdinal("NoteOp")) ? null : reader.GetString(reader.GetOrdinal("NoteOp")),
                        OperatoreId = reader.IsDBNull(reader.GetOrdinal("OperatoreId")) ? null : reader.GetGuid(reader.GetOrdinal("OperatoreId")),
                        DataChiusura = reader.IsDBNull(reader.GetOrdinal("DataChiusura")) ? null : reader.GetDateTime(reader.GetOrdinal("DataChiusura"))
                    };
                }
            }
            if (sinistro == null)
                return NotFound();

            // Se si sta chiudendo (Gestita true e prima era false), obbliga nota di chiusura
            if (Gestita && !sinistro.Gestita)
            {
                if (string.IsNullOrWhiteSpace(NoteOp))
                {
                    ModelState.AddModelError(string.Empty, "Devi inserire una nota di chiusura.");
                    return RedirectToPage(new { id });
                }
                // Salva la nota master e aggiorna OperatoreId/DataChiusura
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "Sp_Sinistri_UpdateNoteOperatore";
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id));
                    cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                    cmd.Parameters.Add(new SqlParameter("@OperatoreId", operatoreId));
                    cmd.Parameters.Add(new SqlParameter("@NoteOp", NoteOp));
                    cmd.Parameters.Add(new SqlParameter("@Gestita", true));
                    cmd.Parameters.Add(new SqlParameter("@DataChiusura", DateTime.Now));
                    await cmd.ExecuteNonQueryAsync();
                }
                // Salva anche nello storico
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "Sp_SinistroNote_Aggiungi";
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@SinistroId", id));
                    cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                    cmd.Parameters.Add(new SqlParameter("@OperatoreId", operatoreId));
                    cmd.Parameters.Add(new SqlParameter("@Testo", "[CHIUSURA] " + NoteOp));
                    cmd.Parameters.Add(new SqlParameter("@OperatoreNome", userName));
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            else if (!Gestita && sinistro.Gestita)
            {
                // Se si riapre, aggiorna solo il flag Gestita (nota master rimane, ma puoi aggiungere una nota storica opzionale)
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_Sinistri_UpdateNoteOperatore";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id));
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                cmd.Parameters.Add(new SqlParameter("@OperatoreId", operatoreId));
                cmd.Parameters.Add(new SqlParameter("@NoteOp", sinistro.NoteOp ?? (object)DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Gestita", false));
                cmd.Parameters.Add(new SqlParameter("@DataChiusura", DBNull.Value));
                await cmd.ExecuteNonQueryAsync();
                // (Opzionale: aggiungi nota storica di riapertura)
            }
            else if (!Gestita && !sinistro.Gestita)
            {
                // Solo aggiorna la nota operatore (non master) se non è gestita
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_Sinistri_UpdateNoteOperatore";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@TranscriptionResultId", id));
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                cmd.Parameters.Add(new SqlParameter("@OperatoreId", operatoreId));
                cmd.Parameters.Add(new SqlParameter("@NoteOp", NoteOp ?? (object)DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Gestita", false));
                cmd.Parameters.Add(new SqlParameter("@DataChiusura", DBNull.Value));
                await cmd.ExecuteNonQueryAsync();
            }
            // Se già gestita e si prova a modificare, non permettere modifica

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAggiungiNotaStoricaAsync(Guid id)
        {
            var userIdStr = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            var userName = User.Identity?.Name ?? "Operatore";
            if (!Guid.TryParse(userIdStr, out var operatoreId))
            {
                ModelState.AddModelError(string.Empty, "Impossibile determinare l'operatore autenticato.");
                return RedirectToPage(new { id });
            }
            if (string.IsNullOrWhiteSpace(NuovaNotaStorica))
            {
                ModelState.AddModelError(string.Empty, "La nota non può essere vuota.");
                return RedirectToPage(new { id });
            }
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_SinistroNote_Aggiungi";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@SinistroId", id));
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                cmd.Parameters.Add(new SqlParameter("@OperatoreId", operatoreId));
                cmd.Parameters.Add(new SqlParameter("@Testo", NuovaNotaStorica));
                cmd.Parameters.Add(new SqlParameter("@OperatoreNome", userName));
                await cmd.ExecuteNonQueryAsync();
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostEliminaNotaStoricaAsync(Guid id, Guid notaId)
        {
            // Recupera info utente
            var userIdStr = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            var isAdmin = User.IsInRole("Admin");
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                TempData["Alert"] = "Impossibile determinare l'utente autenticato.";
                return RedirectToPage(new { id });
            }
            // Recupera la nota
            SinistroNota? nota = null;
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_SinistroNote_GetBySinistro";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@SinistroId", id));
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var nid = reader.GetGuid(reader.GetOrdinal("Id"));
                    if (nid == notaId)
                    {
                        nota = new SinistroNota
                        {
                            Id = nid,
                            OperatoreId = reader.GetGuid(reader.GetOrdinal("OperatoreId")),
                            OperatoreNome = reader.IsDBNull(reader.GetOrdinal("OperatoreNome")) ? null : reader.GetString(reader.GetOrdinal("OperatoreNome")),
                            Testo = reader.GetString(reader.GetOrdinal("Testo")),
                            DataCreazione = reader.GetDateTime(reader.GetOrdinal("DataCreazione")),
                            AziendaId = reader.GetGuid(reader.GetOrdinal("AziendaId")),
                            SinistroId = reader.GetGuid(reader.GetOrdinal("SinistroId"))
                        };
                        break;
                    }
                }
            }
            if (nota == null)
            {
                TempData["Alert"] = "Nota non trovata.";
                return RedirectToPage(new { id });
            }
            // Permesso: autore o admin
            if (nota.OperatoreId != userId && !isAdmin)
            {
                TempData["Alert"] = "Non hai i permessi per eliminare questa nota.";
                return RedirectToPage(new { id });
            }
            // Elimina
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Sp_SinistroNote_Elimina";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@Id", notaId));
                cmd.Parameters.Add(new SqlParameter("@AziendaId", _tenant.AziendaId));
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                    TempData["Success"] = "Nota eliminata con successo.";
                else
                    TempData["Alert"] = "Errore nell'eliminazione della nota.";
            }
            return RedirectToPage(new { id });
        }

        // Handler upload multiplo allegati
        public async Task<IActionResult> OnPostUploadAllegatiAsync(Guid id)
        {
            if (allegati != null && allegati.Length > 0)
            {
                var folder = Path.Combine(@"C:\ImmaginiSinistri", id.ToString());
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                foreach (var file in allegati)
                {
                    var dest = Path.Combine(folder, Path.GetFileName(file.FileName));
                    using var fs = new FileStream(dest, FileMode.Create);
                    await file.CopyToAsync(fs);
                }
            }
            return RedirectToPage(new { id });
        }

        // Ordinamento allegati lato backend
        public List<FileAttachment> GetSortedAttachments(string sort = "nome")
        {
            if (Attachments == null) return [];
            return sort == "data"
                ? [.. Attachments.OrderByDescending(a => System.IO.File.GetCreationTime(a.FullPath))]
                : [.. Attachments.OrderBy(a => a.Name)];
        }

        /// <summary>
        /// Download multiplo: zippa e scarica tutti i file selezionati
        /// </summary>
        public IActionResult OnPostDownloadSelected(string[] selected, Guid id)
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

        // Funzione helper per il mime-type
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
    }
}
