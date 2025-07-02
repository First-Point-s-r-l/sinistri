using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using UfficioSinistri.Data;

namespace UfficioSinistri.Pages.Account
{
    public class SetPasswordModel(AppDbContext db) : PageModel
    {
        private readonly AppDbContext _db = db;

        [BindProperty]
        public string Token { get; set; } = string.Empty;
        [BindProperty]
        [Required]
        public string Password { get; set; } = string.Empty;
        [BindProperty]
        [Required]
        public string ConfermaPassword { get; set; } = string.Empty;

        public string? Errore { get; set; }
        public bool Success { get; set; }
        public bool TokenNonValido { get; set; }
        public string TitoloPagina { get; set; } = "Imposta password";

        private static bool PasswordValida(string pwd)
        {
            if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 8) return false;
            if (!pwd.Any(char.IsUpper)) return false;
            if (!pwd.Any(ch => "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?".Contains(ch))) return false;
            return true;
        }

        public async Task<IActionResult> OnGetAsync(string? token)
        {
            Token = token ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Token)) { TokenNonValido = true; return Page(); }
            var tokenHash = HashToken(Token);
            var now = DateTime.UtcNow;
            var reset = await _db.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.Used && t.ExpiresAt > now);
            if (reset == null || reset.User == null)
            {
                TokenNonValido = true;
                return Page();
            }
            TitoloPagina = reset.User.PasswordHash == "N" ? "Imposta password" : "Reimposta password";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Errore = null;
            if (string.IsNullOrWhiteSpace(Token)) { TokenNonValido = true; return Page(); }
            if (string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfermaPassword))
            { Errore = "Compila tutti i campi."; return Page(); }
            if (Password != ConfermaPassword)
            { Errore = "Le password non coincidono."; return Page(); }
            if (!PasswordValida(Password))
            { Errore = "La password deve avere almeno 8 caratteri, una maiuscola e un simbolo."; return Page(); }
            var tokenHash = HashToken(Token);
            var now = DateTime.UtcNow;
            var reset = await _db.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.Used && t.ExpiresAt > now);
            if (reset == null || reset.User == null)
            {
                TokenNonValido = true;
                return Page();
            }
            // Aggiorna password e marca token come usato
            reset.Used = true;
            reset.User.PasswordHash = HashPassword(Password);
            await _db.SaveChangesAsync();
            Success = true;
            return Page();
        }

        private static string HashToken(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToBase64String(SHA256.HashData(bytes));
        }
        private static string HashPassword(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(SHA256.HashData(bytes));
        }
    }
} 