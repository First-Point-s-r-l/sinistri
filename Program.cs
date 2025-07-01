using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UfficioSinistri.Data;
using UfficioSinistri.Hubs;
using UfficioSinistri.Services;

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
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7285, listen =>
    {
        listen.UseHttps(certificate);
    });
});

//--------------------------------------------------------------------------------------------------
// Fine caricamento certificato per Kestrel
//--------------------------------------------------------------------------------------------------


// 1) Razor Pages + Session + HttpContextAccessor + multitenant
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantProvider>();

// 2) EF Core
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3) Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.LogoutPath = "/Account/Logout";
    });

// 4) SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// 5) Hosted service per notifiche SQL→SignalR
builder.Services.AddHostedService<SinistriNotifier>();


var app = builder.Build();

//--------------------------------------------------------------------------------------------------
// Pipeline middleware
//--------------------------------------------------------------------------------------------------

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Filtra il TenantProvider dalla sessione
app.Use(async (ctx, next) =>
{
    var tenant = ctx.RequestServices.GetRequiredService<TenantProvider>();
    var aziendaStr = ctx.Session.GetString("Azienda");
    if (Guid.TryParse(aziendaStr, out var aid))
        tenant.AziendaId = aid;
    await next();
});

app.UseAuthentication();
app.UseAuthorization();


// 6) Map Razor Pages e SignalR Hub
app.MapRazorPages();
app.MapHub<SinistriHub>("/hubs/sinistri");

app.Run();
