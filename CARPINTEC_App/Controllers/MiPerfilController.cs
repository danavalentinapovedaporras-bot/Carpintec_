using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    public class MiPerfilController : Controller
    {
        private readonly CarpintecContext _context;

        public MiPerfilController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: MiPerfil
        public async Task<IActionResult> Index()
        {
            try
            {
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");
                string? primerNombreSession = HttpContext.Session.GetString("PrimerNombre");
                string? rolSession = HttpContext.Session.GetString("Rol");

                if (idUsuario == null)
                {
                    return RedirectToAction("Index", "Login");
                }

                // Cargar usuario desde BD
                Usuario? usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

                // Cargar cliente relacionado
                Cliente? cliente = null;
                if (usuario != null)
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                        (c.Nombre != null && usuario.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower())
                    );
                }

                string nombre = cliente?.Nombre ?? usuario?.Nombre ?? primerNombreSession ?? "Cliente";
                string apellido = cliente?.Apellido ?? usuario?.Apellido ?? "";
                string primerNombre = !string.IsNullOrWhiteSpace(nombre)
                    ? nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]
                    : "Cliente";
                string nombreCompleto = $"{nombre} {apellido}".Trim();

                // Iniciales avatar
                string iniciales = "CL";
                if (!string.IsNullOrWhiteSpace(primerNombre))
                {
                    iniciales = primerNombre.Substring(0, 1).ToUpper();
                    if (!string.IsNullOrWhiteSpace(apellido))
                        iniciales += apellido.Trim().Substring(0, 1).ToUpper();
                    else if (primerNombre.Length > 1)
                        iniciales += primerNombre.Substring(1, 1).ToUpper();
                }

                // Datos generales de perfil
                ViewBag.NombreCompleto = nombreCompleto;
                ViewBag.PrimerNombre = primerNombre;
                ViewBag.Apellido = apellido;
                ViewBag.Iniciales = iniciales;
                ViewBag.Correo = usuario?.Correo ?? correoSession ?? "";
                ViewBag.Rol = rolSession ?? usuario?.Rol ?? "Cliente";
                ViewBag.Estado = usuario?.Estado ?? "Activo";

                // Datos del cliente
                ViewBag.TipoCliente = cliente?.TipoCliente ?? "Natural";
                ViewBag.Documento = cliente?.Documento ?? "No registrado";
                ViewBag.Telefono = cliente?.Telefono ?? "No registrado";
                ViewBag.Contacto = cliente?.Contacto ?? nombreCompleto;
                ViewBag.Direccion = cliente?.Direccion ?? "No registrada";
                ViewBag.Ciudad = cliente?.Ciudad ?? "No registrada";
                ViewBag.FechaRegistro = cliente?.FechaRegistro?.ToString("dd/MM/yyyy") ?? "No disponible";
                ViewBag.NombreEmpresa = cliente?.NombreEmpresa ?? "";
                ViewBag.IdCliente = cliente?.IdCliente ?? 0;
                ViewBag.IdUsuario = idUsuario;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el perfil: " + ex.Message;
                return View();
            }
        }

        // POST: MiPerfil/ActualizarPerfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPerfil(
            string nombre, string apellido, string telefono,
            string direccion, string ciudad, string documento)
        {
            try
            {
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (idUsuario == null)
                    return Json(new { success = false, message = "Sesión no válida." });

                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado." });

                usuario.Nombre = nombre?.Trim() ?? usuario.Nombre;
                usuario.Apellido = apellido?.Trim() ?? usuario.Apellido;

                var cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                    c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower());

                if (cliente != null)
                {
                    cliente.Nombre = nombre?.Trim() ?? cliente.Nombre;
                    cliente.Apellido = apellido?.Trim() ?? cliente.Apellido;
                    if (!string.IsNullOrWhiteSpace(telefono)) cliente.Telefono = telefono.Trim();
                    if (!string.IsNullOrWhiteSpace(direccion)) cliente.Direccion = direccion.Trim();
                    if (!string.IsNullOrWhiteSpace(ciudad)) cliente.Ciudad = ciudad.Trim();
                    if (!string.IsNullOrWhiteSpace(documento)) cliente.Documento = documento.Trim();
                }

                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("NombreUsuario", $"{usuario.Nombre} {usuario.Apellido}");
                HttpContext.Session.SetString("PrimerNombre", usuario.Nombre ?? "");

                return Json(new { success = true, message = "Perfil actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar: " + ex.Message });
            }
        }

        // POST: MiPerfil/CambiarContrasena
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(
            string contrasenaActual, string nuevaContrasena, string confirmarContrasena)
        {
            try
            {
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (idUsuario == null)
                    return Json(new { success = false, message = "Sesión no válida." });

                if (nuevaContrasena != confirmarContrasena)
                    return Json(new { success = false, message = "Las contraseñas nuevas no coinciden." });

                if (nuevaContrasena.Length < 8)
                    return Json(new { success = false, message = "La contraseña debe tener mínimo 8 caracteres." });

                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado." });

                var hasher = new PasswordHasher<Usuario>();
                bool esHasheada = !string.IsNullOrEmpty(usuario.Contraseña) && usuario.Contraseña.StartsWith("AQAAAA");

                bool contrasenaCorrecta;
                if (esHasheada)
                {
                    var resultado = hasher.VerifyHashedPassword(usuario, usuario.Contraseña, contrasenaActual);
                    contrasenaCorrecta = resultado == PasswordVerificationResult.Success;
                }
                else
                {
                    contrasenaCorrecta = usuario.Contraseña == contrasenaActual;
                }

                if (!contrasenaCorrecta)
                    return Json(new { success = false, message = "La contraseña actual es incorrecta." });

                usuario.Contraseña = hasher.HashPassword(usuario, nuevaContrasena);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Contraseña cambiada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al cambiar contraseña: " + ex.Message });
            }
        }

        // POST: MiPerfil/ActualizarDireccion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDireccion(string direccion, string ciudad)
        {
            try
            {
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (idUsuario == null)
                    return Json(new { success = false, message = "Sesión no válida." });

                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado." });

                var cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                    c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower());

                if (cliente != null)
                {
                    if (!string.IsNullOrWhiteSpace(direccion)) cliente.Direccion = direccion.Trim();
                    if (!string.IsNullOrWhiteSpace(ciudad)) cliente.Ciudad = ciudad.Trim();
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Dirección actualizada correctamente." });
                }

                return Json(new { success = false, message = "No se encontró información de cliente asociada." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar dirección: " + ex.Message });
            }
        }
    }
}
