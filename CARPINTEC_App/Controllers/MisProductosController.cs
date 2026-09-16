using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    public class MisProductosController : Controller
    {
        private readonly CarpintecContext _context;

        public MisProductosController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: MisProductosController
        public async Task<IActionResult> Index()
        {
            try
            {
                // Cargar datos del usuario/cliente en sesión para autocompletar el modal de cotización
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoUsuario = null;
                string? nombreUsuario = HttpContext.Session.GetString("NombreUsuario");
                Cliente? cliente = null;

                if (idUsuario.HasValue)
                {
                    var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                    if (usuario != null)
                    {
                        correoUsuario = usuario.Correo;
                        cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                            (c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                            (c.Nombre != null && usuario.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower())
                        );
                    }
                }

                if (cliente == null)
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync();
                }

                ViewBag.NombreCliente = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : (nombreUsuario ?? "");
                ViewBag.CorreoCliente = cliente?.Correo ?? correoUsuario ?? "";
                ViewBag.TelefonoCliente = cliente?.Telefono ?? "";
                ViewBag.IdCliente = cliente?.IdCliente ?? 1;

                return View();
            }
            catch
            {
                return View();
            }
        }

        // POST: MisProductosController/GuardarCotizacion
        [HttpPost]
        public async Task<IActionResult> GuardarCotizacion(
             string? nombreCliente,
             string? correo,
             string? telefono,
             string producto,
             string madera,
             int cantidad,
             decimal alto,
             decimal ancho,
             decimal profundidad,
             string? tiempoEntrega,
             string? observaciones,
             decimal total,
             IFormFile? fotoLugar)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(producto))
                {
                    return Json(new { success = false, message = "Por favor especifique el producto o mueble a cotizar." });
                }

                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                Usuario? usuario = null;

                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                string correoFinal = (!string.IsNullOrWhiteSpace(correo) ? correo : (usuario?.Correo ?? "")).Trim();
                string nombreFinal = (!string.IsNullOrWhiteSpace(nombreCliente) ? nombreCliente : (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : "Cliente")).Trim();
                string telFinal = (!string.IsNullOrWhiteSpace(telefono) ? telefono : "").Trim();

                Cliente? cliente = null;
                if (!string.IsNullOrEmpty(correoFinal))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoFinal.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreFinal))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreFinal.ToLower()) ||
                        (c.Contacto != null && c.Contacto.ToLower() == nombreFinal.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreFinal.ToLower())
                    );
                }

                if (cliente == null)
                {
                    cliente = new Cliente
                    {
                        Nombre = usuario?.Nombre ?? nombreFinal,
                        Apellido = usuario?.Apellido ?? "",
                        Correo = correoFinal,
                        Telefono = telFinal,
                        TipoCliente = "Natural",
                        Contacto = nombreFinal,
                        Estado = "Activo",
                        FechaRegistro = DateTime.Now
                    };
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync();
                }

                int idClienteFinal = cliente.IdCliente;

                var empleadoDb = await _context.Empleados.FirstOrDefaultAsync();
                int idEmpleadoValido = empleadoDb != null ? empleadoDb.IdEmpleado : 1;

                if (fotoLugar != null && fotoLugar.Length > 0)
                {
                    try
                    {
                        string carpetaUploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(carpetaUploads))
                        {
                            Directory.CreateDirectory(carpetaUploads);
                        }

                        string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(fotoLugar.FileName);
                        string rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

                        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await fotoLugar.CopyToAsync(stream);
                        }
                    }
                    catch
                    {
                        // Si falla la subida, continuamos guardando la cotización
                    }
                }

                var nuevaCotizacion = new Cotizacion
                {
                    Folio = "COT-" + DateTime.Now.Year + "-" + new Random().Next(1000, 9999),
                    IdCliente = idClienteFinal,
                    IdEmpleado = idEmpleadoValido,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    Total = total > 0 ? total : 320000,
                    Estado = "Pendiente",
                    NombreCliente = !string.IsNullOrWhiteSpace(nombreCliente) ? nombreCliente : (cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : "Cliente General"),
                    CorreoCliente = !string.IsNullOrWhiteSpace(correo) ? correo : (cliente?.Correo ?? ""),
                    TelefonoCliente = !string.IsNullOrWhiteSpace(telefono) ? telefono : (cliente?.Telefono ?? ""),
                    DetalleProducto = producto,
                    TipoMadera = !string.IsNullOrWhiteSpace(madera) ? madera : "Pino Macizo",
                    Cantidad = cantidad > 0 ? cantidad : 1,
                    Medidas = $"Alto: {alto}cm, Ancho: {ancho}cm, Prof: {profundidad}cm",
                    TiempoEntrega = tiempoEntrega ?? "15 días hábiles",
                    ObservacionesAdicionales = observaciones ?? "",
                    FechaRegistro = DateTime.Now
                };

                _context.Cotizaciones.Add(nuevaCotizacion);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "¡Su cotización ha sido generada exitosamente! Puede consultarla en 'Mis Cotizaciones'." });
            }
            catch (Exception ex)
            {
                string mensajeError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error al guardar la cotización: " + mensajeError });
            }
        }
    }
}
