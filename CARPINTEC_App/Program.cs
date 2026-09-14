using CARPINTEC_App.Data;
using CARPINTEC_App.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Servicios MVC
// =========================
builder.Services.AddControllersWithViews();

// =========================
// Base de datos
// =========================
string connectionString =
    builder.Configuration.GetConnectionString("ConexionSQL")!;

builder.Services.AddDbContext<CarpintecContext>(options =>
    options.UseSqlServer(connectionString));

// =========================
// Registrar servicios
// =========================
builder.Services.AddScoped<TokenService>();

// Servicio del Chatbot
builder.Services.AddScoped<ChatbotService>();
// =========================
// Configuración JWT
// =========================
var jwtConfig = builder.Configuration.GetSection("Jwt");

var clave = Encoding.UTF8.GetBytes(
    jwtConfig["ClaveSecreta"]!
);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtConfig["Emisor"],
        ValidAudience = jwtConfig["Audiencia"],

        IssuerSigningKey =
            new SymmetricSecurityKey(clave),

        ClockSkew = TimeSpan.Zero
    };

    // =========================
    // Obtener JWT desde Cookie
    // =========================
    options.Events = new JwtBearerEvents
    {
        // Obtener el JWT desde la cookie
        OnMessageReceived = context =>
        {
            context.Token =
                context.Request.Cookies["tokenJwt"];

            return Task.CompletedTask;
        },

        // Si no hay JWT válido, regresar al Login
        OnChallenge = context =>
        {
            context.HandleResponse();

            context.Response.Redirect("/Login");

            return Task.CompletedTask;
        }
    };
});

// =========================
// Autorización
// =========================
builder.Services.AddAuthorization();

// =========================
// Sesión
// =========================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =========================
// Construir aplicación
// =========================
var app = builder.Build();

// =========================
// Pipeline HTTP
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// =========================
// Sesión
// =========================
app.UseSession();

// =========================
// Autenticación JWT
// =========================
app.UseAuthentication();

// =========================
// Autorización
// =========================
app.UseAuthorization();

// =========================
// Rutas
// =========================
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();