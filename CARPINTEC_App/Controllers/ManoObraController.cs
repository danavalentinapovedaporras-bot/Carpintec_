using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    [PermisoCargo("Producción")]
    public class ManoObraController : Controller
    {
        private readonly CarpintecContext _context;

        public ManoObraController(CarpintecContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var listaManoObra = await _context.ManoObras
                    .Include(m => m.IdEmpleadoNavigation)
                    .Include(m => m.IdPedidoNavigation)
                    .Include(m => m.IdProductoNavigation)
                    .ToListAsync();

                // Cargar lista de productos para la vista
                var listaProductos = await _context.Productos.ToListAsync();

                ViewBag.ListaProductos = listaProductos;

                return View(listaManoObra);
            }
            catch
            {
                ViewBag.ListaProductos = new List<object>();

                return View(new List<ManoObra>());
            }
        }


        // POST: Crear Mano Obra
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ManoObra manoObra)
        {
            try
            {
                ModelState.Remove("IdEmpleadoNavigation");
                ModelState.Remove("IdPedidoNavigation");
                ModelState.Remove("IdProductoNavigation");

                if (ModelState.IsValid)
                {
                    _context.ManoObras.Add(manoObra);

                    await _context.SaveChangesAsync();

                    TempData["Exito"] = "Asignación creada correctamente.";
                }
                else
                {
                    TempData["Error"] = "Datos inválidos.";
                }
            }
            catch(Exception ex)
            {
                TempData["Error"] = "Error al crear: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }


        // GET Obtener datos para modal
        [HttpGet]
        public async Task<IActionResult> ObtenerManoObra(int id)
        {
            var item = await _context.ManoObras
                .Include(m => m.IdEmpleadoNavigation)
                .Include(m => m.IdPedidoNavigation)
                .Include(m => m.IdProductoNavigation)
                .FirstOrDefaultAsync(m => m.IdManoObra == id);


            if(item == null)
            {
                return NotFound();
            }


            return Json(item);
        }



        // POST Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(ManoObra manoObra)
        {

            try
            {

                var existente = await _context.ManoObras
                    .FirstOrDefaultAsync(m => m.IdManoObra == manoObra.IdManoObra);


                if(existente == null)
                {
                    TempData["Error"] = "Registro no encontrado.";
                    return RedirectToAction(nameof(Index));
                }


                existente.IdEmpleado = manoObra.IdEmpleado;
                existente.IdPedido = manoObra.IdPedido;
                existente.IdProducto = manoObra.IdProducto;
                existente.Proceso = manoObra.Proceso;
                existente.HorasEstimadas = manoObra.HorasEstimadas;
                existente.HorasReales = manoObra.HorasReales;
                existente.CostoHora = manoObra.CostoHora;
                existente.Estado = manoObra.Estado;
                existente.FechaInicio = manoObra.FechaInicio;
                existente.FechaFin = manoObra.FechaFin;
                existente.Observaciones = manoObra.Observaciones;


                await _context.SaveChangesAsync();


                TempData["Exito"] = "Asignación actualizada correctamente.";

            }
            catch(Exception ex)
            {
                TempData["Error"] = "Error al actualizar: " + ex.Message;
            }


            return RedirectToAction(nameof(Index));
        }



        // POST Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {

            var item = await _context.ManoObras.FindAsync(id);


            if(item != null)
            {
                _context.ManoObras.Remove(item);

                await _context.SaveChangesAsync();

                TempData["Exito"] = "Asignación eliminada correctamente.";
            }


            return RedirectToAction(nameof(Index));
        }



        // GET: Exportar Excel
        public IActionResult ExportarExcel()
        {
            return View();
        }

    }
}