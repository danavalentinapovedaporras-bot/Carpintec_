using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

                // 1. Guardar en tabla Usuario
                _context.Usuarios.Add(usuario);
                _context.SaveChanges();

                // 2. Sincronizar automáticamente en la tabla Cliente para que exista su IdCliente correspondiente
                var clienteExistente = _context.Clientes.FirstOrDefault(c =>
                    (!string.IsNullOrEmpty(usuario.Correo) && c.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                    (c.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower() && (c.Apellido ?? "").ToLower() == usuario.Apellido.ToLower())
                );

                if (clienteExistente == null)
                {
                    clienteExistente = new Cliente
                    {
                        Nombre = usuario.Nombre,
                        Apellido = usuario.Apellido,
                        Correo = usuario.Correo ?? "",
                        Telefono = "",
                        TipoCliente = "Natural",
                        Contacto = $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                        Estado = "Activo",
                        FechaRegistro = DateTime.Now
                    };
                    _context.Clientes.Add(clienteExistente);
                    _context.SaveChanges();
                }

                // 3. Vincular y actualizar la columna IdCliente en Pedido y Cotizacion si existían registros previos con su correo o nombre
                string correoUser = usuario.Correo?.ToLower() ?? "";
                string nombreUser = $"{usuario.Nombre} {usuario.Apellido}".Trim().ToLower();

                var cotizacionesPrevias = _context.Cotizaciones
                    .Where(c => (c.CorreoCliente != null && c.CorreoCliente.ToLower() == correoUser) ||
                                (c.NombreCliente != null && c.NombreCliente.ToLower() == nombreUser))
                    .ToList();

                foreach (var cot in cotizacionesPrevias)
                {
                    cot.IdCliente = clienteExistente.IdCliente;
                }

                var pedidosPrevios = _context.Pedidos
                    .Include(p => p.IdCotizacionNavigation)
                    .Where(p => p.IdCotizacionNavigation != null &&
                                ((p.IdCotizacionNavigation.CorreoCliente != null && p.IdCotizacionNavigation.CorreoCliente.ToLower() == correoUser) ||
                                 (p.IdCotizacionNavigation.NombreCliente != null && p.IdCotizacionNavigation.NombreCliente.ToLower() == nombreUser)))
                    .ToList();

                foreach (var ped in pedidosPrevios)
                {
                    ped.IdCliente = clienteExistente.IdCliente;
                }

                if (cotizacionesPrevias.Any() || pedidosPrevios.Any())
                {
                    _context.SaveChanges();
                }

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