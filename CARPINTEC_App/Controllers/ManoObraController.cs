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

                return View(listaManoObra);
            }
            catch
            {
                return View(new List<ManoObra>());
            }
        }
    }
}