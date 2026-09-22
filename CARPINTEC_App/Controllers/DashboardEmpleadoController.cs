using System;
using System.Threading.Tasks;
using CARPINTEC_App.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize(Roles = "Empleado")]
    [Route("DashboardEmpleado")]
    [Route("Empleado")]
    public class DashboardEmpleadoController : Controller
    {
        private readonly CarpintecContext _context;

        public DashboardEmpleadoController(CarpintecContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            string? nombreUsuario = HttpContext.Session.GetString("NombreUsuario");

            if (idUsuario.HasValue)
            {
                var empleado = await _context.Empleados.FirstOrDefaultAsync(e => e.IdUsuario == idUsuario.Value);
                if (empleado != null)
                {
                    ViewBag.NombreEmpleado = $"{empleado.Nombre} {empleado.Apellido}".Trim();
                    ViewBag.CargoEmpleado = empleado.Cargo;
                    ViewBag.FotoEmpleado = empleado.Foto;
                }
                else
                {
                    ViewBag.NombreEmpleado = !string.IsNullOrWhiteSpace(nombreUsuario) ? nombreUsuario : "Empleado";
                    ViewBag.CargoEmpleado = "Empleado de Taller";
                }
            }
            else
            {
                ViewBag.NombreEmpleado = !string.IsNullOrWhiteSpace(nombreUsuario) ? nombreUsuario : "Empleado";
                ViewBag.CargoEmpleado = "Empleado de Taller";
            }

            // Métricas operativas
            ViewBag.PedidosFabricacion = await _context.Pedidos.CountAsync(p => p.Estado == "En Fabricacion" || p.Estado == "En Proceso");
            ViewBag.PedidosListos = await _context.Pedidos.CountAsync(p => p.Estado == "Listo" || p.Estado == "Terminado");
            ViewBag.TotalCotizaciones = await _context.Cotizaciones.CountAsync();
            ViewBag.TotalProductos = await _context.Productos.CountAsync();
            ViewBag.TotalClientes = await _context.Clientes.CountAsync();
            ViewBag.TotalPedidos = await _context.Pedidos.CountAsync();

            return View();
        }
    }
}