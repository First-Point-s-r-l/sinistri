// Models/Sinistro.cs
using System;
using System.Collections.Generic;

namespace UfficioSinistri.Models
{
    public class Sinistro
    {
        public Guid TranscriptionResultId { get; set; }
        public Guid AziendaId { get; set; }
        public string HistoryCallId { get; set; } = null!;
        public string? Numero { get; set; }
        public string? Nome { get; set; }
        public string? Cognome { get; set; }
        public string? Targa { get; set; }
        public DateTime DataChiamata { get; set; }
        public bool Gestita { get; set; }
        public bool HasAllegati { get; set; }
        public string? Trascrizione { get; set; }
        public string? Riassunto { get; set; }
        public string? Evento { get; set; }
        public string? Assistenza { get; set; }
        public string? PropostaAI { get; set; }
        public string? SelezioneUtente { get; set; }

        // Lista di allegati
        public ICollection<Allegato> Allegati { get; set; } = [];
    }
}