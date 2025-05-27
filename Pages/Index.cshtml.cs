using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UfficioSinistri.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IActionResult OnGet()
        {
            if (User.Identity.IsAuthenticated)
            {
                // Se sei loggato ➔ vai subito su Chiamate
                return RedirectToPage("/Chiamate");
            }
            else
            {
                // Se non sei loggato ➔ vai su Login
                return RedirectToPage("/Account/Login");
            }
        }
    }
}
