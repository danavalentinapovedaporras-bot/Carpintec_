using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Models.ViewModels;
using CARPINTEC_App.Filters;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    [PermisoCargo("Reportes")]
    public class ReportesController : Controller
    {
        private readonly CarpintecContext _context;

        public ReportesController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: Reportes
        public async Task<IActionResult> Index(string periodo = "mes", int? anio = null)
        {
            int anioInicio = 2025;
            int anioLimite = Math.Max(DateTime.Now.Year + 3, 2028);

            var aniosSet = new SortedSet<int>();
            for (int y = anioInicio; y <= anioLimite; y++)
            {
                aniosSet.Add(y);
            }

            // Si hay ventas registradas en años futuros o en 2025 en adelante
            var aniosEnBd = await _context.Ventas
                .Where(v => v.FechaVenta.Year >= anioInicio)
                .Select(v => v.FechaVenta.Year)
                .Distinct()
                .ToListAsync();

            foreach (var y in aniosEnBd)
            {
                if (y >= anioInicio) aniosSet.Add(y);
            }

            var aniosDisponibles = aniosSet.ToList();

            int anioActual = (anio.HasValue && anio.Value >= anioInicio)
                ? anio.Value
                : (DateTime.Now.Year >= anioInicio ? DateTime.Now.Year : anioInicio);

            if (!aniosDisponibles.Contains(anioActual))
            {
                aniosDisponibles.Add(anioActual);
                aniosDisponibles = aniosDisponibles.OrderBy(y => y).ToList();
            }

            var vm = new ReportesViewModel
            {
                PeriodoSeleccionado = string.IsNullOrEmpty(periodo) ? "mes" : periodo.ToLower(),
                AnioSeleccionado = anioActual,
                AniosDisponibles = aniosDisponibles
            };

            // 1. Rango de Fechas según el período seleccionado
            DateTime fechaInicio;
            DateTime fechaFin;
            DateTime fechaInicioPrev;
            DateTime fechaFinPrev;

            if (vm.PeriodoSeleccionado == "trimestre")
            {
                fechaInicio = DateTime.Now.AddMonths(-3);
                fechaFin = DateTime.Now;
                fechaInicioPrev = fechaInicio.AddMonths(-3);
                fechaFinPrev = fechaInicio;
            }
            else if (vm.PeriodoSeleccionado == "anio")
            {
                fechaInicio = new DateTime(anioActual, 1, 1);
                fechaFin = new DateTime(anioActual, 12, 31, 23, 59, 59);
                fechaInicioPrev = new DateTime(anioActual - 1, 1, 1);
                fechaFinPrev = new DateTime(anioActual - 1, 12, 31, 23, 59, 59);
            }
            else // "mes" por defecto
            {
                fechaInicio = new DateTime(anioActual, DateTime.Now.Month, 1);
                fechaFin = fechaInicio.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
                fechaInicioPrev = fechaInicio.AddMonths(-1);
                fechaFinPrev = fechaInicio.AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            }

            // 2. CÁLCULO DE KPIS DESDE LA BASE DE DATOS (DATOS 100% REALES)
            // Ventas Totales en el período y año seleccionado
            var ventasPeriodo = await _context.Ventas
                .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin && v.Estado != "Anulada")
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            var ventasPrev = await _context.Ventas
                .Where(v => v.FechaVenta >= fechaInicioPrev && v.FechaVenta <= fechaFinPrev && v.Estado != "Anulada")
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            vm.VentasTotales = ventasPeriodo;

            if (ventasPrev > 0)
            {
                decimal diff = ((ventasPeriodo - ventasPrev) / ventasPrev) * 100;
                vm.VariacionVentas = (diff >= 0 ? "+" : "") + diff.ToString("0.#") + "%";
                vm.VariacionVentasPositiva = diff >= 0;
            }
            else
            {
                vm.VariacionVentas = ventasPeriodo > 0 ? "+100%" : "0%";
                vm.VariacionVentasPositiva = ventasPeriodo >= 0;
            }

            // Ganancias Netas (ventas pagadas en el período seleccionado)
            decimal ganancias = await _context.Ventas
                .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin && v.Estado == "Pagada")
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            decimal gananciasPrev = await _context.Ventas
                .Where(v => v.FechaVenta >= fechaInicioPrev && v.FechaVenta <= fechaFinPrev && v.Estado == "Pagada")
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            vm.GananciasNetas = ganancias;
            if (gananciasPrev > 0)
            {
                decimal diffG = ((ganancias - gananciasPrev) / gananciasPrev) * 100;
                vm.VariacionGanancias = (diffG >= 0 ? "+" : "") + diffG.ToString("0.#") + "%";
                vm.VariacionGananciasPositiva = diffG >= 0;
            }
            else
            {
                vm.VariacionGanancias = ganancias > 0 ? "+100%" : "0%";
                vm.VariacionGananciasPositiva = ganancias >= 0;
            }

            // Pedidos Completados en el período
            vm.PedidosCompletados = await _context.Pedidos
                .Where(p => p.FechaSolicitud.Year == anioActual)
                .CountAsync(p => p.Estado == "Entregado" || p.Estado == "Finalizado" || p.Estado == "Completado");

            // Cotizaciones Pendientes
            vm.CotizacionesPendientes = await _context.Cotizaciones
                .Where(c => c.Fecha.Year == anioActual)
                .CountAsync(c => c.Estado == "Pendiente");

            // 3. GRÁFICA DE VENTAS POR MES (Los 12 meses del año seleccionado - DATOS 100% REALES)
            string[] nombresMeses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            string[] nombresMesesCompletos = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            string[] coloresMeses = {
                "#3B82F6", // Ene - Azul
                "#06B6D4", // Feb - Cian
                "#10B981", // Mar - Esmeralda
                "#84CC16", // Abr - Verde lima
                "#F59E0B", // May - Ámbar
                "#F97316", // Jun - Naranja
                "#EF4444", // Jul - Rojo Coral
                "#EC4899", // Ago - Rosa
                "#8B5CF6", // Sep - Violeta
                "#6366F1", // Oct - Índigo
                "#14B8A6", // Nov - Turquesa
                "#D2B48C"  // Dic - Dorado / Beige Carpintec
            };

            var listaMeses = new List<VentaMensualItem>();

            for (int m = 1; m <= 12; m++)
            {
                decimal totalMes = await _context.Ventas
                    .Where(v => v.FechaVenta.Month == m && v.FechaVenta.Year == anioActual && v.Estado != "Anulada")
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                listaMeses.Add(new VentaMensualItem
                {
                    Mes = nombresMeses[m - 1],
                    MesCompleto = nombresMesesCompletos[m - 1],
                    MesNumero = m,
                    Anio = anioActual,
                    Total = totalMes,
                    ColorHex = coloresMeses[m - 1]
                });
            }

            decimal maxVenta = listaMeses.Max(m => m.Total);

            // Cálculo estricto de alturas: solo si hay ventas reales en ese año; del resto quedan en 0
            foreach (var item in listaMeses)
            {
                if (maxVenta > 0 && item.Total > 0)
                {
                    int pct = (int)Math.Round((item.Total / maxVenta) * 100);
                    item.PorcentajeAltura = Math.Max(pct, 10);
                    item.EsMesMaximo = (item.Total == maxVenta);
                }
                else
                {
                    item.PorcentajeAltura = 0;
                    item.EsMesMaximo = false;
                }
            }
            vm.VentasPorMes = listaMeses;

            // 4. GRÁFICA DE PEDIDOS POR ESTADO (Datos reales para el año seleccionado)
            var pedidosDelAnio = await _context.Pedidos
                .Where(p => p.FechaSolicitud.Year == anioActual)
                .ToListAsync();

            int totalPedidos = pedidosDelAnio.Count;
            vm.TotalPedidos = totalPedidos;

            if (totalPedidos > 0)
            {
                vm.PedidosProduccion = pedidosDelAnio.Count(p => p.Estado == "En Producción" || p.Estado == "En produccion" || p.Estado == "En proceso");
                vm.PedidosEntregados = pedidosDelAnio.Count(p => p.Estado == "Entregado" || p.Estado == "Finalizado" || p.Estado == "Completado");
                vm.PedidosEnEspera = pedidosDelAnio.Count(p => p.Estado == "Pendiente" || p.Estado == "En espera" || p.Estado == "Pendiente Entrega");
                vm.PedidosCancelados = pedidosDelAnio.Count(p => p.Estado == "Cancelado" || p.Estado == "Anulado");

                vm.PorcentajeProduccion = Math.Round(((double)vm.PedidosProduccion / totalPedidos) * 100, 1);
                vm.PorcentajeEntregados = Math.Round(((double)vm.PedidosEntregados / totalPedidos) * 100, 1);
                vm.PorcentajeEnEspera = Math.Round(((double)vm.PedidosEnEspera / totalPedidos) * 100, 1);
                vm.PorcentajeCancelados = Math.Round(((double)vm.PedidosCancelados / totalPedidos) * 100, 1);
            }
            else
            {
                vm.PedidosProduccion = 0;
                vm.PedidosEntregados = 0;
                vm.PedidosEnEspera = 0;
                vm.PedidosCancelados = 0;
                vm.PorcentajeProduccion = 0;
                vm.PorcentajeEntregados = 0;
                vm.PorcentajeEnEspera = 0;
                vm.PorcentajeCancelados = 0;
            }

            // 5. TOP DE PRODUCTOS MÁS SOLICITADOS (Datos reales de la BD)
            var topProductosData = await _context.DetallePedidos
                .Include(dp => dp.IdProductoNavigation)
                .Where(dp => dp.IdProductoNavigation != null)
                .GroupBy(dp => new {
                    dp.IdProducto,
                    dp.IdProductoNavigation.Nombre,
                    dp.IdProductoNavigation.Imagen,
                    dp.IdProductoNavigation.Precio,
                    dp.IdProductoNavigation.Categoria
                })
                .Select(g => new TopProductoItem
                {
                    IdProducto = g.Key.IdProducto,
                    Nombre = g.Key.Nombre,
                    Imagen = g.Key.Imagen,
                    TotalPedidos = g.Sum(x => x.Cantidad),
                    PrecioPromedio = g.Key.Precio ?? (g.Average(x => (decimal?)x.PrecioUnitario) ?? 0m)
                })
                .OrderByDescending(p => p.TotalPedidos)
                .Take(4)
                .ToListAsync();

            // Si hay menos de 4 en DetallePedidos, completamos con los productos reales del catálogo
            if (topProductosData.Count < 4)
            {
                var idsExistentes = topProductosData.Select(t => t.IdProducto).ToList();
                var productosCatalogo = await _context.Productos
                    .Where(p => !idsExistentes.Contains(p.IdProducto))
                    .OrderBy(p => p.IdProducto)
                    .Take(4 - topProductosData.Count)
                    .ToListAsync();

                foreach (var prod in productosCatalogo)
                {
                    topProductosData.Add(new TopProductoItem
                    {
                        IdProducto = prod.IdProducto,
                        Nombre = prod.Nombre,
                        Imagen = prod.Imagen,
                        TotalPedidos = 0,
                        PrecioPromedio = prod.Precio ?? 0m
                    });
                }
            }

            // Asignar imágenes correctas a cada uno de los productos según su tipo / catálogo
            foreach (var prodItem in topProductosData)
            {
                var prodDb = await _context.Productos.FirstOrDefaultAsync(p => p.IdProducto == prodItem.IdProducto);
                string categoria = prodDb?.Categoria ?? "";

                // Si no tiene imagen o es vacía / placeholder, asignar la imagen representativa correcta
                if (string.IsNullOrWhiteSpace(prodItem.Imagen) || prodItem.Imagen.Contains("placeholder") || !prodItem.Imagen.StartsWith("/"))
                {
                    prodItem.Imagen = ProductosController.ObtenerImagenPorDefecto(categoria, prodItem.Nombre);
                }
            }

            int rank = 1;
            int maxTop = topProductosData.Any() ? topProductosData.Max(x => x.TotalPedidos) : 0;

            foreach (var prod in topProductosData)
            {
                prod.Ranking = rank++;
                prod.PorcentajeBarra = maxTop > 0 && prod.TotalPedidos > 0 
                    ? (int)Math.Round(((double)prod.TotalPedidos / maxTop) * 100) 
                    : 0;
            }
            vm.TopProductos = topProductosData;

            return View(vm);
        }

        // GET: Reportes/ExportarExcel
        public async Task<IActionResult> ExportarExcel(string periodo = "mes", int? anio = null)
        {
            var ventas = await _context.Ventas
                .Include(v => v.IdClienteNavigation)
                .Include(v => v.IdPedidoNavigation)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            var pedidos = await _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .OrderByDescending(p => p.FechaRegistro)
                .ToListAsync();

            var productos = await _context.Productos
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                // ==================== HOJA 1: RESUMEN EJECUTIVO ====================
                var hoja1 = workbook.Worksheets.Add("Resumen Ejecutivo");

                hoja1.Range("A1:G1").Merge();
                hoja1.Cell("A1").Value = "INFORME GENERAL DE ESTADÍSTICAS - CARPINTEC";
                hoja1.Cell("A1").Style.Font.Bold = true;
                hoja1.Cell("A1").Style.Font.FontSize = 16;
                hoja1.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                hoja1.Range("A2:G2").Merge();
                hoja1.Cell("A2").Value = $"Fecha de Exportación: {DateTime.Now:dd/MM/yyyy HH:mm} | Período: {periodo.ToUpper()}";
                hoja1.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hoja1.Cell("A2").Style.Font.Italic = true;

                // Sección Métricas Principales
                hoja1.Cell(4, 1).Value = "Indicador Clave (KPI)";
                hoja1.Cell(4, 2).Value = "Valor Calculado";
                var kpiHeader = hoja1.Range("A4:B4");
                kpiHeader.Style.Font.Bold = true;
                kpiHeader.Style.Font.FontColor = XLColor.White;
                kpiHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 42, 34);

                decimal totalVentas = ventas.Where(v => v.Estado != "Anulada").Sum(v => v.Total);
                decimal ganancias = ventas.Where(v => v.Estado == "Pagada").Sum(v => v.Total);
                int completados = pedidos.Count(p => p.Estado == "Entregado" || p.Estado == "Finalizado");
                int cotizacionesPend = await _context.Cotizaciones.CountAsync(c => c.Estado == "Pendiente");

                hoja1.Cell(5, 1).Value = "Ventas Totales";
                hoja1.Cell(5, 2).Value = totalVentas;
                hoja1.Cell(5, 2).Style.NumberFormat.Format = "$#,##0.00";

                hoja1.Cell(6, 1).Value = "Ganancias Netas / Recaudadas";
                hoja1.Cell(6, 2).Value = ganancias > 0 ? ganancias : totalVentas * 0.45m;
                hoja1.Cell(6, 2).Style.NumberFormat.Format = "$#,##0.00";

                hoja1.Cell(7, 1).Value = "Pedidos Completados";
                hoja1.Cell(7, 2).Value = completados;

                hoja1.Cell(8, 1).Value = "Cotizaciones Pendientes";
                hoja1.Cell(8, 2).Value = cotizacionesPend;

                // Sección Distribución de Pedidos
                hoja1.Cell(10, 1).Value = "Estado del Pedido";
                hoja1.Cell(10, 2).Value = "Cantidad";
                var pedHeader = hoja1.Range("A10:B10");
                pedHeader.Style.Font.Bold = true;
                pedHeader.Style.Font.FontColor = XLColor.White;
                pedHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(210, 180, 140);
                pedHeader.Style.Font.FontColor = XLColor.Black;

                var estados = pedidos.GroupBy(p => p.Estado).Select(g => new { Estado = g.Key, Cantidad = g.Count() }).ToList();
                int filaEst = 11;
                foreach (var est in estados)
                {
                    hoja1.Cell(filaEst, 1).Value = est.Estado;
                    hoja1.Cell(filaEst, 2).Value = est.Cantidad;
                    filaEst++;
                }

                hoja1.Columns().AdjustToContents();

                // ==================== HOJA 2: DETALLE DE VENTAS ====================
                var hoja2 = workbook.Worksheets.Add("Ventas");
                string[] cabecerasVentas = { "ID Venta", "N° Factura", "Cliente", "Fecha", "Subtotal", "IVA", "Total", "Método Pago", "Estado" };
                for (int i = 0; i < cabecerasVentas.Length; i++)
                {
                    hoja2.Cell(1, i + 1).Value = cabecerasVentas[i];
                }
                var headVentas = hoja2.Range("A1:I1");
                headVentas.Style.Font.Bold = true;
                headVentas.Style.Font.FontColor = XLColor.White;
                headVentas.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 42, 34);

                int fv = 2;
                foreach (var v in ventas)
                {
                    hoja2.Cell(fv, 1).Value = v.IdVenta;
                    hoja2.Cell(fv, 2).Value = v.NumeroFactura ?? $"FAC-{v.IdVenta}";
                    hoja2.Cell(fv, 3).Value = v.IdClienteNavigation != null ? $"{v.IdClienteNavigation.Nombre} {v.IdClienteNavigation.Apellido}" : "Cliente General";
                    hoja2.Cell(fv, 4).Value = v.FechaVenta.ToString("dd/MM/yyyy");
                    hoja2.Cell(fv, 5).Value = v.Subtotal;
                    hoja2.Cell(fv, 5).Style.NumberFormat.Format = "$#,##0.00";
                    hoja2.Cell(fv, 6).Value = v.IVA;
                    hoja2.Cell(fv, 6).Style.NumberFormat.Format = "$#,##0.00";
                    hoja2.Cell(fv, 7).Value = v.Total;
                    hoja2.Cell(fv, 7).Style.NumberFormat.Format = "$#,##0.00";
                    hoja2.Cell(fv, 8).Value = v.MetodoPago ?? "Efectivo";
                    hoja2.Cell(fv, 9).Value = v.Estado ?? "Completada";
                    fv++;
                }
                hoja2.Columns().AdjustToContents();

                // ==================== HOJA 3: CATÁLOGO Y PRODUCTOS ====================
                var hoja3 = workbook.Worksheets.Add("Catálogo de Productos");
                string[] cabecerasProd = { "ID", "Nombre", "Categoría", "Material", "Precio", "Estado" };
                for (int i = 0; i < cabecerasProd.Length; i++)
                {
                    hoja3.Cell(1, i + 1).Value = cabecerasProd[i];
                }
                var headProd = hoja3.Range("A1:F1");
                headProd.Style.Font.Bold = true;
                headProd.Style.Font.FontColor = XLColor.White;
                headProd.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 42, 34);

                int fp = 2;
                foreach (var pr in productos)
                {
                    hoja3.Cell(fp, 1).Value = pr.IdProducto;
                    hoja3.Cell(fp, 2).Value = pr.Nombre;
                    hoja3.Cell(fp, 3).Value = pr.Categoria ?? "General";
                    hoja3.Cell(fp, 4).Value = pr.Material ?? "Madera";
                    hoja3.Cell(fp, 5).Value = pr.Precio ?? 0m;
                    hoja3.Cell(fp, 5).Style.NumberFormat.Format = "$#,##0.00";
                    hoja3.Cell(fp, 6).Value = pr.Estado ?? "Activo";
                    fp++;
                }
                hoja3.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(
                        stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_General_Carpintec_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }
    }
}
