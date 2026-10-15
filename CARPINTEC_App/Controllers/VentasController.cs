using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    [PermisoCargo("Ventas")]
    public class VentasController : Controller
    {
        private readonly CarpintecContext _context;

        public VentasController(CarpintecContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Venta venta)
        {
            if (ModelState.IsValid)
            {
                // Si el estado viene vacío, le ponemos Pendiente por defecto
                if (string.IsNullOrEmpty(venta.Estado))
                {
                    venta.Estado = "Pendiente";
                }

                _context.Ventas.Add(venta);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            // Si hay un error, recargamos la vista Index con los datos necesarios
            var ventasList = _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .ToList();

            ViewData["IdCliente"] = new SelectList(_context.Clientes, "IdCliente", "Nombre", venta.IdCliente);
            ViewData["IdPedido"] = new SelectList(_context.Pedidos, "IdPedido", "IdPedido", venta.IdPedido);

            return View("Index", ventasList);
        }
        public async Task<IActionResult> Index(string? search, string? estado, int pagina = 1)
        {
            int pageSize = 6;

            var query = _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(v =>
                    (v.NumeroFactura != null && v.NumeroFactura.Contains(s)) ||
                    (v.IdClienteNavigation != null && (v.IdClienteNavigation.Nombre.Contains(s) || v.IdClienteNavigation.Apellido.Contains(s) || (v.IdClienteNavigation.NombreEmpresa != null && v.IdClienteNavigation.NombreEmpresa.Contains(s)))) ||
                    (v.MetodoPago != null && v.MetodoPago.Contains(s)) ||
                    (v.Observaciones != null && v.Observaciones.Contains(s)) ||
                    v.IdVenta.ToString().Contains(s) ||
                    v.IdPedido.ToString().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(estado) && estado != "Estado: Todos" && estado != "Todos")
            {
                query = query.Where(v => v.Estado == estado);
            }

            int totalRegistros = await query.CountAsync();
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);
            if (totalPaginas == 0) totalPaginas = 1;
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas) pagina = totalPaginas;

            var ventasPaginadas = await query
                .OrderByDescending(v => v.FechaVenta)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // --- LISTAS PARA LOS SELECTS DEL MODAL ---
            ViewData["IdCliente"] = new SelectList(await _context.Clientes.ToListAsync(), "IdCliente", "Nombre");
            ViewData["IdPedido"] = new SelectList(await _context.Pedidos.ToListAsync(), "IdPedido", "IdPedido");

            // --- CÁLCULOS PARA LAS TARJETAS BENTO ---
            var todasVentas = await _context.Ventas.ToListAsync();

            decimal ventasTotales = todasVentas
                .Where(v => v.Estado != "Anulada")
                .Sum(v => v.Total);
            ViewBag.VentasTotalesMes = ventasTotales.ToString("N2");

            var pendientes = todasVentas.Where(v => v.Estado == "Pendiente").ToList();
            ViewBag.CantidadPendientes = pendientes.Count;
            ViewBag.ValorPendientesFormatted = pendientes.Sum(v => v.Total).ToString("N2");

            decimal totalRecaudado = todasVentas
                .Where(v => v.Estado == "Pagada")
                .Sum(v => v.Total);
            ViewBag.TotalRecaudadoFormatted = totalRecaudado.ToString("N2");

            ViewBag.CantidadProximos = pendientes.Count;
            ViewBag.SiguienteVencimientoFecha = "Al día";

            // Datos de búsqueda y paginación
            ViewBag.Search = search;
            ViewBag.Estado = estado;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistroInicio = totalRegistros == 0 ? 0 : (pagina - 1) * pageSize + 1;
            ViewBag.RegistroFin = Math.Min(pagina * pageSize, totalRegistros);

            return View(ventasPaginadas);
        }

        public IActionResult VerFactura(int id)
        {
            var venta = _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .FirstOrDefault(v => v.IdVenta == id);

            if (venta == null)
            {
                return NotFound();
            }

            return View(venta);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CheckoutSimulado(int id)
        {
            // Buscamos la venta usando IdVenta (como está en tu base de datos)
            var venta = await _context.Ventas
                .FirstOrDefaultAsync(v => v.IdVenta == id);

            if (venta == null)
            {
                return NotFound();
            }

            // Preparamos el modelo para enviarlo a la vista con datos del cliente y costo de envío base
            decimal subtotal = venta.Subtotal > 0 ? venta.Subtotal : venta.Total;
            decimal costoEnvio = 35000;
            decimal totalCalculado = subtotal + costoEnvio;

            var model = new PasarelaPagoViewModel
            {
                IdVenta = venta.IdVenta,
                NumeroFactura = venta.NumeroFactura ?? $"FAC-{venta.IdVenta}",
                Subtotal = subtotal,
                CostoEnvio = costoEnvio,
                Total = totalCalculado,
                Ciudad = venta.IdClienteNavigation?.Ciudad ?? "Bogotá D.C.",
                Municipio = "Bogotá",
                Direccion = venta.IdClienteNavigation?.Direccion ?? "",
                HotelEmpresa = venta.IdClienteNavigation?.NombreEmpresa ?? ""
            };

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarPagoSimulado(PasarelaPagoViewModel model)
        {
            var venta = await _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .FirstOrDefaultAsync(v => v.IdVenta == model.IdVenta);

            if (venta == null) return NotFound();

            // Actualizar total con costo de envío si aplica
            if (model.Total > 0)
            {
                venta.Total = model.Total;
            }

            // Actualizar datos del cliente si se proporcionaron
            if (venta.IdClienteNavigation != null)
            {
                if (!string.IsNullOrWhiteSpace(model.Direccion)) venta.IdClienteNavigation.Direccion = model.Direccion;
                if (!string.IsNullOrWhiteSpace(model.Ciudad)) venta.IdClienteNavigation.Ciudad = model.Ciudad;
                if (!string.IsNullOrWhiteSpace(model.HotelEmpresa)) venta.IdClienteNavigation.NombreEmpresa = model.HotelEmpresa;
            }

            // Guardar detalle de despacho en observaciones del pedido
            string detalleDespacho = $"[Despacho: {model.Direccion}, {model.Localidad}, {model.Municipio} - {model.Ciudad} (CP: {model.CodigoPostal}). Detalle: {model.Piso ?? "N/A"}. Lugar: {model.HotelEmpresa ?? "Particular"}. Costo Envío: ${model.CostoEnvio:N0}]";
            if (venta.IdPedidoNavigation != null)
            {
                venta.IdPedidoNavigation.Observaciones = $"{venta.IdPedidoNavigation.Observaciones} {detalleDespacho}".Trim();
            }

            // Validamos qué seleccionó el cliente en la pasarela
            if (model.MetodoPago == "Cuotas")
            {
                venta.Estado = "Pendiente"; // Queda pendiente para validación del supervisor
                venta.MetodoPago = $"Crédito a {model.NumeroCuotas ?? 3} cuotas";
                TempData["Mensaje"] = $"Plan a {model.NumeroCuotas ?? 3} cuotas solicitado. Quedó pendiente de validación por el supervisor.";
            }
            else
            {
                venta.Estado = "Pagada"; // Pago simulado exitoso
                venta.MetodoPago = model.MetodoPago switch
                {
                    "PSE" => $"PSE - {model.BancoPse ?? "Bancolombia"}",
                    "Nequi" => $"Nequi ({model.TelefonoNequi ?? "Celular"})",
                    "Daviplata" => $"Daviplata ({model.TelefonoDaviplata ?? "Celular"})",
                    _ => "Tarjeta (Simulado)"
                };
                TempData["Exito"] = $"¡El pago simulado por {venta.Total:C0} con {venta.MetodoPago} se ha procesado con éxito!";

                if (venta.IdPedidoNavigation != null)
                {
                    venta.IdPedidoNavigation.Estado = "En Preparación";
                }
            }

            _context.Update(venta);
            await _context.SaveChangesAsync();

            // Redirige al listado de ventas o al dashboard
            return RedirectToAction("Index", "Ventas");
        }

        [HttpGet]
        public IActionResult DetalleFacturaCard(int id)
        {
            // Buscamos la factura con sus relaciones
            var venta = _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .FirstOrDefault(v => v.IdVenta == id);

            if (venta == null)
            {
                return NotFound();
            }

            // Retorna una "Vista Parcial" con el diseño de la tarjeta
            return PartialView("_DetalleFactura", venta);
        }


    }
}