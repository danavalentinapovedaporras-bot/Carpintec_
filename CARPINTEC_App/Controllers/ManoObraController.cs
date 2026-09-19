using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

        // GET: ManoObra (Listado principal con filtros, métricas y paginación)
        public async Task<IActionResult> Index(string? search, string? estado, int pagina = 1)
        {
            int pageSize = 6;

            // 1. MÉTRICAS DINÁMICAS DESDE LA BASE DE DATOS
            // Empleados trabajando (empleados con labores activas "En proceso")
            var empleadosTrabajando = await _context.ManoObras
                .Where(m => m.Estado == "En proceso" || m.Estado == "En Proceso")
                .Select(m => m.IdEmpleado)
                .Distinct()
                .CountAsync();

            // Si ningún empleado está en proceso, mostramos el total de empleados con labores asignadas
            if (empleadosTrabajando == 0)
            {
                empleadosTrabajando = await _context.ManoObras
                    .Select(m => m.IdEmpleado)
                    .Distinct()
                    .CountAsync();
            }

            // Trabajos pendientes
            var trabajosPendientes = await _context.ManoObras
                .Where(m => m.Estado == "Pendiente")
                .CountAsync();

            // Trabajos finalizados
            var trabajosFinalizados = await _context.ManoObras
                .Where(m => m.Estado == "Finalizado")
                .CountAsync();

            // Costo total de mano de obra calculado desde la base de datos (seguro ante tablas vacías)
            decimal costoTotal = await _context.ManoObras
                .Select(m => (decimal?)((m.HorasReales ?? m.HorasEstimadas) * m.CostoHora))
                .SumAsync() ?? 0m;

            ViewBag.EmpleadosTrabajando = empleadosTrabajando;
            ViewBag.TrabajosPendientes = trabajosPendientes;
            ViewBag.TrabajosFinalizados = trabajosFinalizados;
            ViewBag.CostoTotalFormatted = costoTotal.ToString("C0", CultureInfo.CreateSpecificCulture("es-CO"));

            // 2. CONSULTA PRINCIPAL CON FILTROS (Traducible 100% a SQL)
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
                    (m.IdEmpleadoNavigation != null && (m.IdEmpleadoNavigation.Nombre.Contains(s) || m.IdEmpleadoNavigation.Apellido.Contains(s))) ||
                    (m.Proceso != null && m.Proceso.Contains(s)) ||
                    (m.IdProductoNavigation != null && m.IdProductoNavigation.Nombre.Contains(s)) ||
                    (m.IdPedidoNavigation != null && m.IdPedidoNavigation.CodigoPedido.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                string est = estado.Trim();
                query = query.Where(m => m.Estado == est || m.Estado.ToLower() == est.ToLower());
            }

            // 3. PAGINACIÓN
            int totalRegistros = await query.CountAsync();
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);
            if (totalPaginas == 0) totalPaginas = 1;
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas) pagina = totalPaginas;

            var listaManoObra = await query
                .OrderByDescending(m => m.IdManoObra)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Estado = estado;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistrosPorPagina = pageSize;
            ViewBag.RegistroInicio = totalRegistros == 0 ? 0 : (pagina - 1) * pageSize + 1;
            ViewBag.RegistroFin = Math.Min(pagina * pageSize, totalRegistros);

            // 4. LISTAS PARA SELECTS EN MODALES
            ViewBag.ListaEmpleados = await _context.Empleados
                .Where(e => e.Estado == "Activo")
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            ViewBag.ListaPedidos = await _context.Pedidos
                .OrderByDescending(p => p.IdPedido)
                .Take(50)
                .ToListAsync();

            ViewBag.ListaProductos = await _context.Productos
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(listaManoObra);
        }

        // GET: ManoObra/ObtenerManoObra/5 (Para modales de Detalle y Edición)
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
                return NotFound(new { mensaje = "Registro no encontrado." });
            }

            var horas = item.HorasReales ?? item.HorasEstimadas;
            var costoTotal = horas * item.CostoHora;

            return Json(new
            {
                idManoObra = item.IdManoObra,
                codigoOrden = item.CodigoOrden,
                idPedido = item.IdPedido,
                pedidoCodigo = item.IdPedidoNavigation?.CodigoPedido ?? ("#" + item.IdPedido),
                idProducto = item.IdProducto,
                productoNombre = item.IdProductoNavigation?.Nombre ?? "N/A",
                idEmpleado = item.IdEmpleado,
                empleadoNombre = item.IdEmpleadoNavigation != null ? $"{item.IdEmpleadoNavigation.Nombre} {item.IdEmpleadoNavigation.Apellido}" : "N/A",
                empleadoCargo = item.IdEmpleadoNavigation?.Cargo ?? "N/A",
                proceso = item.Proceso,
                horasEstimadas = item.HorasEstimadas,
                horasReales = item.HorasReales,
                costoHora = item.CostoHora,
                costoTotal = costoTotal.ToString("C0", CultureInfo.CreateSpecificCulture("es-CO")),
                estado = item.Estado,
                fechaInicio = item.FechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = item.FechaFin?.ToString("yyyy-MM-dd"),
                observaciones = item.Observaciones ?? ""
            });
        }

        // POST: ManoObra/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ManoObra manoObra)
        {
            // Removemos referencias de navegación requeridas para que ModelState valide
            ModelState.Remove("IdEmpleadoNavigation");
            ModelState.Remove("IdPedidoNavigation");
            ModelState.Remove("IdProductoNavigation");

            if (ModelState.IsValid)
            {
                try
                {
                    // Si no se asignó código de orden, autogenerar uno único
                    if (string.IsNullOrWhiteSpace(manoObra.CodigoOrden))
                    {
                        int consecutivo = (await _context.ManoObras.MaxAsync(m => (int?)m.IdManoObra) ?? 0) + 1;
                        manoObra.CodigoOrden = $"MO-{DateTime.Now.Year}-{consecutivo + 100}";
                    }
                    else
                    {
                        // Verificar que no esté duplicado
                        bool existeCodigo = await _context.ManoObras.AnyAsync(m => m.CodigoOrden == manoObra.CodigoOrden);
                        if (existeCodigo)
                        {
                            manoObra.CodigoOrden += "-" + DateTime.Now.Millisecond;
                        }
                    }

                    // Asegurar valor válido de Estado según restricción de base de datos
                    if (string.IsNullOrWhiteSpace(manoObra.Estado))
                    {
                        manoObra.Estado = "Pendiente";
                    }

                    _context.ManoObras.Add(manoObra);
                    await _context.SaveChangesAsync();

                    TempData["Exito"] = $"Asignación '{manoObra.CodigoOrden}' creada exitosamente.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error al registrar la asignación: " + ex.Message;
                }
            }
            else
            {
                TempData["Error"] = "Los datos ingresados no son válidos. Por favor verifique el formulario.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: ManoObra/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(ManoObra manoObra)
        {
            ModelState.Remove("IdEmpleadoNavigation");
            ModelState.Remove("IdPedidoNavigation");
            ModelState.Remove("IdProductoNavigation");

            if (ModelState.IsValid)
            {
                try
                {
                    var existente = await _context.ManoObras.FindAsync(manoObra.IdManoObra);
                    if (existente == null)
                    {
                        TempData["Error"] = "El registro a editar no existe.";
                        return RedirectToAction(nameof(Index));
                    }

                    existente.IdPedido = manoObra.IdPedido;
                    existente.IdProducto = manoObra.IdProducto;
                    existente.IdEmpleado = manoObra.IdEmpleado;
                    existente.Proceso = manoObra.Proceso;
                    existente.HorasEstimadas = manoObra.HorasEstimadas;
                    existente.HorasReales = manoObra.HorasReales;
                    existente.CostoHora = manoObra.CostoHora;
                    existente.Estado = manoObra.Estado;
                    existente.FechaInicio = manoObra.FechaInicio;
                    existente.FechaFin = manoObra.FechaFin;
                    existente.Observaciones = manoObra.Observaciones;

                    await _context.SaveChangesAsync();
                    TempData["Exito"] = $"Asignación '{existente.CodigoOrden}' actualizada con éxito.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error al actualizar la asignación: " + ex.Message;
                }
            }
            else
            {
                TempData["Error"] = "Los datos ingresados no son válidos para la actualización.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: ManoObra/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                var item = await _context.ManoObras.FindAsync(id);
                if (item != null)
                {
                    string codigo = item.CodigoOrden;
                    _context.ManoObras.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Exito"] = $"Asignación '{codigo}' eliminada exitosamente.";
                }
                else
                {
                    TempData["Error"] = "La asignación no fue encontrada.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la asignación: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: ManoObra/ExportarExcel
        public async Task<IActionResult> ExportarExcel()
        {
            var registros = await _context.ManoObras
                .Include(m => m.IdEmpleadoNavigation)
                .Include(m => m.IdPedidoNavigation)
                .Include(m => m.IdProductoNavigation)
                .OrderByDescending(m => m.IdManoObra)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var hoja = workbook.Worksheets.Add("Mano de Obra");

                // Encabezado Principal
                hoja.Range("A1:K1").Merge();
                hoja.Cell("A1").Value = "REPORTE DE MANO DE OBRA - CARPINTEC";
                hoja.Cell("A1").Style.Font.Bold = true;
                hoja.Cell("A1").Style.Font.FontSize = 16;
                hoja.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Subtítulo con fecha
                hoja.Range("A2:K2").Merge();
                hoja.Cell("A2").Value = "Generado el: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                hoja.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hoja.Cell("A2").Style.Font.Italic = true;

                // Cabeceras de columna
                string[] headers = {
                    "Código Orden", "Pedido", "Producto", "Empleado", "Cargo",
                    "Actividad / Proceso", "Horas Estimadas", "Horas Reales",
                    "Costo / Hora", "Costo Total", "Estado"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    hoja.Cell(4, i + 1).Value = headers[i];
                }

                var encabezado = hoja.Range("A4:K4");
                encabezado.Style.Font.Bold = true;
                encabezado.Style.Font.FontColor = XLColor.White;
                encabezado.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 42, 34); // Tono caoba / primario Carpintec
                encabezado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                int fila = 5;
                foreach (var item in registros)
                {
                    var horas = item.HorasReales ?? item.HorasEstimadas;
                    var costoTotal = horas * item.CostoHora;

                    hoja.Cell(fila, 1).Value = item.CodigoOrden;
                    hoja.Cell(fila, 2).Value = item.IdPedidoNavigation?.CodigoPedido ?? ("#" + item.IdPedido);
                    hoja.Cell(fila, 3).Value = item.IdProductoNavigation?.Nombre ?? "N/A";
                    hoja.Cell(fila, 4).Value = item.IdEmpleadoNavigation != null ? $"{item.IdEmpleadoNavigation.Nombre} {item.IdEmpleadoNavigation.Apellido}" : "Sin Asignar";
                    hoja.Cell(fila, 5).Value = item.IdEmpleadoNavigation?.Cargo ?? "Operario";
                    hoja.Cell(fila, 6).Value = item.Proceso;
                    hoja.Cell(fila, 7).Value = item.HorasEstimadas;
                    hoja.Cell(fila, 8).Value = item.HorasReales ?? 0m;
                    hoja.Cell(fila, 9).Value = item.CostoHora;
                    hoja.Cell(fila, 9).Style.NumberFormat.Format = "$#,##0.00";
                    hoja.Cell(fila, 10).Value = costoTotal;
                    hoja.Cell(fila, 10).Style.NumberFormat.Format = "$#,##0.00";
                    hoja.Cell(fila, 11).Value = item.Estado;

                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reporte_Mano_De_Obra_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }
    }
}