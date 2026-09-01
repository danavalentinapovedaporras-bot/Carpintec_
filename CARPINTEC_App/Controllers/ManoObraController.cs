using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CARPINTEC_App.Data;
using CARPINTEC_App.Models;

namespace CARPINTEC_App.Controllers
{
    public class ManoObraController : Controller
    {
        private readonly CarpintecContext _context;

        public ManoObraController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: ManoObra
        public async Task<IActionResult> Index()
        {
            var listaAsignaciones = await _context.ManoObra
                .Include(m => m.IdEmpleadoNavigation)
                .Include(m => m.IdPedidoNavigation)
                .Include(m => m.IdProductoNavigation)
                .ToListAsync();

            // Usamos plurales (Pedidos, Empleados, Productos) tal como los maneja Entity Framework
            ViewBag.ListaPedidos = await _context.Pedidos.ToListAsync();
            ViewBag.ListaEmpleados = await _context.Empleados.ToListAsync();
            ViewBag.ListaProductos = await _context.Productos.ToListAsync();

            return View(listaAsignaciones);
        }

        // POST: ManoObra/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ManoObra modelo)
        {
            if (string.IsNullOrEmpty(modelo.CodigoOrden))
            {
                modelo.CodigoOrden = "MO-" + new Random().Next(1000, 9999);
            }

            // Manejo seguro del campo nullable para evitar conflictos de tipos
            if (modelo.HorasReales.HasValue)
            {
                modelo.HorasEstimadas = modelo.HorasReales.Value;
            }

            _context.Add(modelo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}