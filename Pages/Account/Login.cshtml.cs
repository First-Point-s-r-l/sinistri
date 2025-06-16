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
        public string Email { get; set; }

        [BindProperty, Required(ErrorMessage = "Campo password obbligatorio"), DataType(DataType.Password)]
        public string Password { get; set; }

        public string Errore { get; set; }

        private const int MaxTentativiFalliti = 3;

        public bool PasswordSuccess { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            var token = Request.Form["g-recaptcha-response"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Errore = "❌ Token reCAPTCHA mancante nel POST.";
                return Page();
            }
            if (!await VerificaRecaptchaAsync(token))
            {
                Errore = "⚠️ Verifica reCAPTCHA fallita.";
                return Page();
            }

            if (!ModelState.IsValid)
                return Page();

            var utente = _db.Utenti.FirstOrDefault(u => u.Email == Email);

            if (utente == null)
            {
                Errore = "Credenziali errate.";
                return Page();
            }

            if (!utente.Attivo)
            {
                Errore = "Account bloccato.";
                return Page();
            }

            if (utente.PasswordHash != HashPassword(Password.Trim()))
            {
                var inserita = HashPassword(Password);
                var salvata = utente.PasswordHash;

                Console.WriteLine($"Inserita: {inserita}");
                Console.WriteLine($"Salvata: {salvata}");
                // Tentativo fallito
                var key = $"Tentativi_{Email}";
                int tentativi = HttpContext.Session.GetInt32(key) ?? 0;
                tentativi++;
                HttpContext.Session.SetInt32(key, tentativi);

                if (tentativi >= MaxTentativiFalliti)
                {
                    utente.Attivo = false;
                    await _db.SaveChangesAsync();

                    Errore = "Troppi tentativi falliti. Account bloccato.";
                    return Page();
                }
                else
                {
                    Errore = $"Credenziali errate. Tentativo {tentativi} di {MaxTentativiFalliti}.";
                    return Page();
                }
            }

            // Login riuscito
            // Reset dei tentativi
            HttpContext.Session.Remove($"Tentativi_{Email}");

            HttpContext.Session.SetString("Email", utente.Email);
            HttpContext.Session.SetString("Azienda", utente.AziendaId.ToString());
            HttpContext.Session.SetString("IsAdmin", utente.IsAdmin.ToString());

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, utente.Email),
                new Claim(ClaimTypes.Role, utente.IsAdmin ? "Admin" : "User")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToPage("/Sinistri");
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
        private async Task<bool> VerificaRecaptchaAsync(string token)
        {
            var secret = _config["Recaptcha:SecretKey"];
            var client = new HttpClient();

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
