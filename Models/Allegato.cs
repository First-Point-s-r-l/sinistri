// Models/Allegato.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UfficioSinistri.Models
{
    public class Allegato
    {
        [Key]
        public Guid AllegatoId { get; set; }

        [Required]
        public Guid SinistroId { get; set; }

        [ForeignKey(nameof(SinistroId))]
        public Sinistro Sinistro { get; set; } = null!;

        [Required]
        public Guid AziendaId { get; set; }

        [Required, MaxLength(255)]
        public string NomeFile { get; set; } = null!;

        [MaxLength(50)]
        public string? Estensione { get; set; }

        public byte[]? FileData { get; set; }

        [Required]
        public DateTime DataCaricamento { get; set; }
    }
}