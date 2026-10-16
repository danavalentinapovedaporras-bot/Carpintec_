using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

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

        public async Task<IActionResult> Index(string? search, string? estado, int pagina = 1)
        {
            try
            {
                int pageSize = 8;

                var query = _context.ManoObras
                    .Include(m => m.IdEmpleadoNavigation)
                    .Include(m => m.IdPedidoNavigation)
                    .Include(m => m.IdProductoNavigation)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string s = search.Trim();
                    query = query.Where(m =>
                        (m.CodigoOrden != null && m.CodigoOrden.Contains(s)) ||
                        (m.Proceso != null && m.Proceso.Contains(s)) ||
                        (m.Observaciones != null && m.Observaciones.Contains(s)) ||
                        (m.IdEmpleadoNavigation != null && (m.IdEmpleadoNavigation.Nombre.Contains(s) || m.IdEmpleadoNavigation.Apellido.Contains(s) || m.IdEmpleadoNavigation.Cargo.Contains(s))) ||
                        (m.IdProductoNavigation != null && m.IdProductoNavigation.Nombre.Contains(s)) ||
                        m.IdPedido.ToString().Contains(s));
                }

                if (!string.IsNullOrWhiteSpace(estado) && estado != "Todos" && estado != "Todos los estados")
                {
                    query = query.Where(m => m.Estado == estado);
                }

                int totalRegistros = await query.CountAsync();
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);
                if (totalPaginas == 0) totalPaginas = 1;
                if (pagina < 1) pagina = 1;
                if (pagina > totalPaginas) pagina = totalPaginas;

                var listaPaginada = await query
                    .OrderByDescending(m => m.IdManoObra)
                    .Skip((pagina - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Cargar listas para los select de modales
                ViewBag.ListaProductos = await _context.Productos.OrderBy(p => p.Nombre).ToListAsync();
                ViewBag.ListaEmpleados = await _context.Empleados.Where(e => e.Estado == "Activo").OrderBy(e => e.Nombre).ToListAsync();
                ViewBag.ListaPedidos = await _context.Pedidos.OrderByDescending(p => p.IdPedido).ToListAsync();

                // Métricas para Bento Cards
                var todas = await _context.ManoObras.ToListAsync();
                var empleadosTrabajando = todas
                    .Where(m => m.Estado == "En proceso" || m.Estado == "En Proceso")
                    .Select(m => m.IdEmpleado)
                    .Distinct()
                    .Count();

                var trabajosPendientes = todas.Count(m => m.Estado == "Pendiente");
                var trabajosFinalizados = todas.Count(m => m.Estado == "Completada" || m.Estado == "Finalizado");
                decimal costoTotal = todas.Sum(m => (m.HorasReales ?? m.HorasEstimadas) * m.CostoHora);

                ViewBag.EmpleadosTrabajando = empleadosTrabajando;
                ViewBag.TrabajosPendientes = trabajosPendientes;
                ViewBag.TrabajosFinalizados = trabajosFinalizados;
                ViewBag.CostoTotalFormatted = "$" + costoTotal.ToString("N2");

                // Paginación y filtros
                ViewBag.Search = search;
                ViewBag.Estado = estado;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
                ViewBag.RegistroInicio = totalRegistros == 0 ? 0 : ((pagina - 1) * pageSize) + 1;
                ViewBag.RegistroFin = Math.Min(pagina * pageSize, totalRegistros);

                return View(listaPaginada);
            }
            catch
            {
                ViewBag.ListaProductos = new List<Producto>();
                ViewBag.ListaEmpleados = new List<Empleado>();
                ViewBag.ListaPedidos = new List<Pedido>();
                ViewBag.Search = "";
                ViewBag.Estado = "";
                ViewBag.PaginaActual = 1;
                ViewBag.TotalPaginas = 1;
                ViewBag.TotalRegistros = 0;
                ViewBag.RegistroInicio = 0;
                ViewBag.RegistroFin = 0;
                ViewBag.EmpleadosTrabajando = 0;
                ViewBag.TrabajosPendientes = 0;
                ViewBag.TrabajosFinalizados = 0;
                ViewBag.CostoTotalFormatted = "$0.00";

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

                if (string.IsNullOrWhiteSpace(manoObra.CodigoOrden))
                {
                    manoObra.CodigoOrden = $"MO-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
                }

                if (manoObra.HorasEstimadas == 0 && manoObra.HorasReales.HasValue)
                {
                    manoObra.HorasEstimadas = manoObra.HorasReales.Value;
                }

                if (ModelState.IsValid)
                {
                    _context.ManoObras.Add(manoObra);
                    await _context.SaveChangesAsync();
                    TempData["Exito"] = "Asignación creada correctamente.";
                }
                else
                {
                    TempData["Error"] = "Datos inválidos para crear la asignación.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET Obtener datos para modal (Detalles y Edición)
        [HttpGet]
        public async Task<IActionResult> ObtenerManoObra(int id)
        {
            var item = await _context.ManoObras
                .Include(m => m.IdEmpleadoNavigation)
                .Include(m => m.IdPedidoNavigation)
                .Include(m => m.IdProductoNavigation)
                .FirstOrDefaultAsync(m => m.IdManoObra == id);

            if (item == null)
            {
                return NotFound(new { mensaje = "Registro no encontrado" });
            }

            var horas = item.HorasReales ?? item.HorasEstimadas;
            var costoTotal = horas * item.CostoHora;

            return Json(new
            {
                idManoObra = item.IdManoObra,
                codigoOrden = item.CodigoOrden,
                idPedido = item.IdPedido,
                idProducto = item.IdProducto,
                idEmpleado = item.IdEmpleado,
                proceso = item.Proceso,
                horasEstimadas = item.HorasEstimadas,
                horasReales = item.HorasReales,
                costoHora = item.CostoHora,
                estado = item.Estado,
                fechaInicio = item.FechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = item.FechaFin?.ToString("yyyy-MM-dd"),
                observaciones = item.Observaciones ?? "",
                empleadoNombre = item.IdEmpleadoNavigation != null ? $"{item.IdEmpleadoNavigation.Nombre} {item.IdEmpleadoNavigation.Apellido}" : $"Empleado #{item.IdEmpleado}",
                empleadoCargo = item.IdEmpleadoNavigation?.Cargo ?? "Operario",
                empleadoDocumento = item.IdEmpleadoNavigation?.Documento ?? "N/A",
                pedidoCodigo = item.IdPedidoNavigation != null ? $"Pedido #{item.IdPedidoNavigation.IdPedido} - {(item.IdPedidoNavigation.Producto ?? "")}" : $"Pedido #{item.IdPedido}",
                productoNombre = item.IdProductoNavigation?.Nombre ?? $"Producto #{item.IdProducto}",
                costoTotal = costoTotal,
                costoTotalFormatted = "$" + costoTotal.ToString("N2")
            });
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

                if (existente == null)
                {
                    TempData["Error"] = "Registro no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrWhiteSpace(manoObra.CodigoOrden))
                {
                    existente.CodigoOrden = manoObra.CodigoOrden;
                }
                existente.IdEmpleado = manoObra.IdEmpleado;
                existente.IdPedido = manoObra.IdPedido;
                existente.IdProducto = manoObra.IdProducto;
                existente.Proceso = manoObra.Proceso;
                existente.HorasEstimadas = manoObra.HorasEstimadas > 0 ? manoObra.HorasEstimadas : (manoObra.HorasReales ?? existente.HorasEstimadas);
                existente.HorasReales = manoObra.HorasReales;
                existente.CostoHora = manoObra.CostoHora;
                existente.Estado = manoObra.Estado;
                existente.FechaInicio = manoObra.FechaInicio;
                existente.FechaFin = manoObra.FechaFin;
                existente.Observaciones = manoObra.Observaciones;

                await _context.SaveChangesAsync();
                TempData["Exito"] = "Asignación actualizada correctamente.";
            }
            catch (Exception ex)
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
            try
            {
                var item = await _context.ManoObras.FindAsync(id);
                if (item != null)
                {
                    _context.ManoObras.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Exito"] = "Asignación eliminada correctamente.";
                }
                else
                {
                    TempData["Error"] = "El registro no existe o ya fue eliminado.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Exportar Excel
        public async Task<IActionResult> ExportarExcel()
        {
            try
            {
                var registros = await _context.ManoObras
                    .Include(m => m.IdEmpleadoNavigation)
                    .Include(m => m.IdPedidoNavigation)
                    .Include(m => m.IdProductoNavigation)
                    .OrderByDescending(m => m.IdManoObra)
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Mano de Obra");

                worksheet.Cell(1, 1).Value = "Código Orden";
                worksheet.Cell(1, 2).Value = "Pedido";
                worksheet.Cell(1, 3).Value = "Empleado";
                worksheet.Cell(1, 4).Value = "Cargo";
                worksheet.Cell(1, 5).Value = "Producto";
                worksheet.Cell(1, 6).Value = "Proceso";
                worksheet.Cell(1, 7).Value = "Horas Estimadas";
                worksheet.Cell(1, 8).Value = "Horas Reales";
                worksheet.Cell(1, 9).Value = "Costo/Hora";
                worksheet.Cell(1, 10).Value = "Costo Total";
                worksheet.Cell(1, 11).Value = "Estado";
                worksheet.Cell(1, 12).Value = "Fecha Inicio";
                worksheet.Cell(1, 13).Value = "Fecha Fin";
                worksheet.Cell(1, 14).Value = "Observaciones";

                var header = worksheet.Range("A1:N1");
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D2B48C");
                header.Style.Font.FontColor = XLColor.Black;

                int row = 2;
                foreach (var item in registros)
                {
                    worksheet.Cell(row, 1).Value = item.CodigoOrden;
                    worksheet.Cell(row, 2).Value = $"Pedido #{item.IdPedido}";
                    worksheet.Cell(row, 3).Value = item.IdEmpleadoNavigation != null ? $"{item.IdEmpleadoNavigation.Nombre} {item.IdEmpleadoNavigation.Apellido}" : $"ID: {item.IdEmpleado}";
                    worksheet.Cell(row, 4).Value = item.IdEmpleadoNavigation?.Cargo ?? "Operario";
                    worksheet.Cell(row, 5).Value = item.IdProductoNavigation?.Nombre ?? $"Producto #{item.IdProducto}";
                    worksheet.Cell(row, 6).Value = item.Proceso;
                    worksheet.Cell(row, 7).Value = item.HorasEstimadas;
                    worksheet.Cell(row, 8).Value = item.HorasReales ?? 0;
                    worksheet.Cell(row, 9).Value = item.CostoHora;
                    var horas = item.HorasReales ?? item.HorasEstimadas;
                    worksheet.Cell(row, 10).Value = horas * item.CostoHora;
                    worksheet.Cell(row, 11).Value = item.Estado;
                    worksheet.Cell(row, 12).Value = item.FechaInicio?.ToString("yyyy-MM-dd") ?? "";
                    worksheet.Cell(row, 13).Value = item.FechaFin?.ToString("yyyy-MM-dd") ?? "";
                    worksheet.Cell(row, 14).Value = item.Observaciones ?? "";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ManoDeObra_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al exportar a Excel: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}