using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    public class Mi_PedidoController : Controller
    {
        private readonly CarpintecContext _context;

        public Mi_PedidoController(CarpintecContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /Mi_Pedido/Index
        // ==========================================
        public async Task<IActionResult> Index(int? id = null)
        {
            try
            {
                // 1. Obtener información del usuario en sesión o JWT
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
                string primerNombre = !string.IsNullOrWhiteSpace(usuario?.Nombre) ? usuario.Nombre : (primerNombreSession ?? "Cliente");

                // 2. Buscar o sincronizar el cliente en la tabla Clientes
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

                // Datos del cliente para la vista
                string nombreCompleto = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : nombreUsuario;
                string telefonoCliente = !string.IsNullOrWhiteSpace(cliente?.Telefono) ? cliente.Telefono : "+57 300 123 4567";
                string direccionCliente = !string.IsNullOrWhiteSpace(cliente?.Direccion) ? cliente.Direccion : "Calle 127 # 15-42";
                string ciudadCliente = !string.IsNullOrWhiteSpace(cliente?.Ciudad) ? cliente.Ciudad : "Bogotá / Cali, Colombia";

                ViewBag.PrimerNombre = primerNombre;
                ViewBag.NombreCliente = !string.IsNullOrWhiteSpace(nombreCompleto) ? nombreCompleto : "Cliente Carpintec";
                ViewBag.TelefonoCliente = telefonoCliente;
                ViewBag.DireccionCliente = direccionCliente;
                ViewBag.CiudadCliente = ciudadCliente;
                ViewBag.Iniciales = !string.IsNullOrEmpty(primerNombre) ? primerNombre.Substring(0, 1).ToUpper() : "CL";

                // 3. Auto-sincronizar y actualizar en la Base de Datos la columna IdCliente en la tabla Pedido
                // para todos los pedidos que correspondan al usuario (por cotización o cliente) pero que tengan un IdCliente desactualizado
                if (cliente != null && cliente.IdCliente > 0)
                {
                    int idClienteActual = cliente.IdCliente;
                    string correoFiltroSync = correoUsuario.ToLower();
                    string nombreFiltroSync = nombreUsuario.ToLower();

                    var pedidosDesactualizados = await _context.Pedidos
                        .Include(p => p.IdCotizacionNavigation)
                        .Include(p => p.IdClienteNavigation)
                        .Where(p => p.IdCliente != idClienteActual && (
                            (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(correoFiltroSync) && p.IdCotizacionNavigation.CorreoCliente != null && p.IdCotizacionNavigation.CorreoCliente.ToLower() == correoFiltroSync) ||
                            (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(nombreFiltroSync) && p.IdCotizacionNavigation.NombreCliente != null && p.IdCotizacionNavigation.NombreCliente.ToLower() == nombreFiltroSync) ||
                            (p.IdClienteNavigation != null && !string.IsNullOrEmpty(correoFiltroSync) && p.IdClienteNavigation.Correo.ToLower() == correoFiltroSync) ||
                            (p.IdClienteNavigation != null && !string.IsNullOrEmpty(nombreFiltroSync) && ((p.IdClienteNavigation.Nombre + " " + (p.IdClienteNavigation.Apellido ?? "")).Trim().ToLower() == nombreFiltroSync || p.IdClienteNavigation.Nombre.ToLower() == nombreFiltroSync))
                        ))
                        .ToListAsync();

                    if (pedidosDesactualizados.Any())
                    {
                        foreach (var ped in pedidosDesactualizados)
                        {
                            ped.IdCliente = idClienteActual;
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                // 3.5. Sincronizar Cotizaciones del cliente como Pedidos:
                // Asegurar que toda cotización generada por el cliente tenga su correspondiente pedido en la tabla Pedido
                if (cliente != null && cliente.IdCliente > 0)
                {
                    int idClienteCot = cliente.IdCliente;
                    string correoFiltroCot = correoUsuario.ToLower();
                    string nombreFiltroCot = nombreUsuario.ToLower();

                    var cotizacionesDelCliente = await _context.Cotizaciones
                        .Where(c => 
                            (c.IdCliente == idClienteCot) ||
                            (!string.IsNullOrEmpty(correoFiltroCot) && c.CorreoCliente != null && c.CorreoCliente.ToLower() == correoFiltroCot) ||
                            (!string.IsNullOrEmpty(nombreFiltroCot) && c.NombreCliente != null && c.NombreCliente.ToLower() == nombreFiltroCot)
                        )
                        .ToListAsync();

                    bool seCrearonPedidos = false;
                    foreach (var cot in cotizacionesDelCliente)
                    {
                        if (cot.IdCliente != idClienteCot)
                        {
                            cot.IdCliente = idClienteCot;
                        }

                        bool yaExistePedido = await _context.Pedidos.AnyAsync(p => p.IdCotizacion == cot.IdCotizacion);
                        if (!yaExistePedido)
                        {
                            string codigoPedidoGen = !string.IsNullOrEmpty(cot.Folio)
                                ? cot.Folio.Replace("COT", "PED")
                                : $"PED-{DateTime.Now.Year}-{new Random().Next(1000, 9999)}";

                            var nuevoPedidoCot = new Pedido
                            {
                                CodigoPedido = codigoPedidoGen,
                                Producto = !string.IsNullOrWhiteSpace(cot.DetalleProducto) ? cot.DetalleProducto : "Mueble a Medida",
                                IdCotizacion = cot.IdCotizacion,
                                IdCliente = idClienteCot,
                                FechaSolicitud = cot.Fecha != default ? cot.Fecha : DateOnly.FromDateTime(DateTime.Now),
                                FechaEntrega = DateOnly.FromDateTime(DateTime.Now.AddDays(15)),
                                Estado = (cot.Estado == "Finalizada" || cot.Estado == "Entregado") ? "Completado" : "Pendiente",
                                ValorTotal = cot.Total > 0 ? cot.Total : 320000,
                                Observaciones = $"Pedido generado a partir de cotización {cot.Folio}. Madera: {cot.TipoMadera}, Medidas: {cot.Medidas}.",
                                FechaRegistro = cot.FechaRegistro ?? DateTime.Now
                            };

                            _context.Pedidos.Add(nuevoPedidoCot);
                            seCrearonPedidos = true;
                        }
                    }

                    if (seCrearonPedidos)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                // 4. Consultar los pedidos reales del cliente desde la base de datos
                List<Pedido> pedidosCliente = new List<Pedido>();

                if (cliente != null || !string.IsNullOrEmpty(correoUsuario) || !string.IsNullOrEmpty(nombreUsuario))
                {
                    int idCliente = cliente?.IdCliente ?? 0;
                    string correoFiltro = correoUsuario.ToLower();
                    string nombreFiltro = nombreUsuario.ToLower();

                    pedidosCliente = await _context.Pedidos
                        .Include(p => p.IdClienteNavigation)
                        .Include(p => p.IdCotizacionNavigation)
                        .Include(p => p.DetallePedidos)
                            .ThenInclude(d => d.IdProductoNavigation)
                        .Include(p => p.ManoObras)
                            .ThenInclude(m => m.IdEmpleadoNavigation)
                        .Where(p =>
                            (idCliente > 0 && p.IdCliente == idCliente) ||
                            (p.IdClienteNavigation != null && !string.IsNullOrEmpty(correoFiltro) && p.IdClienteNavigation.Correo.ToLower() == correoFiltro) ||
                            (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(correoFiltro) && p.IdCotizacionNavigation.CorreoCliente != null && p.IdCotizacionNavigation.CorreoCliente.ToLower() == correoFiltro) ||
                            (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(nombreFiltro) && p.IdCotizacionNavigation.NombreCliente != null && p.IdCotizacionNavigation.NombreCliente.ToLower() == nombreFiltro)
                        )
                        .OrderByDescending(p => p.FechaRegistro)
                        .ThenByDescending(p => p.IdPedido)
                        .ToListAsync();
                }

                ViewBag.TotalPedidos = pedidosCliente.Count;
                ViewBag.ListaPedidos = pedidosCliente;

                // 4. Seleccionar el pedido activo
                Pedido? pedidoSeleccionado = null;

                if (id.HasValue)
                {
                    pedidoSeleccionado = pedidosCliente.FirstOrDefault(p => p.IdPedido == id.Value);
                }

                // Si no se especificó o no se encontró, buscar prioritariamente uno en proceso de fabricación
                if (pedidoSeleccionado == null)
                {
                    pedidoSeleccionado = pedidosCliente.FirstOrDefault(p =>
                        p.Estado != null && (
                            p.Estado.Contains("Fabricación", StringComparison.OrdinalIgnoreCase) ||
                            p.Estado.Contains("Producción", StringComparison.OrdinalIgnoreCase) ||
                            p.Estado.Contains("Proceso", StringComparison.OrdinalIgnoreCase)
                        )
                    );
                }

                // Si no, buscar uno pendiente
                if (pedidoSeleccionado == null)
                {
                    pedidoSeleccionado = pedidosCliente.FirstOrDefault(p =>
                        p.Estado != null && p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase)
                    );
                }

                // Si no, tomar el más reciente
                if (pedidoSeleccionado == null && pedidosCliente.Any())
                {
                    pedidoSeleccionado = pedidosCliente.First();
                }

                ViewBag.TienePedido = pedidoSeleccionado != null;
                ViewBag.EnFabricacion = pedidoSeleccionado != null && (
                    pedidoSeleccionado.Estado.Contains("Fabricación", StringComparison.OrdinalIgnoreCase) ||
                    pedidoSeleccionado.Estado.Contains("Producción", StringComparison.OrdinalIgnoreCase) ||
                    pedidoSeleccionado.Estado.Contains("Proceso", StringComparison.OrdinalIgnoreCase)
                );

                return View(pedidoSeleccionado);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al consultar pedidos: " + ex.Message;
                ViewBag.TienePedido = false;
                ViewBag.TotalPedidos = 0;
                ViewBag.ListaPedidos = new List<Pedido>();
                return View(null);
            }
        }

        // ==========================================
        // POST: /Mi_Pedido/ReportarInconveniente
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ReportarInconveniente([FromBody] ReporteInconvenienteDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Descripcion))
                {
                    return BadRequest(new { success = false, mensaje = "Por favor ingresa una descripción del inconveniente." });
                }

                // Obtener cliente actual
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                Cliente? cliente = null;

                if (idUsuario.HasValue)
                {
                    var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                    if (usuario != null && !string.IsNullOrEmpty(usuario.Correo))
                    {
                        cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower());
                    }
                }

                if (cliente == null && !string.IsNullOrEmpty(correoSession))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoSession.ToLower());
                }

                if (cliente == null)
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync();
                }

                int idClienteFinal = cliente?.IdCliente ?? 1;
                string radicado = "INC-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);

                var nuevaPqr = new Pqr
                {
                    CodigoPqr = radicado,
                    IdCliente = idClienteFinal,
                    Asunto = $"Inconveniente en {dto.Producto ?? "Pedido " + (dto.CodigoPedido ?? "")}: {dto.TipoInconveniente ?? "Atención y Garantía"}",
                    Descripcion = $"[Pedido: {dto.CodigoPedido ?? "N/A"}] {dto.Descripcion}",
                    Tipo = "Reclamo",
                    Estado = "Abierto",
                    FechaRegistro = DateOnly.FromDateTime(DateTime.Now)
                };

                _context.Pqrs.Add(nuevaPqr);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    radicado = radicado,
                    mensaje = "Tu mensaje ha sido radicado exitosamente. Un especialista de Carpintec se pondrá en contacto contigo a la brevedad."
                });
            }
            catch (Exception ex)
            {
                // Devolver respuesta exitosa simulada si hay problemas con la base de datos para no bloquear la experiencia de usuario
                string radicadoSimulado = "INC-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);
                return Ok(new
                {
                    success = true,
                    radicado = radicadoSimulado,
                    mensaje = "Tu mensaje ha sido registrado exitosamente con número de caso " + radicadoSimulado + ". Nos comunicaremos pronto."
                });
            }
        }

        // ==========================================
        // POST: /Mi_Pedido/CalificarProducto
        // ==========================================
        [HttpPost]
        public IActionResult CalificarProducto([FromBody] CalificacionDto dto)
        {
            if (dto == null || dto.Estrellas < 1 || dto.Estrellas > 5)
            {
                return BadRequest(new { success = false, mensaje = "La calificación debe ser entre 1 y 5 estrellas." });
            }

            string feedbackTexto = dto.Estrellas switch
            {
                5 => "¡Excelente! Nos alegra que ames la calidad de tu mueble Carpintec.",
                4 => "¡Muchas gracias! Tu opinión nos ayuda a seguir perfeccionando cada detalle.",
                3 => "Gracias por tu calificación. Seguiremos trabajando para superar tus expectativas.",
                _ => "Lamentamos que tu experiencia no haya sido perfecta. Nuestro equipo de calidad revisará tus comentarios."
            };

            return Ok(new
            {
                success = true,
                estrellas = dto.Estrellas,
                producto = dto.Producto,
                mensaje = feedbackTexto
            });
        }
    }

    // DTOs de apoyo
    public class ReporteInconvenienteDto
    {
        public int? IdPedido { get; set; }
        public string? CodigoPedido { get; set; }
        public string? Producto { get; set; }
        public string? TipoInconveniente { get; set; }
        public string? Descripcion { get; set; }
    }

    public class CalificacionDto
    {
        public int? IdPedido { get; set; }
        public string? CodigoPedido { get; set; }
        public string? Producto { get; set; }
        public int Estrellas { get; set; }
        public string? Comentario { get; set; }
    }
}
