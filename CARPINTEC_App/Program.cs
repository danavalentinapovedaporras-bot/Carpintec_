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

// Servicio del Chatbot Administrativo (ERP y Base de Datos)
builder.Services.AddScoped<ChatbotService>();

// Servicio del Chatbot Público (Visitantes de la Página Web)
builder.Services.AddScoped<ChatbotPublicoService>();
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
// Inicialización segura de esquema para vacaciones
// =========================
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CarpintecContext>();
    db.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaInicioVacaciones')
        BEGIN
            ALTER TABLE Empleado ADD FechaInicioVacaciones DATE NULL;
        END
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaFinVacaciones')
        BEGIN
            ALTER TABLE Empleado ADD FechaFinVacaciones DATE NULL;
        END
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaInicioEstado')
        BEGIN
            ALTER TABLE Empleado ADD FechaInicioEstado DATE NULL;
        END
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaFinEstado')
        BEGIN
            ALTER TABLE Empleado ADD FechaFinEstado DATE NULL;
        END
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleado') AND name = 'IdUsuario')
        BEGIN
            ALTER TABLE Empleado ADD IdUsuario INT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Empleado_Usuario')
            BEGIN
                ALTER TABLE Empleado ADD CONSTRAINT FK_Empleado_Usuario FOREIGN KEY (IdUsuario) REFERENCES Usuario(IdUsuario);
            END
        END
    ");
}
catch
{
    // Se ignora si la base de datos aún no está inicializada o se ejecuta offline
}

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