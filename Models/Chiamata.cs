using System.ComponentModel.DataAnnotations;

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

        public required string Numero { get; set; }

        public required string NomeComunicato { get; set; }
        public required string CorrispondenzaRubrica { get; set; }
        public required string Targa { get; set; }
        public required string Testo { get; set; }
        public required string Evento { get; set; }
        public required string Riassunto { get; set; }
    }
}
