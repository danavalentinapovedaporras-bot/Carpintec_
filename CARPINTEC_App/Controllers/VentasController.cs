using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
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
        public IActionResult Index()
        {
            // Consultamos la tabla Ventas del contexto
            var ventasList = _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .ToList();

            // --- LISTAS PARA LOS SELECTS DEL MODAL ---
            ViewData["IdCliente"] = new SelectList(_context.Clientes, "IdCliente", "Nombre");
            ViewData["IdPedido"] = new SelectList(_context.Pedidos, "IdPedido", "IdPedido");

            // --- CÁLCULOS PARA LAS TARJETAS BENTO ---
            decimal ventasTotales = ventasList
                .Where(v => v.Estado != "Anulada")
                .Sum(v => v.Total);
            ViewBag.VentasTotalesMes = ventasTotales.ToString("N2");

            var pendientes = ventasList.Where(v => v.Estado == "Pendiente").ToList();
            ViewBag.CantidadPendientes = pendientes.Count;
            ViewBag.ValorPendientesFormatted = pendientes.Sum(v => v.Total).ToString("N2");

            decimal totalRecaudado = ventasList
                .Where(v => v.Estado == "Pagada")
                .Sum(v => v.Total);
            ViewBag.TotalRecaudadoFormatted = totalRecaudado.ToString("N2");

            ViewBag.CantidadProximos = pendientes.Count;
            ViewBag.SiguienteVencimientoFecha = "Al día";

            return View(ventasList);
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