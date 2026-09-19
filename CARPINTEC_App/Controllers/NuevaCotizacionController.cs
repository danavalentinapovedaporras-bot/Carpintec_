using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    public class NuevaCotizacionController : Controller
    {
        private readonly CarpintecContext _context;

        public NuevaCotizacionController(CarpintecContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /NuevaCotizacion/Index
        // ==========================================
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Obtener usuario en sesión
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");
                string? primerNombreSession = HttpContext.Session.GetString("PrimerNombre");

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

                // 2. Buscar o crear Cliente
                Cliente? cliente = null;
                if (!string.IsNullOrEmpty(correoUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoUsuario.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreUsuario.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreUsuario.ToLower())
                    );
                }

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

                string nombreCompleto = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : (!string.IsNullOrWhiteSpace(nombreUsuario) ? nombreUsuario : "Cliente Carpintec");
                string primerNombre = !string.IsNullOrWhiteSpace(usuario?.Nombre) ? usuario.Nombre : (primerNombreSession ?? "Cliente");
                string iniciales = !string.IsNullOrWhiteSpace(primerNombre) ? primerNombre.Substring(0, 1).ToUpper() : "CL";

                ViewBag.NombreCliente = nombreCompleto;
                ViewBag.CorreoCliente = !string.IsNullOrWhiteSpace(cliente?.Correo) ? cliente.Correo : correoUsuario;
                ViewBag.TelefonoCliente = !string.IsNullOrWhiteSpace(cliente?.Telefono) ? cliente.Telefono : "300 123 4567";
                ViewBag.Iniciales = iniciales;
                ViewBag.IdCliente = cliente?.IdCliente ?? 0;

                return View();
            }
            catch
            {
                ViewBag.NombreCliente = "Cliente Carpintec";
                ViewBag.CorreoCliente = "";
                ViewBag.TelefonoCliente = "";
                ViewBag.Iniciales = "CL";
                return View();
            }
        }

        // ==========================================
        // POST: /NuevaCotizacion/GuardarCotizacion
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
                        if (!Directory.Exists(carpetaUploads)) Directory.CreateDirectory(carpetaUploads);

                        string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(fotoLugar.FileName);
                        string rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

                        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await fotoLugar.CopyToAsync(stream);
                        }
                    }
                    catch { }
                }

                // 1. Guardar la Cotización
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

                // 2. Crear de inmediato el Pedido correspondiente para que se refleje instantáneamente en la vista Mi_Pedido
                string codigoPedidoGen = !string.IsNullOrEmpty(nuevaCotizacion.Folio)
                    ? nuevaCotizacion.Folio.Replace("COT", "PED")
                    : $"PED-{DateTime.Now.Year}-{new Random().Next(1000, 9999)}";

                var nuevoPedido = new Pedido
                {
                    CodigoPedido = codigoPedidoGen,
                    Producto = nuevaCotizacion.DetalleProducto,
                    IdCotizacion = nuevaCotizacion.IdCotizacion,
                    IdCliente = idClienteFinal,
                    FechaSolicitud = DateOnly.FromDateTime(DateTime.Now),
                    FechaEntrega = DateOnly.FromDateTime(DateTime.Now.AddDays(15)),
                    Estado = "Pendiente",
                    ValorTotal = nuevaCotizacion.Total > 0 ? nuevaCotizacion.Total : 320000,
                    Observaciones = $"Pedido generado automáticamente desde cotización {nuevaCotizacion.Folio}. Madera: {nuevaCotizacion.TipoMadera}, Medidas: {nuevaCotizacion.Medidas}.",
                    FechaRegistro = DateTime.Now
                };

                _context.Pedidos.Add(nuevoPedido);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    idCotizacion = nuevaCotizacion.IdCotizacion,
                    idPedido = nuevoPedido.IdPedido,
                    codigoPedido = nuevoPedido.CodigoPedido,
                    folioCotizacion = nuevaCotizacion.Folio,
                    message = "¡Cotización y Pedido registrados con éxito! Redirigiendo a Mis Pedidos...",
                    redirectUrl = "/Mi_Pedido"
                });
            }
            catch (Exception ex)
            {
                string mensajeError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error al guardar la cotización: " + mensajeError });
            }
        }
    }
}
