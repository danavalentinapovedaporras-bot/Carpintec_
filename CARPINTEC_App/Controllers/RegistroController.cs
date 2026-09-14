using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using System.Linq;

namespace CARPINTEC_App.Controllers
{
    public class RegistroController : Controller
    {
        private readonly CarpintecContext _context;

        public RegistroController(CarpintecContext context)
        {
            _context = context;
        }

        // Mostrar la vista de registro
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Registrar un nuevo cliente
        [HttpPost]
        public IActionResult Registrar(Usuario usuario, string confirmarPassword)
        {
            try
            {
                // Eliminar espacios
                usuario.Nombre = usuario.Nombre?.Trim() ?? "";
                usuario.Apellido = usuario.Apellido?.Trim() ?? "";
                usuario.Correo = usuario.Correo?.Trim();


                // Verificar correo repetido
                if (!string.IsNullOrEmpty(usuario.Correo))
                {
                    bool existeCorreo = _context.Usuarios
                        .Any(u => u.Correo != null &&
                                  u.Correo.ToLower() == usuario.Correo.ToLower());

                    if (existeCorreo)
                    {
                        TempData["Error"] = "El correo ya está registrado.";
                        return View("Index", usuario);
                    }
                }

                // Verificar contraseñas
                if (usuario.Contraseña != confirmarPassword)
                {
                    TempData["Error"] = "Las contraseñas no coinciden.";
                    return View("Index", usuario);
                }

               
                // Validar contraseña segura
                string patron = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&.#_-])[A-Za-z\d@$!%*?&.#_-]{8,}$";

                if (!Regex.IsMatch(usuario.Contraseña, patron))
                {
                    TempData["Error"] = "La contraseña debe tener mínimo 8 caracteres, una mayúscula, una minúscula, un número y un carácter especial.";
                    return View("Index", usuario);
                }

                // Encriptar contraseña
                var hasher = new PasswordHasher<Usuario>();
                usuario.Contraseña = hasher.HashPassword(usuario, usuario.Contraseña);

                // Datos por defecto
                usuario.Rol = "Cliente";
                usuario.Estado = "Activo";
                // Guardar
                _context.Usuarios.Add(usuario);
                _context.SaveChanges();

                TempData["Success"] = "Cuenta creada correctamente. Ya puedes iniciar sesión.";

                return RedirectToAction("Index", "Login");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View("Index", usuario);
            }
        }
    }
}