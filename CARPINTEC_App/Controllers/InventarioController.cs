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

            // 2. Historial de movimientos reales ordenados del más reciente al más antiguo
            var historialMovimientos = await _context.MovimientosInventario
                .OrderByDescending(m => m.FechaMovimiento)
                .Take(5)
                .ToListAsync();

            ViewBag.HistorialMovimientos = historialMovimientos;
            ViewBag.ProductosBajos = productosBajos;

            // === ESTADÍSTICAS DINÁMICAS ===

            // Productos totales
            ViewBag.ProductosTotales = totalRegistros;

            // Conteo de stock bajo
            ViewBag.StockBajoCount = productosBajos.Count;

            // Valor total de inventario = Σ(StockActual × PrecioCompra)
            ViewBag.ValorInventario = await _context.VistaInventario
                .SumAsync(p => (decimal?)(p.StockActual * p.PrecioCompra)) ?? 0m;

            // Movimientos en los últimos 30 días
            ViewBag.MovimientosCount = await _context.MovimientosInventario
                .Where(m => m.FechaMovimiento >= DateTime.Now.AddDays(-30))
                .CountAsync();

            // Datos de paginación existentes
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistrosPorPagina = registrosPorPagina;

            // Cargar diccionario de categorías: IdProducto -> Categoria
            var listaTodosProductos = listaInventario.Select(i => i.IdProducto).Distinct().ToList();
            var categorias = await _context.Productos
                .Where(p => listaTodosProductos.Contains(p.IdProducto))
                .ToDictionaryAsync(p => p.IdProducto, p => p.Categoria ?? "General");
            ViewBag.Categorias = categorias;

            // Lista completa de inventario para el select del modal de reposición
            var todosInventario = await _context.VistaInventario.ToListAsync();
            ViewBag.TodosInventario = todosInventario;

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

        // POST: InventarioController/Edit/5
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

                // Actualizar campos editables
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
            catch (Exception)
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

            _context.SolicitudesReposicion.Add(solicitud);
            await _context.SaveChangesAsync();

            TempData["Exito"] = "Solicitud de reposición creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // POST: InventarioController/Delete/5
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
            catch (Exception)
            {
                TempData["Error"] = "No se pudo eliminar el registro. Puede tener dependencias.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

