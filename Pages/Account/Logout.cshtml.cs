using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;

namespace AdvSinopia.Pages.Account
{
        public class LogoutModel : PageModel
        {
            public async Task<IActionResult> OnPostAsync()
            {
                await HttpContext.SignOutAsync();   // rimuove il cookie
                return RedirectToPage("/Account/Login");
            }
        }
}
