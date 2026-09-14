using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly CarpintecContext _context;

        // Inyectamos el contexto de la base de datos
        public InventarioController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: InventarioController
        public async Task<IActionResult> Index(int pagina = 1)
        {
            int registrosPorPagina = 5;

            var query = _context.VistaInventario;

            int totalRegistros = await query.CountAsync();
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / registrosPorPagina);

            var listaInventario = await query
                .Skip((pagina - 1) * registrosPorPagina)
                .Take(registrosPorPagina)
                .ToListAsync<Inventario>();

            // 1. Productos con stock bajo (1 a 10 unidades)
            var productosBajos = await _context.VistaInventario
                .Where(p => p.StockActual >= 1 && p.StockActual <= 10)
                .ToListAsync();
            ViewBag.ProductosBajos = productosBajos;

            // 2. Historial de movimientos reales (Últimos 3)
            var historialMovimientos = await _context.MovimientosInventario
                .OrderByDescending(m => m.FechaMovimiento)
                .Take(3)
                .ToListAsync();
            ViewBag.HistorialMovimientos = historialMovimientos;



            // Datos de paginación existentes
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistrosPorPagina = registrosPorPagina;

            ViewBag.TotalProductos = totalRegistros;

            ViewBag.TotalStockBajo = await _context.VistaInventario
                .Where(p => p.StockActual >= 1 && p.StockActual <= 10)
                .CountAsync();

            ViewBag.ValorInventario = await _context.VistaInventario
                .SumAsync(p => p.StockActual * p.PrecioCompra);

            var fechaLimite = DateTime.Now.AddDays(-30);
            ViewBag.TotalMovimientos = await _context.MovimientosInventario
                .Where(m => m.FechaMovimiento >= fechaLimite)
                .CountAsync();

            return View(listaInventario);
        }

        public IActionResult Rebastecimiento()
        {
            return View();
        }
        public IActionResult GestionFacturas()
        {
            return View();
        }
        // GET: InventarioController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: InventarioController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: InventarioController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: InventarioController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: InventarioController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearReposicion(SolicitudReposicion solicitud)
        {
            solicitud.FechaCreacion = DateTime.Now;

            // 1. Guardamos la solicitud de reposición
            _context.SolicitudesReposicion.Add(solicitud);

            // 2. Creamos automáticamente el registro en el historial de movimientos
            var movimiento = new MovimientoInventario
            {
                Tipo = "Entrada",
                NombreProducto = "Producto ID: " + solicitud.ProductoId,
                Cantidad = solicitud.Cantidad,
                UnidadMedida = "Unidades",
                ReferenciaOProyecto = "Reposición solicitada (" + solicitud.Motivo + ")",
                FechaMovimiento = DateTime.Now
            };

            _context.MovimientosInventario.Add(movimiento);

            // 3. Guardamos ambos cambios de golpe en la base de datos
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: InventarioController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: InventarioController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}