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