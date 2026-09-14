using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class PedidosController : Controller
    {
        private readonly CarpintecContext _context;

        public PedidosController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: PedidosController (Vista principal)

        public async Task<IActionResult> Index(string estado = "Todas", int page = 1)
        {
            int pageSize = 10;

            // --- 1. MÉTRICAS PARA LAS 4 TARJETAS ---
            ViewBag.TotalActivos = await _context.Pedidos.CountAsync();
            ViewBag.EnProduccionCount = await _context.Pedidos.Where(p => p.Estado == "En Producción").CountAsync();
            ViewBag.PendientesEntregaCount = await _context.Pedidos.Where(p => p.Estado == "Pendiente Entrega").CountAsync();

            decimal valorEnCurso = await _context.Pedidos
                .Where(p => p.Estado != "Entregado" && p.Estado != "Cancelado")
                .SumAsync(p => (decimal?)p.ValorTotal) ?? 0;

            ViewBag.ValorEnCursoFormatted = valorEnCurso.ToString("N2");

            // --- NUEVO: CARGAR LA LISTA DE CLIENTES PARA EL SELECTOR DEL MODAL ---
            ViewBag.ListaClientes = await _context.Clientes.ToListAsync();

            // --- 2. CONSULTA DE LA TABLA CON PAGINACIÓN Y FILTROS ---
            var query = _context.Pedidos.Include(p => p.IdClienteNavigation).AsQueryable();

            if (!string.IsNullOrEmpty(estado) && estado != "Todas")
            {
                query = query.Where(p => p.Estado == estado);
            }

            int totalRegistros = await query.CountAsync();
            var registros = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.EstadoActual = estado;
            ViewBag.PageCurrent = page;
            ViewBag.TotalPages = (int)Math.Ceiling(decimal.Divide(totalRegistros, pageSize));

            return View(registros);
        }

        // POST: PedidosController/GuardarPedido (Procesa el formulario del modal directamente)
        [HttpPost]
        public async Task<IActionResult> GuardarPedido(Pedido pedido)
        {
            // 1. Limpiamos las validaciones de navegación
            ModelState.Remove("IdClienteNavigation");
            ModelState.Remove("IdCotizacionNavigation");
            ModelState.Remove("DetallePedidos");
            ModelState.Remove("ManoObras");
            ModelState.Remove("Venta");

            // Si tu base de datos exige un IdCotizacion y no lo estás pidiendo en el form, 
            // le asignamos un valor por defecto que exista en tu tabla Cotizacion (ej: 1)
            if (pedido.IdCotizacion == 0)
            {
                pedido.IdCotizacion = 1;
            }

            pedido.FechaRegistro = DateTime.Now;

            // 2. Comprobamos si el modelo pasa las reglas de validación
            if (ModelState.IsValid)
            {
                _context.Pedidos.Add(pedido);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // 3. SI HAY UN ERROR DE VALIDACIÓN: Imprimimos en la consola de Visual Studio 
            // exactamente qué campo está fallando
            foreach (var state in ModelState.Values)
            {
                foreach (var error in state.Errors)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR DE VALIDACIÓN: " + error.ErrorMessage);
                }
            }

            return RedirectToAction(nameof(Index));
        }
        // GET: PedidosController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: PedidosController/Create
        public ActionResult Create()
        {
            return View();
        }

        // GET: PedidosController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: PedidosController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
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

        // GET: PedidosController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: PedidosController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
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

        public IActionResult ExportarExcel()
        {
            var pedidos = _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                var hoja = workbook.Worksheets.Add("Pedidos");

                // Título
                hoja.Range("A1:F1").Merge();
                hoja.Cell("A1").Value = "REPORTE DE PEDIDOS - CARPINTEC";
                hoja.Cell("A1").Style.Font.Bold = true;
                hoja.Cell("A1").Style.Font.FontSize = 18;
                hoja.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Fecha de exportación
                hoja.Range("A2:F2").Merge();
                hoja.Cell("A2").Value = "Fecha de exportación: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                hoja.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hoja.Cell("A2").Style.Font.Italic = true;

                // Encabezados
                hoja.Cell(4, 1).Value = "Código";
                hoja.Cell(4, 2).Value = "Cliente";
                hoja.Cell(4, 3).Value = "Fecha Solicitud";
                hoja.Cell(4, 4).Value = "Fecha Entrega";
                hoja.Cell(4, 5).Value = "Estado";
                hoja.Cell(4, 6).Value = "Valor Total";

                // Estilo de los encabezados
                var encabezado = hoja.Range("A4:F4");
                encabezado.Style.Font.Bold = true;
                encabezado.Style.Font.FontColor = XLColor.White;
                encabezado.Style.Fill.BackgroundColor = XLColor.DarkGreen;
                encabezado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                int fila = 5;

                foreach (var pedido in pedidos)
                {
                    hoja.Cell(fila, 1).Value = pedido.CodigoPedido;
                    hoja.Cell(fila, 2).Value = (pedido.IdClienteNavigation != null)
                        ? pedido.IdClienteNavigation.Nombre + " " + pedido.IdClienteNavigation.Apellido
                        : "Sin Cliente";
                    hoja.Cell(fila, 3).Value = pedido.FechaSolicitud.ToString("dd/MM/yyyy");
                    hoja.Cell(fila, 4).Value = pedido.FechaEntrega.ToString("dd/MM/yyyy");
                    hoja.Cell(fila, 5).Value = pedido.Estado;
                    hoja.Cell(fila, 6).Value = pedido.ValorTotal;

                    // Formato de moneda
                    hoja.Cell(fila, 6).Style.NumberFormat.Format = "$ #,##0";

                    fila++;
                }

                // Bordes
                var tabla = hoja.Range($"A4:F{fila - 1}");
                tabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tabla.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                hoja.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);

                    return File(
                        stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Pedidos.xlsx");
                }
            }
        }
    }
}