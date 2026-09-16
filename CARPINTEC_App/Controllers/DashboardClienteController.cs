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

                // ==============================================================
                // 8. GENERAR NOTIFICACIONES REALES: PAGOS PENDIENTES & PRODUCTOS POR TERMINAR
                // ==============================================================
                var listaNotificaciones = new List<NotificacionClienteItem>();

                // A. Pagos Pendientes (Facturas con saldo pendiente)
                var facturasPendientes = facturasCliente
                    .Where(f => f.Estado != null && (f.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) || f.Estado.Equals("Vencida", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(f => f.Fecha)
                    .ToList();

                foreach (var f in facturasPendientes)
                {
                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "pago",
                        Titulo = $"Factura #{f.Folio} pendiente de pago",
                        Mensaje = $"Tienes un saldo pendiente por valor de {f.Total.ToString("C0", culturaEs)}.",
                        Monto = f.Total.ToString("C0", culturaEs),
                        Codigo = f.Folio,
                        Estado = f.Estado,
                        Icono = "receipt_long",
                        ColorBadge = "bg-rose-500",
                        UrlAccion = "/GestionFacturas",
                        TextoAccion = "Pagar Ahora",
                        FechaStr = f.Fecha.ToString("d MMM yyyy", culturaEs)
                    });
                }

                // A.2. Ventas registradas con saldo pendiente
                var ventasCliente = await _context.Ventas
                    .Where(v => v.IdCliente == idCliente)
                    .ToListAsync();

                var ventasPendientes = ventasCliente
                    .Where(v => v.Estado != null && v.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var v in ventasPendientes)
                {
                    if (!listaNotificaciones.Any(n => n.Codigo == v.NumeroFactura))
                    {
                        listaNotificaciones.Add(new NotificacionClienteItem
                        {
                            Tipo = "pago",
                            Titulo = $"Orden #{v.NumeroFactura ?? v.IdVenta.ToString()} pendiente",
                            Mensaje = $"Tienes un valor pendiente de {v.Total.ToString("C0", culturaEs)}.",
                            Monto = v.Total.ToString("C0", culturaEs),
                            Codigo = v.NumeroFactura ?? $"VNT-{v.IdVenta}",
                            Estado = "Pendiente",
                            Icono = "payments",
                            ColorBadge = "bg-rose-500",
                            UrlAccion = "/GestionFacturas",
                            TextoAccion = "Pagar Saldo",
                            FechaStr = v.FechaVenta.ToString("d MMM yyyy", culturaEs)
                        });
                    }
                }

                // A.3. Cotizaciones pendientes de confirmación/pago si aún no hay facturas
                if (!listaNotificaciones.Any(n => n.Tipo == "pago"))
                {
                    var cotPendientes = cotizacionesCliente
                        .Where(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                        .Take(2)
                        .ToList();

                    foreach (var c in cotPendientes)
                    {
                        listaNotificaciones.Add(new NotificacionClienteItem
                        {
                            Tipo = "pago",
                            Titulo = $"Cotización #{c.Folio} en espera",
                            Mensaje = $"Tu cotización para \"{c.DetalleProducto}\" ({c.Total.ToString("C0", culturaEs)}) está lista para confirmación y pago.",
                            Monto = c.Total.ToString("C0", culturaEs),
                            Codigo = c.Folio,
                            Estado = "Pendiente",
                            Icono = "request_quote",
                            ColorBadge = "bg-amber-600",
                            UrlAccion = "/MiCotizacion",
                            TextoAccion = "Ver y Confirmar",
                            FechaStr = "En espera"
                        });
                    }
                }

                // B. Productos por Terminar o en fase avanzada en el taller
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                var pedidosPorTerminar = pedidosCliente
                    .Where(p => p.Estado != null && (
                        p.Estado.Equals("Pintura", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Equals("Ensamble", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Equals("Completado", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Equals("Listo", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Contains("Entrega", StringComparison.OrdinalIgnoreCase) ||
                        (p.FechaEntrega != default && p.FechaEntrega >= hoy && p.FechaEntrega <= hoy.AddDays(7) && !p.Estado.Equals("Entregado", StringComparison.OrdinalIgnoreCase))
                    ))
                    .OrderByDescending(p => p.FechaRegistro ?? DateTime.MinValue)
                    .ToList();

                foreach (var ped in pedidosPorTerminar)
                {
                    bool esListo = ped.Estado.Equals("Completado", StringComparison.OrdinalIgnoreCase) || ped.Estado.Equals("Listo", StringComparison.OrdinalIgnoreCase);
                    string titulo = esListo ? $"¡Tu {ped.Producto} está listo!" : $"¡Tu {ped.Producto} está por terminar!";
                    string desc = esListo
                        ? $"El pedido #{ped.CodigoPedido} ha finalizado su fabricación con éxito y está listo para ser despachado o entregado."
                        : $"El pedido #{ped.CodigoPedido} está en fase de {ped.Estado}. Entrega programada para el {ped.FechaEntrega.ToString("d 'de' MMMM", culturaEs)}.";

                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "terminar",
                        Titulo = titulo,
                        Mensaje = desc,
                        Codigo = ped.CodigoPedido,
                        Estado = ped.Estado,
                        Icono = esListo ? "verified" : "build_circle",
                        ColorBadge = esListo ? "bg-emerald-600" : "bg-teal-600",
                        UrlAccion = "/Mi_Pedido",
                        TextoAccion = "Ver en Mi Pedido",
                        FechaStr = $"Entrega: {ped.FechaEntrega.ToString("d MMM", culturaEs)}"
                    });
                }

                // Si no hay pedidos en fase final pero sí en taller (ej. recién cotizado):
                if (!pedidosPorTerminar.Any())
                {
                    var pedidosActivos = pedidosCliente
                        .Where(p => p.Estado != null && !p.Estado.Equals("Entregado", StringComparison.OrdinalIgnoreCase) && !p.Estado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(p => p.FechaRegistro ?? DateTime.MinValue)
                        .Take(2)
                        .ToList();

                    foreach (var ped in pedidosActivos)
                    {
                        listaNotificaciones.Add(new NotificacionClienteItem
                        {
                            Tipo = "terminar",
                            Titulo = $"Pedido en Taller: {ped.Producto}",
                            Mensaje = $"Tu orden #{ped.CodigoPedido} está en estado \"{ped.Estado}\". Entrega estimada: {ped.FechaEntrega.ToString("d 'de' MMMM", culturaEs)}.",
                            Codigo = ped.CodigoPedido,
                            Estado = ped.Estado,
                            Icono = "handyman",
                            ColorBadge = "bg-[#8b4513]",
                            UrlAccion = "/Mi_Pedido",
                            TextoAccion = "Ver Seguimiento",
                            FechaStr = $"Entrega: {ped.FechaEntrega.ToString("d MMM", culturaEs)}"
                        });
                    }
                }

                ViewBag.ListaNotificaciones = listaNotificaciones;
                ViewBag.TotalNotificaciones = listaNotificaciones.Count;
                ViewBag.TotalPagosPendientes = listaNotificaciones.Count(n => n.Tipo == "pago");
                ViewBag.TotalProductosPorTerminar = listaNotificaciones.Count(n => n.Tipo == "terminar");
                ViewBag.PrimerPagoPendiente = listaNotificaciones.FirstOrDefault(n => n.Tipo == "pago");
                ViewBag.PrimerProductoPorTerminar = listaNotificaciones.FirstOrDefault(n => n.Tipo == "terminar");

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
                ViewBag.ListaNotificaciones = new List<NotificacionClienteItem>();
                ViewBag.TotalNotificaciones = 0;
                ViewBag.TotalPagosPendientes = 0;
                ViewBag.TotalProductosPorTerminar = 0;
                return View();
            }
        }

        public class NotificacionClienteItem
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Tipo { get; set; } = "info"; // "pago" | "terminar"
            public string Titulo { get; set; } = "";
            public string Mensaje { get; set; } = "";
            public string? Monto { get; set; }
            public string? Codigo { get; set; }
            public string? Estado { get; set; }
            public string Icono { get; set; } = "notifications";
            public string ColorBadge { get; set; } = "bg-amber-500";
            public string UrlAccion { get; set; } = "#";
            public string TextoAccion { get; set; } = "Ver detalles";
            public string FechaStr { get; set; } = "Hoy";
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
