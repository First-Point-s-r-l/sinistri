using System.ComponentModel.DataAnnotations;

namespace UfficioSinistri.Models
{
    public class Utente
    {
        public Guid Id { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        public string? PasswordHash { get; set; }

        [Required]
        public Guid AziendaId { get; set; }

        public bool IsAdmin { get; set; }

        public bool Attivo { get; set; } = true;
    }
}
