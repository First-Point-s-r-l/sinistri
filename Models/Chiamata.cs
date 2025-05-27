using System.ComponentModel.DataAnnotations;

namespace UfficioSinistri.Models
{
    public class Chiamata
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string HistoryCallId { get; set; }

        [Required]
        public Guid AziendaId { get; set; }

        public string NomeComunicato { get; set; }
        public string CorrispondenzaRubrica { get; set; }
        public string Targa { get; set; }
        public string Testo { get; set; }
        public string Evento { get; set; }
        public string Riassunto { get; set; }
    }
}
