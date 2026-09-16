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

                // 3. Consultar los pedidos reales del cliente desde la base de datos
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
    }
}
