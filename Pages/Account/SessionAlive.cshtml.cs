using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UfficioSinistri.Pages.Account
{
    [Authorize]
    public class SessionAliveModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated ?? false)
                return new OkResult();
            return new UnauthorizedResult();
        }
    }
} 