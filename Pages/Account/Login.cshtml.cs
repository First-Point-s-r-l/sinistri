using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UfficioSinistri.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

//                                                                            "=^.^="
namespace UfficioSinistri.Pages.Account
{
    public class LoginModel(AppDbContext db, IConfiguration config) : PageModel
    {

        private readonly AppDbContext _db = db;
        private readonly IConfiguration _config = config;

        [BindProperty, Required(ErrorMessage = "Campo mail obbligatorio"), EmailAddress]
        public required string Email { get; set; }

        [BindProperty, Required(ErrorMessage = "Campo password obbligatorio"), DataType(DataType.Password)]
        public required string Password { get; set; }

        public required string Errore { get; set; }

        private const int MaxTentativiFalliti = 3;

        public bool PasswordSuccess { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            // 1) reCAPTCHA
            // Leggiamo sempre una stringa (mai null), anche se vuota
            //var token = Request.Form["g-recaptcha-response"].FirstOrDefault() ?? "";
            string? token = Request.Form["g-recaptcha-response"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Errore = "❌ Token reCAPTCHA mancante nel POST.";
                return Page();
            }
            // Adesso VerificaRecaptchaAsync prende sempre una stringa non-null
            if (!await VerificaRecaptchaAsync(token))
            {
                Errore = "⚠️ Verifica reCAPTCHA fallita.";
                return Page();
            }

            if (!ModelState.IsValid)
                return Page();

            // 2) Carica utente
            var utente = _db.Utenti.FirstOrDefault(u => u.Email == Email);
            if (utente == null || !utente.Attivo)
            {
                Errore = utente == null
                    ? "Credenziali errate."
                    : "Account bloccato.";
                return Page();
            }

            // 3) Verifica password e blocco per troppi tentativi
            if (utente.PasswordHash != HashPassword(Password.Trim()))
            {
                var key = $"Tentativi_{Email}";
                int tentativi = HttpContext.Session.GetInt32(key) ?? 0;
                tentativi++;
                HttpContext.Session.SetInt32(key, tentativi);

                if (tentativi >= MaxTentativiFalliti)
                {
                    utente.Attivo = false;
                    await _db.SaveChangesAsync();
                    Errore = "Troppi tentativi falliti. Account bloccato.";
                }
                else
                {
                    Errore = $"Credenziali errate. Tentativo {tentativi} di {MaxTentativiFalliti}.";
                }
                return Page();
            }

            // 4) Login OK: reset tentativi e set sessione
            HttpContext.Session.Remove($"Tentativi_{Email}");
            HttpContext.Session.SetString("Email", utente.Email);
            HttpContext.Session.SetString("Azienda", utente.AziendaId.ToString());
            HttpContext.Session.SetString("IsAdmin", utente.IsAdmin.ToString());

            // 5) Preparo i claim, inclusi AziendaId e NameIdentifier
            var claims = new List<Claim>
            {
                // identifica univocamente l'utente
                new(ClaimTypes.NameIdentifier, utente.Id.ToString()),
                // email o username
                new(ClaimTypes.Name, utente.Email),
                // ruolo
                new(ClaimTypes.Role, utente.IsAdmin ? "Admin" : "User"),
                // tenant multitenant
                new("AziendaId", utente.AziendaId.ToString())
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // 6) Firma il cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            return RedirectToPage("/Sinistri");
        }


        private static string HashPassword(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(SHA256.HashData(bytes));
        }
        private async Task<bool> VerificaRecaptchaAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var secret = _config["Recaptcha:SecretKey"] ?? "";
            var client = new HttpClient();

            // Passiamo sempre una stringa non-null in Add
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "secret", secret },
                    { "response", token }
                }));

            var json = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonDocument.Parse(json);
            return result.RootElement.GetProperty("success").GetBoolean();
        }

        public void OnGet(bool passwordSuccess = false)
        {
            PasswordSuccess = passwordSuccess;
        }
    }
}
