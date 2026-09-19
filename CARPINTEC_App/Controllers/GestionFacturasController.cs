using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    public class GestionFacturasController : Controller
    {
        private readonly CarpintecContext _context;

        public GestionFacturasController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: GestionFacturas
        public async Task<IActionResult> Index()
        {
            try
            {
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");
                string? primerNombreSession = HttpContext.Session.GetString("PrimerNombre");

                if (idUsuario == null)
                    return RedirectToAction("Index", "Login");

                // Buscar usuario y cliente
                Usuario? usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

                Cliente? cliente = null;
                if (usuario != null)
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                        (c.Nombre != null && usuario.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower())
                    );
                }

                // Datos de presentación
                string nombre = cliente?.Nombre ?? usuario?.Nombre ?? primerNombreSession ?? "Cliente";
                string apellido = cliente?.Apellido ?? usuario?.Apellido ?? "";
                string primerNombre = !string.IsNullOrWhiteSpace(nombre)
                    ? nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]
                    : "Cliente";
                string nombreCompleto = $"{nombre} {apellido}".Trim();
                string correo = usuario?.Correo ?? correoSession ?? "";

                string iniciales = "CL";
                if (!string.IsNullOrWhiteSpace(primerNombre))
                {
                    iniciales = primerNombre.Substring(0, 1).ToUpper();
                    if (!string.IsNullOrWhiteSpace(apellido))
                        iniciales += apellido.Trim().Substring(0, 1).ToUpper();
                    else if (primerNombre.Length > 1)
                        iniciales += primerNombre.Substring(1, 1).ToUpper();
                }

                // Cargar cotizaciones del cliente con detalles y productos
                List<Cotizacion> cotizaciones = new();

                if (cliente != null)
                {
                    int idCliente = cliente.IdCliente;
                    string correoLower = (correo ?? "").ToLower();
                    string nombreLower = nombre.ToLower();

                    cotizaciones = await _context.Cotizaciones
                        .Include(c => c.DetalleCotizacions)
                            .ThenInclude(d => d.IdProductoNavigation)
                        .Where(c =>
                            c.IdCliente == idCliente ||
                            (c.CorreoCliente != null && c.CorreoCliente.ToLower() == correoLower) ||
                            (c.NombreCliente != null && c.NombreCliente.ToLower().Contains(nombreLower))
                        )
                        .OrderByDescending(c => c.IdCotizacion)
                        .ToListAsync();
                }
                else if (!string.IsNullOrEmpty(correo))
                {
                    string correoLower = correo.ToLower();
                    cotizaciones = await _context.Cotizaciones
                        .Include(c => c.DetalleCotizacions)
                            .ThenInclude(d => d.IdProductoNavigation)
                        .Where(c =>
                            c.CorreoCliente != null &&
                            c.CorreoCliente.ToLower() == correoLower
                        )
                        .OrderByDescending(c => c.IdCotizacion)
                        .ToListAsync();
                }

                // Métricas
                decimal totalFacturado = cotizaciones.Sum(c => c.Total);
                int totalCotizaciones = cotizaciones.Count;
                int pendientes = cotizaciones.Count(c =>
                    c.Estado != null && (
                        c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
                        c.Estado.Equals("En revisión", StringComparison.OrdinalIgnoreCase) ||
                        c.Estado.Equals("Enviada", StringComparison.OrdinalIgnoreCase)
                    ));
                int pagadas = cotizaciones.Count(c =>
                    c.Estado != null && (
                        c.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase) ||
                        c.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) ||
                        c.Estado.Equals("Finalizada", StringComparison.OrdinalIgnoreCase)
                    ));
                decimal montoPendiente = cotizaciones
                    .Where(c => c.Estado != null && (
                        c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
                        c.Estado.Equals("En revisión", StringComparison.OrdinalIgnoreCase)
                    ))
                    .Sum(c => c.Total);

                ViewBag.Cotizaciones = cotizaciones;
                ViewBag.NombreCompleto = nombreCompleto;
                ViewBag.PrimerNombre = primerNombre;
                ViewBag.Iniciales = iniciales;
                ViewBag.Correo = correo;
                ViewBag.TotalFacturado = totalFacturado;
                ViewBag.TotalCotizaciones = totalCotizaciones;
                ViewBag.Pendientes = pendientes;
                ViewBag.Pagadas = pagadas;
                ViewBag.MontoPendiente = montoPendiente;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar las facturas: " + ex.Message;
                ViewBag.Cotizaciones = new List<Cotizacion>();
                ViewBag.TotalFacturado = 0m;
                ViewBag.TotalCotizaciones = 0;
                ViewBag.Pendientes = 0;
                ViewBag.Pagadas = 0;
                ViewBag.MontoPendiente = 0m;
                ViewBag.NombreCompleto = HttpContext.Session.GetString("NombreUsuario") ?? "Cliente";
                ViewBag.PrimerNombre = HttpContext.Session.GetString("PrimerNombre") ?? "Cliente";
                ViewBag.Iniciales = "CL";
                return View();
            }
        }
    }
}
