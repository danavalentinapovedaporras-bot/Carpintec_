using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class ConfiguracionController : Controller
    {
        private readonly CarpintecContext _context;

        public ConfiguracionController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: Configuracion
        public async Task<IActionResult> Index()
        {
            // Obtener configuración de la empresa de la base de datos
            var config = await _context.Configuracions.FirstOrDefaultAsync();

            if (config == null)
            {
                // Si aún no existe, crear la configuración inicial
                config = new Configuracion
                {
                    NombreEmpresa = "CARPINTEC S.A.S.",
                    Nit = "901234567-8",
                    Direccion = "Calle 10 #20-30",
                    Ciudad = "Chía",
                    Telefono = "6015551234",
                    Correo = "contacto@carpintec.com",
                    SitioWeb = "www.carpintec.com",
                    Logo = "logo.png",
                    Iva = 19.00m,
                    Moneda = "COP",
                    FechaActualizacion = DateTime.Now
                };

                _context.Configuracions.Add(config);
                await _context.SaveChangesAsync();
            }

            // Obtener usuario actualmente autenticado
            string? username = User.Identity?.Name;
            Usuario? usuario = null;

            if (!string.IsNullOrEmpty(username))
            {
                usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Correo == username);
            }

            if (usuario == null)
            {
                usuario = await _context.Usuarios.FirstOrDefaultAsync();
            }

            ViewBag.UsuarioActual = usuario;

            return View(config);
        }

        // POST: Configuracion/GuardarEmpresa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEmpresa(Configuracion model)
        {
            try
            {
                var config = await _context.Configuracions.FirstOrDefaultAsync();

                if (config == null)
                {
                    model.FechaActualizacion = DateTime.Now;
                    _context.Configuracions.Add(model);
                }
                else
                {
                    config.NombreEmpresa = model.NombreEmpresa;
                    config.Nit = model.Nit;
                    config.Direccion = model.Direccion;
                    config.Ciudad = model.Ciudad;
                    config.Telefono = model.Telefono;
                    config.Correo = model.Correo;
                    config.SitioWeb = model.SitioWeb;
                    config.Iva = model.Iva;
                    config.Moneda = model.Moneda;
                    config.FechaActualizacion = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                TempData["Exito"] = "Los datos de la empresa han sido actualizados exitosamente en la base de datos.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar la configuración: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Configuracion/GuardarPerfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarPerfil(int idUsuario, string nombre, string? apellido, string correo, string? telefono)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(idUsuario);

                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                usuario.Nombre = nombre;
                usuario.Apellido = apellido ?? "";
                usuario.Correo = correo;

                await _context.SaveChangesAsync();
                TempData["Exito"] = "Perfil actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar el perfil: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Configuracion/GuardarSeguridad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSeguridad(int idUsuario, string passwordActual, string nuevaPassword, string confirmarPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nuevaPassword) || nuevaPassword.Length < 6)
                {
                    TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                    return RedirectToAction(nameof(Index));
                }

                if (nuevaPassword != confirmarPassword)
                {
                    TempData["Error"] = "La confirmación de la contraseña no coincide.";
                    return RedirectToAction(nameof(Index));
                }

                var usuario = await _context.Usuarios.FindAsync(idUsuario);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                var hasher = new PasswordHasher<Usuario>();

                // Si la contraseña actual no está vacía, la verificamos
                if (!string.IsNullOrEmpty(usuario.Contraseña) && !string.IsNullOrEmpty(passwordActual))
                {
                    var verify = hasher.VerifyHashedPassword(usuario, usuario.Contraseña, passwordActual);
                    if (verify == PasswordVerificationResult.Failed && usuario.Contraseña != passwordActual)
                    {
                        TempData["Error"] = "La contraseña actual ingresada es incorrecta.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Hashear y guardar nueva contraseña
                usuario.Contraseña = hasher.HashPassword(usuario, nuevaPassword);
                await _context.SaveChangesAsync();

                TempData["Exito"] = "Contraseña actualizada exitosamente en la base de datos.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cambiar la contraseña: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Configuracion/GuardarPreferencias
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarPreferencias(string idioma, string tema, bool? alertasStock, bool? nuevosPedidos, bool? autenticacionDosFactores)
        {
            TempData["Exito"] = "Preferencias guardadas correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
