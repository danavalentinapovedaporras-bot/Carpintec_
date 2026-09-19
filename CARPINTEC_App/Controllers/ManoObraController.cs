using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
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

                // Cargar la lista de productos y asignarla a ViewBag para la vista
                var listaProductos = await _context.Productos.ToListAsync(); // Ajustar nombre de DbSet si es distinto
                ViewBag.ListaProductos = listaProductos;

                return View(listaManoObra);
            }
            catch
            {
                // Asegurar que ViewBag no sea null incluso en el caso de excepción
                ViewBag.ListaProductos = new List<object>(); // Ajustar tipo si es necesario
                return View(new List<ManoObra>());
            }
        }
    }
}