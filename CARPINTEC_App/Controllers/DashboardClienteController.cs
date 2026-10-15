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
                ViewBag.CorreoUsuario = HttpContext.Session.GetString("CorreoUsuario") ?? usuario?.Correo ?? "";

                // 7. Cargar métricas reales del cliente si está disponible
                int idCliente = cliente?.IdCliente ?? 1;
                string? correo = cliente?.Correo ?? usuario?.Correo;

                var cotizacionesCliente = await _context.Cotizaciones
                    .Where(c => c.IdCliente == idCliente || (!string.IsNullOrEmpty(correo) && c.CorreoCliente == correo))
                    .ToListAsync();

                ViewBag.TotalCotizaciones = cotizacionesCliente.Count;
                ViewBag.CotizacionesPendientes = cotizacionesCliente.Count(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase));
                ViewBag.CotizacionesAprobadas = cotizacionesCliente.Count(c => c.Estado != null && (c.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase)));

                string correoFiltro = (correo ?? "").ToLower();
                string nombreFiltro = (nombreCompleto ?? "").ToLower();

                var pedidosCliente = await _context.Pedidos
                    .Include(p => p.IdCotizacionNavigation)
                    .Where(p => 
                        (idCliente > 0 && p.IdCliente == idCliente) ||
                        (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(correoFiltro) && p.IdCotizacionNavigation.CorreoCliente != null && p.IdCotizacionNavigation.CorreoCliente.ToLower() == correoFiltro) ||
                        (p.IdCotizacionNavigation != null && !string.IsNullOrEmpty(nombreFiltro) && p.IdCotizacionNavigation.NombreCliente != null && p.IdCotizacionNavigation.NombreCliente.ToLower() == nombreFiltro)
                    )
                    .OrderByDescending(p => p.FechaRegistro ?? DateTime.MinValue)
                    .ToListAsync();

                ViewBag.TotalPedidos = pedidosCliente.Count;
                ViewBag.PedidosEnFabricacion = pedidosCliente.Count(p => p.Estado != null && (p.Estado.Contains("Preparación") || p.Estado.Contains("Fabricación") || p.Estado.Contains("Proceso") || p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase)));
                ViewBag.PedidosListos = pedidosCliente.Count(p => p.Estado != null && (p.Estado.Contains("Listo") || p.Estado.Contains("Finalizado") || p.Estado.Contains("Entregado") || p.Estado.Contains("Despacho")));

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
                // 8. GENERAR NOTIFICACIONES REALES Y ACTUALIZADAS:
                //    1. PRODUCTOS PENDIENTES DE PAGO
                //    2. PRODUCTOS PAGOS (CONFIRMADOS / EN PREPARACIÓN)
                //    3. PRODUCTOS EN DESPACHO Y ENTREGA AL CLIENTE
                //    4. NUEVOS PRODUCTOS DEL CATÁLOGO
                // ==============================================================
                var listaNotificaciones = new List<NotificacionClienteItem>();

                // Pre-cargar ventas del cliente para asociar IdVenta
                var ventasClienteParaFacturas = await _context.Ventas
                    .Include(v => v.IdPedidoNavigation)
                    .Where(v => v.IdCliente == idCliente || (v.IdPedidoNavigation != null && v.IdPedidoNavigation.IdCliente == idCliente))
                    .OrderByDescending(v => v.FechaVenta)
                    .ToListAsync();

                // -------------------------------------------------------------
                // 1. PRODUCTOS PENDIENTES DE PAGO (Facturas o Ventas pendientes)
                // -------------------------------------------------------------
                var facturasPendientes = facturasCliente
                    .Where(f => f.Estado != null && (f.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) || f.Estado.Equals("Vencida", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(f => f.Fecha)
                    .ToList();

                foreach (var f in facturasPendientes)
                {
                    var ventaAsociada = ventasClienteParaFacturas
                        .FirstOrDefault(v => v.NumeroFactura != null && v.NumeroFactura == f.Folio);

                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "pago",
                        Titulo = $"Factura #{f.Folio} pendiente de pago",
                        Mensaje = $"Tienes un saldo pendiente de {f.Total.ToString("C0", culturaEs)}. Completa el pago para iniciar la fabricación.",
                        Monto = f.Total.ToString("C0", culturaEs),
                        Codigo = f.Folio,
                        Estado = f.Estado,
                        Icono = "receipt_long",
                        ColorBadge = "bg-rose-500",
                        UrlAccion = "/GestionFacturas",
                        TextoAccion = "Pagar Ahora",
                        FechaStr = f.Fecha.ToString("d MMM yyyy", culturaEs),
                        IdVenta = ventaAsociada?.IdVenta ?? 0
                    });
                }

                var ventasPendientes = ventasClienteParaFacturas
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
                            Mensaje = $"Tienes un valor pendiente de {v.Total.ToString("C0", culturaEs)}. Completa el pago para enviar al taller.",
                            Monto = v.Total.ToString("C0", culturaEs),
                            Codigo = v.NumeroFactura ?? $"VNT-{v.IdVenta}",
                            Estado = "Pendiente",
                            Icono = "payments",
                            ColorBadge = "bg-rose-500",
                            UrlAccion = "/GestionFacturas",
                            TextoAccion = "Pagar Saldo",
                            FechaStr = v.FechaVenta.ToString("d MMM yyyy", culturaEs),
                            IdVenta = v.IdVenta
                        });
                    }
                }

                var cotPendientes = cotizacionesCliente
                    .Where(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToList();

                foreach (var c in cotPendientes)
                {
                    if (!listaNotificaciones.Any(n => n.Codigo == c.Folio))
                    {
                        listaNotificaciones.Add(new NotificacionClienteItem
                        {
                            Tipo = "pago",
                            Titulo = $"Cotización #{c.Folio} en espera de pago",
                            Mensaje = $"Tu cotización para \"{c.DetalleProducto}\" ({c.Total.ToString("C0", culturaEs)}) está lista para pagar y pasar a preparación.",
                            Monto = c.Total.ToString("C0", culturaEs),
                            Codigo = c.Folio,
                            Estado = "Pendiente",
                            Icono = "request_quote",
                            ColorBadge = "bg-amber-600",
                            UrlAccion = "/MiCotizacion",
                            TextoAccion = "Pagar Cotización",
                            FechaStr = "En espera"
                        });
                    }
                }

                // -------------------------------------------------------------
                // 2. PRODUCTOS PAGOS (Pago confirmado / En preparación en taller)
                // -------------------------------------------------------------
                var pedidosPagos = pedidosCliente
                    .Where(p => p.Estado != null && !p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) && !p.Estado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToList();

                foreach (var ped in pedidosPagos)
                {
                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "pagado",
                        Titulo = $"¡Pago Confirmado! - {ped.Producto}",
                        Mensaje = $"Pago recibido exitosamente. Tu pedido #{ped.CodigoPedido} está en estado \"{ped.Estado}\". Entrega estimada: {ped.FechaEntrega.ToString("d 'de' MMMM", culturaEs)}.",
                        Monto = ped.ValorTotal > 0 ? ped.ValorTotal.ToString("C0", culturaEs) : null,
                        Codigo = ped.CodigoPedido,
                        Estado = ped.Estado,
                        Icono = "verified",
                        ColorBadge = "bg-emerald-600",
                        UrlAccion = "/Mi_Pedido",
                        TextoAccion = "Ver en Mi Pedido",
                        FechaStr = ped.FechaRegistro.HasValue ? ped.FechaRegistro.Value.ToString("d MMM", culturaEs) : "Reciente"
                    });
                }

                // -------------------------------------------------------------
                // 3. PRODUCTOS EN PROCESO DE DESPACHO Y ENTREGA AL CLIENTE
                // -------------------------------------------------------------
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                var pedidosDespacho = pedidosCliente
                    .Where(p => p.Estado != null && (
                        p.Estado.Contains("Despacho", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Contains("Entrega", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Contains("Listo", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Contains("Completado", StringComparison.OrdinalIgnoreCase) ||
                        p.Estado.Contains("Entregado", StringComparison.OrdinalIgnoreCase) ||
                        (p.FechaEntrega != default && p.FechaEntrega >= hoy && p.FechaEntrega <= hoy.AddDays(7))
                    ))
                    .Take(3)
                    .ToList();

                foreach (var ped in pedidosDespacho)
                {
                    bool esEntregado = ped.Estado.Contains("Entregado", StringComparison.OrdinalIgnoreCase);
                    string tituloDesp = esEntregado
                        ? $"🚚 ¡Entregado con éxito! - {ped.Producto}"
                        : $"🚚 En proceso de Despacho y Entrega - {ped.Producto}";
                    string msgDesp = esEntregado
                        ? $"Tu pedido #{ped.CodigoPedido} ha sido entregado exitosamente al cliente. ¡Esperamos que disfrutes tu mueble Carpintec!"
                        : $"El pedido #{ped.CodigoPedido} ha salido a reparto o está en coordinación final de entrega al cliente. Entrega: {ped.FechaEntrega.ToString("d 'de' MMMM", culturaEs)}.";

                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "despacho",
                        Titulo = tituloDesp,
                        Mensaje = msgDesp,
                        Codigo = ped.CodigoPedido,
                        Estado = ped.Estado,
                        Icono = "local_shipping",
                        ColorBadge = "bg-sky-600",
                        UrlAccion = "/Mi_Pedido",
                        TextoAccion = "Rastrear Entrega",
                        FechaStr = ped.FechaEntrega != default ? $"Entrega: {ped.FechaEntrega.ToString("d MMM", culturaEs)}" : "En ruta"
                    });
                }

                // -------------------------------------------------------------
                // 4. NUEVOS PRODUCTOS DEL CATÁLOGO
                // -------------------------------------------------------------
                var nuevosProductos = await _context.Productos
                    .Where(p => p.Estado == null || p.Estado == "Activo")
                    .OrderByDescending(p => p.IdProducto)
                    .Take(3)
                    .ToListAsync();

                foreach (var prod in nuevosProductos)
                {
                    string precioFmt = prod.Precio.HasValue ? prod.Precio.Value.ToString("C0", culturaEs) : "A convenir";
                    string matFmt = !string.IsNullOrWhiteSpace(prod.Material) ? prod.Material : "Maderas seleccionadas";
                    string medFmt = !string.IsNullOrWhiteSpace(prod.Medidas) ? prod.Medidas : "Diseño a medida";

                    listaNotificaciones.Add(new NotificacionClienteItem
                    {
                        Tipo = "catalogo",
                        Titulo = $"✨ Nuevo en Catálogo: {prod.Nombre}",
                        Mensaje = $"Nuevo modelo disponible en madera {matFmt} ({medFmt}). Disponible desde {precioFmt}. ¡Cotízalo a medida hoy!",
                        Monto = precioFmt,
                        Codigo = prod.Codigo ?? $"PRD-{prod.IdProducto:D3}",
                        Estado = "Nuevo",
                        Icono = "storefront",
                        ColorBadge = "bg-amber-600",
                        UrlAccion = "/MisProductos",
                        TextoAccion = "Ver en Catálogo",
                        FechaStr = prod.FechaRegistro.HasValue ? prod.FechaRegistro.Value.ToString("d MMM", culturaEs) : "Nuevo"
                    });
                }

                ViewBag.ListaNotificaciones = listaNotificaciones;
                ViewBag.TotalNotificaciones = listaNotificaciones.Count;
                ViewBag.TotalPagosPendientes = listaNotificaciones.Count(n => n.Tipo == "pago");
                ViewBag.TotalPagados = listaNotificaciones.Count(n => n.Tipo == "pagado");
                ViewBag.TotalDespacho = listaNotificaciones.Count(n => n.Tipo == "despacho");
                ViewBag.TotalCatalogo = listaNotificaciones.Count(n => n.Tipo == "catalogo");
                ViewBag.PrimerPagoPendiente = listaNotificaciones.FirstOrDefault(n => n.Tipo == "pago");
                ViewBag.PrimerPagado = listaNotificaciones.FirstOrDefault(n => n.Tipo == "pagado");
                ViewBag.PrimerDespacho = listaNotificaciones.FirstOrDefault(n => n.Tipo == "despacho");
                ViewBag.PrimerCatalogo = listaNotificaciones.FirstOrDefault(n => n.Tipo == "catalogo");

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

        // POST: Procesar pago desde modal de notificaciones (Dashboard Cliente)
        // Si el cliente cancela, el pedido NO pasa a "En Proceso"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarPagoDesdeNotificacion(int idVenta, string tipoPago, int? numeroCuotas)
        {
            try
            {
                var venta = await _context.Ventas.FindAsync(idVenta);
                if (venta == null)
                    return Json(new { success = false, mensaje = "Venta no encontrada." });

                if (tipoPago == "Contado")
                {
                    venta.Estado = "Pagada";
                    venta.MetodoPago = "Tarjeta (Simulado)";

                    // SOLO cuando se confirma el pago, el pedido pasa a "En Preparación"
                    var pedido = await _context.Pedidos.FindAsync(venta.IdPedido);
                    if (pedido != null &&
                        (pedido.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
                         pedido.Estado.Equals("Cotizado", StringComparison.OrdinalIgnoreCase)))
                    {
                        pedido.Estado = "En Preparación";
                        _context.Update(pedido);
                    }

                    // Actualizar factura asociada si existe
                    if (!string.IsNullOrEmpty(venta.NumeroFactura))
                    {
                        var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.Folio == venta.NumeroFactura);
                        if (factura != null)
                        {
                            factura.Estado = "Pagada";
                            _context.Update(factura);
                        }
                    }
                }
                else if (tipoPago == "Cuotas")
                {
                    venta.Estado = "Pendiente";
                    venta.MetodoPago = $"Crédito a {numeroCuotas} cuotas";
                    // El pedido NO cambia de estado — NO pasa a "En Preparación" sin pago confirmado
                }

                _context.Update(venta);
                await _context.SaveChangesAsync();

                var mensaje = tipoPago == "Contado"
                    ? "¡Pago procesado exitosamente! Tu pedido ahora está en preparación de fabricación."
                    : $"Plan a {numeroCuotas} cuotas solicitado. Queda pendiente de validación por el supervisor.";

                return Json(new { success = true, mensaje, tipoPago });
            }
            catch (Exception)
            {
                return Json(new { success = false, mensaje = "Error al procesar el pago. Inténtalo de nuevo." });
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
            public int IdVenta { get; set; }
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
