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

        public InventarioController(CarpintecContext context)
        {
            _context = context;
        }

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

            // Productos con stock bajo
            var productosBajos = await _context.VistaInventario
                .Where(p => p.StockActual >= 1 && p.StockActual <= 10)
                .ToListAsync();

            ViewBag.ProductosBajos = productosBajos;

            // Historial de movimientos reales
            var historialMovimientos = await _context.MovimientosInventario
                .OrderByDescending(m => m.FechaMovimiento)
                .Take(5)
                .ToListAsync();

            ViewBag.HistorialMovimientos = historialMovimientos;


            // Estadísticas dinámicas

            ViewBag.ProductosTotales = totalRegistros;

            ViewBag.StockBajoCount = productosBajos.Count;

            ViewBag.ValorInventario = await _context.VistaInventario
                .SumAsync(p => (decimal?)(p.StockActual * p.PrecioCompra)) ?? 0m;


            ViewBag.MovimientosCount = await _context.MovimientosInventario
                .Where(m => m.FechaMovimiento >= DateTime.Now.AddDays(-30))
                .CountAsync();


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


        public ActionResult Details(int id)
        {
            return View();
        }


        public ActionResult Create()
        {
            return View();
        }


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


        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.Inventarios
                .FirstOrDefaultAsync(i => i.IdInventario == id);

            if (item == null)
            {
                TempData["Error"] = "El registro de inventario no fue encontrado.";
                return RedirectToAction(nameof(Index));
            }

            return View(item);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Inventario inventario)
        {
            if (id != inventario.IdInventario)
            {
                TempData["Error"] = "ID de inventario no coincide.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var itemExistente = await _context.Inventarios
                    .FirstOrDefaultAsync(i => i.IdInventario == id);

                if (itemExistente == null)
                {
                    TempData["Error"] = "El registro de inventario no fue encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                itemExistente.StockActual = inventario.StockActual;
                itemExistente.StockMinimo = inventario.StockMinimo;
                itemExistente.UnidadMedida = inventario.UnidadMedida;
                itemExistente.PrecioCompra = inventario.PrecioCompra;
                itemExistente.Ubicacion = inventario.Ubicacion;
                itemExistente.Estado = inventario.Estado;
                itemExistente.FechaActualizacion = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Exito"] = "Registro de inventario actualizado correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "Ocurrió un error al actualizar el registro.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearReposicion(SolicitudReposicion solicitud)
        {
            solicitud.FechaCreacion = DateTime.Now;

            // Guardar solicitud de reposición
            _context.SolicitudesReposicion.Add(solicitud);


            // Crear automáticamente movimiento en historial

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


            await _context.SaveChangesAsync();


            TempData["Exito"] = "Solicitud de reposición creada correctamente.";

            return RedirectToAction(nameof(Index));
        }


        // GET: Delete
        public ActionResult Delete(int id)
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.Inventarios
                    .FirstOrDefaultAsync(i => i.IdInventario == id);

                if (item == null)
                {
                    TempData["Error"] = "El registro de inventario no fue encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Inventarios.Remove(item);

                await _context.SaveChangesAsync();

                TempData["Exito"] = "Registro de inventario eliminado correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "No se pudo eliminar el registro. Puede tener dependencias.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}