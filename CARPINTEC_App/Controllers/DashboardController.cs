using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class DashboardController : Controller
    {
        private readonly CarpintecContext _context;

        public DashboardController(CarpintecContext context)
        {
            _context = context;
        }


        // GET: Dashboard
        public async Task<IActionResult> Index()
        {
            var dashboard = new DashboardViewModel();


            // Total usuarios
            dashboard.TotalUsuarios =
                await _context.Usuarios.CountAsync();


            // Total empleados
            dashboard.TotalEmpleados =
                await _context.Empleados.CountAsync();


            // Total clientes
            dashboard.TotalClientes =
                await _context.Clientes.CountAsync();


            // Total productos
            dashboard.TotalProductos =
                await _context.Productos.CountAsync();


            // Cotizaciones pendientes
            dashboard.CotizacionesPendientes =
                await _context.Cotizaciones
                .CountAsync(c => c.Estado == "Pendiente");


            // Pedidos activos
            dashboard.PedidosActivos =
                await _context.Pedidos
                .CountAsync(p => p.Estado != "Finalizado");


            // Ventas del mes
            dashboard.VentasMes =
                await _context.Ventas
                .Where(v => v.FechaVenta.Month == DateTime.Now.Month)
                .SumAsync(v => v.Total);



            // Últimos pedidos
            dashboard.UltimosPedidos =
                await _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .OrderByDescending(p => p.FechaRegistro)
                .Take(5)
                .ToListAsync();

            // Proyectos en Producción desde Base de Datos

            dashboard.ProyectosProduccion =
                await _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .Where(p => p.Estado != "Finalizado")
                .OrderByDescending(p => p.FechaRegistro)
                .Take(5)
                .ToListAsync();

            // Actividad reciente del Dashboard desde Base de Datos

            dashboard.ActividadesRecientes = new List<ActividadRecienteViewModel>();


            // PEDIDOS RECIENTES
            var pedidosRecientes = await _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .OrderByDescending(p => p.FechaRegistro)
                .Take(3)
                .ToListAsync();


            foreach (var pedido in pedidosRecientes)
            {
                dashboard.ActividadesRecientes.Add(new ActividadRecienteViewModel
                {
                    Titulo = "Nuevo pedido",
                    Descripcion = $"Pedido {pedido.CodigoPedido} - Cliente: {pedido.IdClienteNavigation.Nombre}",
                    Fecha = pedido.FechaRegistro.HasValue
                        ? pedido.FechaRegistro.Value.ToString("dd/MM/yyyy HH:mm")
                        : pedido.FechaSolicitud.ToString("dd/MM/yyyy"),
                    Icono = "shopping_cart"
                });
            }



            // COTIZACIONES RECIENTES
            var cotizacionesRecientes = await _context.Cotizaciones
                .OrderByDescending(c => c.FechaRegistro)
                .Take(2)
                .ToListAsync();


            foreach (var cotizacion in cotizacionesRecientes)
            {
                dashboard.ActividadesRecientes.Add(new ActividadRecienteViewModel
                {
                    Titulo = "Nueva cotización",
                    Descripcion = $"Cotización {cotizacion.Folio} - Estado: {cotizacion.Estado}",
                    Fecha = cotizacion.FechaRegistro.HasValue
                        ? cotizacion.FechaRegistro.Value.ToString("dd/MM/yyyy HH:mm")
                        : cotizacion.Fecha.ToString("dd/MM/yyyy"),
                    Icono = "request_quote"
                });
            }


            // Mostrar máximo 5 actividades
            dashboard.ActividadesRecientes =
                dashboard.ActividadesRecientes
                .Take(5)
                .ToList();


            return View(dashboard);
        }



        public IActionResult Calendario()
        {
            return View();
        }



        public IActionResult Details(int id)
        {
            return View();
        }



        public IActionResult Create()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(IFormCollection collection)
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



        public IActionResult Edit(int id)
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, IFormCollection collection)
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



        public IActionResult Delete(int id)
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id, IFormCollection collection)
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