using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CARPINTEC_App.Controllers
{
    public class DashboardClienteController : Controller
    {
        private readonly CarpintecContext _context;

        public DashboardClienteController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: DashboardClienteController
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Obtener información del usuario en sesión
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? primerNombreSession = HttpContext.Session.GetString("PrimerNombre");
                string? nombreCompletoSession = HttpContext.Session.GetString("NombreUsuario");

                Usuario? usuario = null;
                Cliente? cliente = null;

                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                    if (usuario != null)
                    {
                        cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                            (c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                            (c.Nombre != null && usuario.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower())
                        );
                    }
                }

                // 2. Extraer nombre y apellido limpios
                string nombre = cliente?.Nombre ?? usuario?.Nombre ?? primerNombreSession ?? "Cliente";
                string apellido = cliente?.Apellido ?? usuario?.Apellido ?? "";
                string primerNombre = !string.IsNullOrWhiteSpace(nombre)
                    ? nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]
                    : "Cliente";

                string nombreCompleto = $"{nombre} {apellido}".Trim();
                if (string.IsNullOrWhiteSpace(nombreCompleto))
                {
                    nombreCompleto = nombreCompletoSession ?? primerNombre;
                }

                // 3. Determinar saludo personalizado (Bienvenido / Bienvenida)
                string saludo = ObtenerSaludo(primerNombre);

                // 4. Iniciales para el avatar
                string iniciales = "CL";
                if (!string.IsNullOrWhiteSpace(primerNombre))
                {
                    iniciales = primerNombre.Substring(0, 1).ToUpper();
                    if (!string.IsNullOrWhiteSpace(apellido))
                    {
                        iniciales += apellido.Trim().Substring(0, 1).ToUpper();
                    }
                    else if (primerNombre.Length > 1)
                    {
                        iniciales += primerNombre.Substring(1, 1).ToUpper();
                    }
                }

                // 5. Fecha actual formateada en español
                var culturaEs = new CultureInfo("es-CO");
                string fechaFormateada = culturaEs.TextInfo.ToTitleCase(DateTime.Now.ToString("dddd, d 'de' MMMM yyyy", culturaEs));

                // 6. Asignar datos al ViewBag
                ViewBag.Saludo = saludo;
                ViewBag.PrimerNombre = primerNombre;
                ViewBag.NombreCompleto = nombreCompleto;
                ViewBag.SaludoPersonalizado = $"{saludo}, {primerNombre}";
                ViewBag.Iniciales = iniciales;
                ViewBag.FechaActual = fechaFormateada;

                // 7. Cargar métricas reales del cliente si está disponible
                int idCliente = cliente?.IdCliente ?? 1;
                string? correo = cliente?.Correo ?? usuario?.Correo;

                var cotizacionesCliente = await _context.Cotizaciones
                    .Where(c => c.IdCliente == idCliente || (!string.IsNullOrEmpty(correo) && c.CorreoCliente == correo))
                    .ToListAsync();

                ViewBag.TotalCotizaciones = cotizacionesCliente.Count;
                ViewBag.CotizacionesPendientes = cotizacionesCliente.Count(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase));
                ViewBag.CotizacionesAprobadas = cotizacionesCliente.Count(c => c.Estado != null && (c.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase)));

                var pedidosCliente = await _context.Pedidos
                    .Where(p => p.IdCliente == idCliente)
                    .ToListAsync();

                ViewBag.TotalPedidos = pedidosCliente.Count;
                ViewBag.PedidosEnFabricacion = pedidosCliente.Count(p => p.Estado != null && (p.Estado.Contains("Fabricación") || p.Estado.Contains("Proceso") || p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase)));
                ViewBag.PedidosListos = pedidosCliente.Count(p => p.Estado != null && (p.Estado.Contains("Listo") || p.Estado.Contains("Finalizado") || p.Estado.Contains("Entregado")));

                var facturasCliente = await _context.Facturas
                    .Where(f => (!string.IsNullOrEmpty(nombreCompleto) && f.Cliente.Contains(nombreCompleto)) ||
                                (cliente != null && !string.IsNullOrEmpty(cliente.Nombre) && f.Cliente.Contains(cliente.Nombre)))
                    .ToListAsync();

                ViewBag.TotalFacturas = facturasCliente.Count;
                ViewBag.FacturasPendientes = facturasCliente.Count(f => f.Estado != null && f.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase));
                ViewBag.FacturasPagadas = facturasCliente.Count(f => f.Estado != null && f.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase));

                var pqrsCliente = await _context.Pqrs
                    .Where(p => p.IdCliente == idCliente)
                    .ToListAsync();

                ViewBag.TotalPqrs = pqrsCliente.Count;
                ViewBag.PqrsAbiertas = pqrsCliente.Count(p => p.Estado != null && (p.Estado.Equals("Abierto", StringComparison.OrdinalIgnoreCase) || p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase)));
                ViewBag.PqrsResueltas = pqrsCliente.Count(p => p.Estado != null && (p.Estado.Equals("Resuelto", StringComparison.OrdinalIgnoreCase) || p.Estado.Equals("Cerrado", StringComparison.OrdinalIgnoreCase)));

                return View();
            }
            catch
            {
                ViewBag.Saludo = "Bienvenido";
                ViewBag.PrimerNombre = "Cliente";
                ViewBag.NombreCompleto = "Cliente";
                ViewBag.SaludoPersonalizado = "Bienvenido, Cliente";
                ViewBag.Iniciales = "CL";
                ViewBag.FechaActual = DateTime.Now.ToString("dddd, d 'de' MMMM yyyy", new CultureInfo("es-CO"));
                return View();
            }
        }

        /// <summary>
        /// Determina inteligentemente si el saludo debe ser "Bienvenida" o "Bienvenido" según el nombre
        /// </summary>
        public static string ObtenerSaludo(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return "Bienvenido";
            }

            string primerNombre = nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();

            // Nombres masculinos conocidos que terminan en 'a' o 'as'
            string[] masculinosTerminanEnA = {
                "joshua", "luca", "lucas", "sasha", "elias", "elías", "isaias", "isaías",
                "matias", "matías", "borja", "bautista", "dakota", "nikita", "ezra"
            };

            if (masculinosTerminanEnA.Contains(primerNombre))
            {
                return "Bienvenido";
            }

            // Nombres femeninos comunes que no terminan en 'a'
            string[] femeninosNoTerminanEnA = {
                "carmen", "isabel", "raquel", "pilar", "mercedes", "luz", "beatriz",
                "ines", "inés", "rocio", "rocío", "consuelo", "rosario", "mar",
                "belen", "belén", "guadalupe", "merced", "ester", "esther", "miriam",
                "myriam", "judith", "ruth", "astrid", "ingrid", "abigail", "sarai",
                "salome", "salomé", "monserrat", "monzerrat", "sol", "iris", "ivon", "ivonne",
                "karen", "sharon", "shirley", "mary", "mabel", "janet", "yanet"
            };

            if (femeninosNoTerminanEnA.Contains(primerNombre) || primerNombre.EndsWith("a") || primerNombre.EndsWith("á"))
            {
                return "Bienvenida";
            }

            return "Bienvenido";
        }
    }
}
