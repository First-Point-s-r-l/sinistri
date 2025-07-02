using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UfficioSinistri.Models
{
    public class PasswordResetToken
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public Guid UserId { get; set; }
        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;
        [Required]
        public DateTime ExpiresAt { get; set; }
        [Required]
        public bool Used { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("UserId")]
        public Utente? User { get; set; }
    }
} 