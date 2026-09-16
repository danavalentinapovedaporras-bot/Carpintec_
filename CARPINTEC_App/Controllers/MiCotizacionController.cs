using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    public class MiCotizacionController : Controller
    {
        private readonly CarpintecContext _context;

        public MiCotizacionController(CarpintecContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /MiCotizacion/Index
        // ==========================================
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Obtener datos del usuario en sesión o a través de claims JWT
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");

                if (!idUsuario.HasValue && User.Identity?.IsAuthenticated == true)
                {
                    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value;
                    if (int.TryParse(claimId, out int parsedId))
                    {
                        idUsuario = parsedId;
                    }
                }

                Usuario? usuario = null;
                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                if (usuario == null && User.Identity?.IsAuthenticated == true)
                {
                    var claimEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(claimEmail))
                    {
                        usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo != null && u.Correo.ToLower() == claimEmail.ToLower());
                    }
                }

                string correoUsuario = (usuario?.Correo ?? correoSession ?? "").Trim();
                string nombreUsuario = (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : (nombreSession ?? "")).Trim();

                // 2. Buscar o sincronizar el registro en la tabla Clientes
                Cliente? cliente = null;

                if (!string.IsNullOrEmpty(correoUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoUsuario.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreUsuario.ToLower()) ||
                        (c.Contacto != null && c.Contacto.ToLower() == nombreUsuario.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreUsuario.ToLower())
                    );
                }

                // Si el usuario existe en sesión pero no tiene registro en Clientes, lo creamos para asignarle su propio IdCliente
                if (cliente == null && usuario != null)
                {
                    cliente = new Cliente
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
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync();
                }

                // 3. Preparar ViewBag para el formulario y cabecera
                ViewBag.NombreCliente = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : nombreUsuario;
                ViewBag.CorreoCliente = !string.IsNullOrEmpty(cliente?.Correo) ? cliente.Correo : correoUsuario;
                ViewBag.TelefonoCliente = cliente?.Telefono ?? "";
                ViewBag.IdCliente = cliente?.IdCliente ?? 0;

                // 4. Consultar las cotizaciones del cliente desde la base de datos
                List<Cotizacion> cotizaciones = new List<Cotizacion>();

                if (cliente != null || !string.IsNullOrEmpty(correoUsuario) || !string.IsNullOrEmpty(nombreUsuario))
                {
                    int idCliente = cliente?.IdCliente ?? 0;
                    string correoFiltro = correoUsuario.ToLower();
                    string nombreFiltro = nombreUsuario.ToLower();

                    cotizaciones = await _context.Cotizaciones
                        .Include(c => c.IdClienteNavigation)
                        .Include(c => c.DetalleCotizacions)
                            .ThenInclude(d => d.IdProductoNavigation)
                        .Where(c =>
                            (idCliente > 0 && c.IdCliente == idCliente) ||
                            (!string.IsNullOrEmpty(correoFiltro) && c.CorreoCliente != null && c.CorreoCliente.ToLower() == correoFiltro) ||
                            (!string.IsNullOrEmpty(nombreFiltro) && c.NombreCliente != null && c.NombreCliente.ToLower() == nombreFiltro)
                        )
                        .OrderByDescending(c => c.FechaRegistro)
                        .ThenByDescending(c => c.IdCotizacion)
                        .ToListAsync();
                }

                // 5. Calcular métricas para las tarjetas de resumen
                ViewBag.TotalCotizaciones = cotizaciones.Count;
                ViewBag.Aprobadas = cotizaciones.Count(c => c.Estado != null && (c.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Finalizada", StringComparison.OrdinalIgnoreCase)));
                ViewBag.Pendientes = cotizaciones.Count(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase));

                return View(cotizaciones);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar cotizaciones: " + ex.Message;
                ViewBag.TotalCotizaciones = 0;
                ViewBag.Aprobadas = 0;
                ViewBag.Pendientes = 0;
                return View(new List<Cotizacion>());
            }
        }

        // ==========================================
        // POST: /MiCotizacion/GuardarCotizacion
        // ==========================================
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

                // 1. Identificar o vincular cliente actual
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue && User.Identity?.IsAuthenticated == true)
                {
                    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                    if (int.TryParse(claimId, out int parsedId)) idUsuario = parsedId;
                }

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

                // Si no existe el cliente en la BD, crearlo para persistir sus datos
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

                // 2. Asignar un empleado válido
                var empleadoDb = await _context.Empleados.FirstOrDefaultAsync();
                int idEmpleadoValido = empleadoDb != null ? empleadoDb.IdEmpleado : 1;

                // 3. Manejo opcional de foto subida
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
                        // Si falla la subida de foto, no interrumpimos la cotización
                    }
                }

                // 4. Crear la nueva cotización en la BD con los datos reales del cliente
                var nuevaCotizacion = new Cotizacion
                {
                    Folio = "COT-" + DateTime.Now.Year + "-" + new Random().Next(1000, 9999),
                    IdCliente = idClienteFinal,
                    IdEmpleado = idEmpleadoValido,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    Total = total > 0 ? total : 320000,
                    Estado = "Pendiente",
                    NombreCliente = nombreFinal,
                    CorreoCliente = correoFinal,
                    TelefonoCliente = telFinal,
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

                return Json(new { success = true, message = "¡Su cotización ha sido enviada con éxito! Nuestro equipo técnico se comunicará pronto." });
            }
            catch (Exception ex)
            {
                string mensajeError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error al guardar la cotización: " + mensajeError });
            }
        }

        // ==========================================
        // GET: /MiCotizacion/ObtenerDetalle/5
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ObtenerDetalle(int id)
        {
            try
            {
                var cotizacion = await _context.Cotizaciones
                    .Include(c => c.IdClienteNavigation)
                    .Include(c => c.DetalleCotizacions)
                        .ThenInclude(d => d.IdProductoNavigation)
                    .FirstOrDefaultAsync(c => c.IdCotizacion == id);

                if (cotizacion == null)
                {
                    return Json(new { success = false, message = "Cotización no encontrada." });
                }

                string nombreProd = !string.IsNullOrWhiteSpace(cotizacion.DetalleProducto)
                    ? cotizacion.DetalleProducto
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.IdProductoNavigation?.Nombre ?? "Mueble personalizado");

                string tipoMad = !string.IsNullOrWhiteSpace(cotizacion.TipoMadera)
                    ? cotizacion.TipoMadera
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.IdProductoNavigation?.Material ?? "Pino Macizo");

                int cant = cotizacion.Cantidad ?? cotizacion.DetalleCotizacions.FirstOrDefault()?.Cantidad ?? 1;
                string med = !string.IsNullOrWhiteSpace(cotizacion.Medidas)
                    ? cotizacion.Medidas
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.Medidas ?? "N/A");

                string nomCliente = !string.IsNullOrWhiteSpace(cotizacion.NombreCliente)
                    ? cotizacion.NombreCliente
                    : ($"{cotizacion.IdClienteNavigation?.Nombre} {cotizacion.IdClienteNavigation?.Apellido}".Trim());

                string corCliente = !string.IsNullOrWhiteSpace(cotizacion.CorreoCliente)
                    ? cotizacion.CorreoCliente
                    : (cotizacion.IdClienteNavigation?.Correo ?? "");

                string telCliente = !string.IsNullOrWhiteSpace(cotizacion.TelefonoCliente)
                    ? cotizacion.TelefonoCliente
                    : (cotizacion.IdClienteNavigation?.Telefono ?? "");

                return Json(new
                {
                    success = true,
                    folio = cotizacion.Folio,
                    fecha = cotizacion.Fecha.ToString("yyyy-MM-dd"),
                    cliente = string.IsNullOrWhiteSpace(nomCliente) ? "Cliente General" : nomCliente,
                    correo = corCliente,
                    telefono = telCliente,
                    producto = nombreProd,
                    madera = tipoMad,
                    cantidad = cant,
                    medidas = med,
                    total = cotizacion.Total.ToString("C0", new System.Globalization.CultureInfo("es-CO")),
                    estado = cotizacion.Estado ?? "Pendiente",
                    observaciones = string.IsNullOrWhiteSpace(cotizacion.ObservacionesAdicionales) ? "Sin observaciones adicionales." : cotizacion.ObservacionesAdicionales,
                    tiempoEntrega = cotizacion.TiempoEntrega ?? "15 días hábiles"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}