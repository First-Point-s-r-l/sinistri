﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UfficioSinistri.Pages
{
    public class IndexModel : PageModel
    {
        //private readonly ILogger<IndexModel> _logger;  

        public IActionResult OnGet()
        {
            if (User?.Identity?.IsAuthenticated == true) // Added null checks for User and Identity  
            {
                // Se sei loggato ➔ vai subito su Chiamate  
                return RedirectToPage("/Sinistri");
            }
            else
            {
                // Se non sei loggato ➔ vai su Login  
                return RedirectToPage("/Account/Login");
            }
        }
    }
}
