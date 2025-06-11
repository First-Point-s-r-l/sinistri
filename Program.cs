using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using UfficioSinistri.Data;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

//--------------------------------------------------------------------------------------------------
// 1) Carica il certificato per Kestrel (HTTPS)
//--------------------------------------------------------------------------------------------------
var certThumbprint = "B627FB1093AF3CD052D21C219029A2155DFF8A84";
using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
store.Open(OpenFlags.ReadOnly);
var certs = store.Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, validOnly: false);
store.Close();

if (certs.Count == 0)
    throw new Exception("❌ Certificato non trovato.");

var certificate = certs[0];
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(7285, listenOptions =>
    {
        listenOptions.UseHttps(certificate);
    });
});

// Add services to the container.
builder.Services.AddRazorPages();

// REGISTRA AppDbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Aggiunge la gestione della sessione
builder.Services.AddSession();

builder.Services.AddScoped<UfficioSinistri.Services.TenantProvider>();

builder.Services.AddHttpContextAccessor();

// Aggiunge autenticazione cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.Use(async (context, next) =>
{
    var tenant = context.RequestServices.GetRequiredService<UfficioSinistri.Services.TenantProvider>();

    var aziendaStr = context.Session.GetString("Azienda");
    if (!string.IsNullOrWhiteSpace(aziendaStr) && Guid.TryParse(aziendaStr, out var aziendaId))
    {
        tenant.AziendaId = aziendaId;
    }

    await next();
});



app.UseAuthorization();

app.MapRazorPages();

app.Run();
