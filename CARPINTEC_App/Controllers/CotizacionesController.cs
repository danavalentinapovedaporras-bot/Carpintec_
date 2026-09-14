using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class CotizacionesController : Controller
    {
        private readonly CarpintecContext _context;

        public CotizacionesController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: Cotizaciones (ÚNICO método Index con Filtro de Estado y Paginación integrados)
        // GET: Cotizaciones (ÚNICO método Index con Filtro de Estado y Paginación integrados)
        public async Task<IActionResult> Index(string estado = "Todas", int page = 1)
        {
            int pageSize = 10;

            // --- CÁLCULOS PARA LAS TARJETAS (Métricas Generales) ---
            ViewBag.TotalCotizaciones = await _context.Cotizaciones.CountAsync();

            ViewBag.PendientesAprobacion = await _context.Cotizaciones.Where(c => c.Estado == "Pendiente").CountAsync();

            // Suma del total de las pendientes (Valuadas en $)
            decimal valorPendientes = await _context.Cotizaciones
                .Where(c => c.Estado == "Pendiente")
                .SumAsync(c => (decimal?)c.Total) ?? 0;

            // Formatear a millones de forma segura
            string valorFormateado;
            if (valorPendientes >= 1000000)
            {
                decimal enMillones = valorPendientes / 1000000.0m;
                valorFormateado = "$" + enMillones.ToString("0.0") + "M";
            }
            else
            {
                valorFormateado = "$" + valorPendientes.ToString("N0");
            }

            ViewBag.ValorPendientesFormatted = valorFormateado;

            // Cálculo de la Tasa de Conversión (Ejemplo: Aprobadas / Total * 100)
            int totalAprobadas = await _context.Cotizaciones.Where(c => c.Estado == "Aprobada").CountAsync();
            int totalGenerales = ViewBag.TotalCotizaciones;

            decimal tasaConversion = totalGenerales > 0
                ? Math.Round(((decimal)totalAprobadas / totalGenerales) * 100, 1)
                : 0;

            ViewBag.TasaConversion = tasaConversion;
            ViewBag.PorcentajeBarraCss = $"{tasaConversion}%"; // Para la barrita de progreso visual

            // --- CONSULTA PARA LA TABLA CON FILTROS Y PAGINACIÓN ---
            var query = _context.Cotizaciones.Include(c => c.IdClienteNavigation).AsQueryable();

            if (!string.IsNullOrEmpty(estado) && estado != "Todas")
            {
                query = query.Where(c => c.Estado == estado);
            }

            int totalRegistros = await query.CountAsync();

            var cotizaciones = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.EstadoActual = estado;
            ViewBag.PageCurrent = page;
            ViewBag.TotalPages = (int)Math.Ceiling(decimal.Divide(totalRegistros, pageSize));
            ViewBag.TotalRegistros = totalRegistros;

            return View(cotizaciones);
        }

        // GET: CotizacionesController/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var cotizacion = await _context.Cotizaciones
                .Include(c => c.IdClienteNavigation)
                .Include(c => c.IdEmpleadoNavigation)
                .FirstOrDefaultAsync(c => c.IdCotizacion == id);

            if (cotizacion == null)
            {
                return NotFound();
            }

            return View(cotizacion);
        }

        // GET: CotizacionesController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: CotizacionesController/GuardarCotizacion (Desde el Modal)
        [HttpPost]
        public async Task<IActionResult> GuardarCotizacion(
             string nombreCliente,
             string correo,
             string telefono,
             string producto,
             string madera,
             int cantidad,
             decimal alto,
             decimal ancho,
             decimal profundidad,
             string tiempoEntrega,
             string observaciones,
             decimal total,
             IFormFile? fotoLugar)
        {
            try
            {
                string? rutaServidor = null;

                if (fotoLugar != null && fotoLugar.Length > 0)
                {
                    string carpetaUploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(carpetaUploads)) Directory.CreateDirectory(carpetaUploads);

                    string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(fotoLugar.FileName);
                    rutaServidor = Path.Combine("uploads", nombreArchivo);
                    string rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await fotoLugar.CopyToAsync(stream);
                    }
                }

                var empleadoDb = await _context.Empleados.FirstOrDefaultAsync();
                int idEmpleadoValido = empleadoDb != null ? empleadoDb.IdEmpleado : 1;

                var nuevaCotizacion = new Cotizacion
                {
                    Folio = "COT-" + DateTime.Now.Year + "-" + new Random().Next(1000, 9999),
                    IdCliente = 1,
                    IdEmpleado = idEmpleadoValido,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    Total = total,
                    Estado = "Pendiente",
                    NombreCliente = nombreCliente,
                    CorreoCliente = correo,
                    TelefonoCliente = telefono,
                    DetalleProducto = producto,
                    TipoMadera = madera,
                    Cantidad = cantidad > 0 ? cantidad : 1,
                    Medidas = $"Alto: {alto}cm, Ancho: {ancho}cm, Prof: {profundidad}cm",
                    TiempoEntrega = tiempoEntrega ?? "15 días hábiles",
                    ObservacionesAdicionales = observaciones,
                    FechaRegistro = DateTime.Now
                };

                _context.Cotizaciones.Add(nuevaCotizacion);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "¡Cotización guardada con todos sus datos ordenados!" });
            }
            catch (Exception ex)
            {
                string mensajeError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error de BD: " + mensajeError });
            }
        }

        // POST: CotizacionesController/Create
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

        // GET: CotizacionesController/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var cotizacion = await _context.Cotizaciones
                .FirstOrDefaultAsync(c => c.IdCotizacion == id);

            if (cotizacion == null)
            {
                return NotFound();
            }

            return View(cotizacion);
        }

        // POST: CotizacionesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cotizacion cotizacion)
        {
            if (id != cotizacion.IdCotizacion)
            {
                return NotFound();
            }

            try
            {
                _context.Entry(cotizacion).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return Content(ex.ToString());
            }
        }

        // GET: CotizacionesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: Cotizaciones/EliminarDirecto/5
        [HttpPost]
        public async Task<IActionResult> EliminarDirecto(int id)
        {
            try
            {
                var cotizacion = await _context.Cotizaciones.FindAsync(id);
                if (cotizacion == null)
                {
                    return Json(new { success = false, message = "La cotización no fue encontrada." });
                }

                // Eliminamos los detalles asociados primero para evitar conflictos de llaves foráneas
                var detalles = _context.DetalleCotizacion.Where(d => d.IdCotizacion == id);
                _context.DetalleCotizacion.RemoveRange(detalles);

                // Eliminamos la cotización principal
                _context.Cotizaciones.Remove(cotizacion);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}