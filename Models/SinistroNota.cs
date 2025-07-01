using System;

namespace UfficioSinistri.Models
{
    public class SinistroNota
    {
        public Guid Id { get; set; }
        public Guid SinistroId { get; set; }
        public Guid AziendaId { get; set; }
        public Guid OperatoreId { get; set; }
        public string? OperatoreNome { get; set; }
        public string Testo { get; set; } = null!;
        public DateTime DataCreazione { get; set; }
    }
} 