using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UfficioSinistri.Models
{
    public class Chiamata
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public required string HistoryCallId { get; set; }

        [Required]
        public Guid AziendaId { get; set; }

        [Required]
        public required string Numero { get; set; }

        [Required]
        public required string NomeComunicato { get; set; }

        [Required]
        public required string CorrispondenzaRubrica { get; set; }

        [Required]
        public required string Targa { get; set; }

        [Required]
        public required string Testo { get; set; }

        [Required]
        public required string Evento { get; set; }

        [Required]
        public required string Riassunto { get; set; }

        // --- Nuovi campi ---
        [Required]
        public DateTime DataChiamata { get; set; }

        [Required]
        public bool Gestita { get; set; }

        [Required]
        public bool HasAllegati { get; set; }

        // Navigazione verso gli allegati (da mappare in DbContext)
        public ICollection<Allegato> Allegati { get; set; } = [];
    }
}