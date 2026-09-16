using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Services
{
    /* INICIO: Servicio Inteligente de Chatbot conectado a la Base de Datos */
    public class ChatbotService
    {
        private readonly CarpintecContext _context;

        public ChatbotService(CarpintecContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Procesa la pregunta del usuario en lenguaje natural y genera una respuesta basada en datos de la base de datos.
        /// </summary>
        public async Task<string> ResponderAsync(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                return "👋 ¡Hola! ¿En qué puedo ayudarte hoy? Puedes consultarme sobre productos, stock, pedidos, cotizaciones, clientes, facturación o PQR.";
            }

            string normalizado = NormalizarTexto(mensaje);

            // 1. Saludos y bienvenida
            if (EsSaludo(normalizado))
            {
                return GenerarSaludo();
            }

            // 2. Ayuda y capacidades
            if (EsAyuda(normalizado))
            {
                return GenerarMenuAyuda();
            }

            // 3. Detección de códigos específicos (PED-, FAC-, PQR-, etc.)
            string respuestaCodigo = await BuscarPorCodigoAsync(mensaje, normalizado);
            if (!string.IsNullOrEmpty(respuestaCodigo))
            {
                return respuestaCodigo;
            }

            // 4. Resumen general / Balance / Estadísticas de la empresa
            if (EsResumenGeneral(normalizado))
            {
                return await ConsultarResumenGeneralAsync();
            }

            // 5. Inventario y Stock
            if (EsConsultaInventario(normalizado))
            {
                return await ConsultarInventarioAsync(normalizado);
            }

            // 6. Pedidos
            if (EsConsultaPedidos(normalizado))
            {
                return await ConsultarPedidosAsync(normalizado);
            }

            // 7. Facturas y Ventas
            if (EsConsultaFacturas(normalizado))
            {
                return await ConsultarFacturasAsync(normalizado);
            }

            // 8. Cotizaciones
            if (EsConsultaCotizaciones(normalizado))
            {
                return await ConsultarCotizacionesAsync(normalizado);
            }

            // 9. PQR (Peticiones, Quejas, Reclamos)
            if (EsConsultaPQR(normalizado))
            {
                return await ConsultarPQRAsync(normalizado);
            }

            // 10. Clientes
            if (EsConsultaClientes(normalizado))
            {
                return await ConsultarClientesAsync(normalizado);
            }

            // 11. Empleados y Mano de Obra
            if (EsConsultaEmpleados(normalizado))
            {
                return await ConsultarEmpleadosAsync(normalizado);
            }

            // 12. Productos y Catálogo
            if (EsConsultaProductos(normalizado))
            {
                return await ConsultarProductosAsync(normalizado);
            }

            // 13. Fallback inteligente: buscar coincidencia directa en nombres de productos o clientes
            string busquedaDirecta = await BuscarCoincidenciaDirectaAsync(mensaje, normalizado);
            if (!string.IsNullOrEmpty(busquedaDirecta))
            {
                return busquedaDirecta;
            }

            // 14. Respuesta por defecto con sugerencias
            return GenerarRespuestaPorDefecto(mensaje);
        }

        #region Normalización de Texto

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            string descompuesto = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in descompuesto)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string limpio = sb.ToString().Normalize(NormalizationForm.FormC).ToLower();
            limpio = Regex.Replace(limpio, @"[¿?¡!.,;:()\-_/]", " ");
            return Regex.Replace(limpio, @"\s+", " ").Trim();
        }

        private static bool ContienePalabra(string texto, params string[] palabras)
        {
            return palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Clasificadores de Intención

        private static bool EsSaludo(string t)
        {
            return ContienePalabra(t, "hola", "buenos dias", "buenas tardes", "buenas noches", "que tal", "saludos", "buenas");
        }

        private static bool EsAyuda(string t)
        {
            return ContienePalabra(t, "ayuda", "que puedes hacer", "que sabes hacer", "opciones", "comandos", "menu", "como funciona", "que puedo preguntar");
        }

        private static bool EsResumenGeneral(string t)
        {
            return ContienePalabra(t, "resumen", "balance", "estadistica", "estadisticas", "metricas", "como va el negocio", "reporte general", "dashboard", "kpi", "resumen ejecutivo", "estado del negocio");
        }

        private static bool EsConsultaInventario(string t)
        {
            return ContienePalabra(t, "inventario", "stock", "existencia", "existencias", "material", "materiales", "madera", "herraje", "agotado", "agotados", "bodega", "quedan", "queda");
        }

        private static bool EsConsultaPedidos(string t)
        {
            return ContienePalabra(t, "pedido", "pedidos", "orden", "ordenes", "solicitud de entrega");
        }

        private static bool EsConsultaFacturas(string t)
        {
            return ContienePalabra(t, "factura", "facturas", "facturacion", "venta", "ventas", "cobro", "cobros", "ingreso", "ingresos", "ganancia", "ganancias", "dinero", "recaudo");
        }

        private static bool EsConsultaCotizaciones(string t)
        {
            return ContienePalabra(t, "cotizacion", "cotizaciones", "cotizar", "presupuesto", "presupuestos");
        }

        private static bool EsConsultaPQR(string t)
        {
            return ContienePalabra(t, "pqr", "pqrs", "queja", "quejas", "reclamo", "reclamos", "peticion", "peticiones", "sugerencia", "sugerencias", "inconformidad");
        }

        private static bool EsConsultaClientes(string t)
        {
            return ContienePalabra(t, "cliente", "clientes", "empresa", "empresas", "comprador", "compradores");
        }

        private static bool EsConsultaEmpleados(string t)
        {
            return ContienePalabra(t, "empleado", "empleados", "trabajador", "trabajadores", "personal", "mano de obra", "ebanista", "ebanistas", "carpintero", "carpinteros", "operario");
        }

        private static bool EsConsultaProductos(string t)
        {
            return ContienePalabra(t, "producto", "productos", "catalogo", "mueble", "muebles", "precio", "precios", "cuanto cuesta", "cuanto vale", "cocina", "closet", "mesa", "silla", "escritorio", "puerta");
        }

        #endregion

        #region Consultas a la Base de Datos

        // ==========================================
        // 1. Búsqueda directa por códigos (PED-, FAC-, PQR-, etc.)
        // ==========================================
        private async Task<string> BuscarPorCodigoAsync(string original, string norm)
        {
            // Búsqueda de código PED-XXXX
            var matchPedido = Regex.Match(original, @"(ped|PED)[-_]?\d+", RegexOptions.IgnoreCase);
            if (matchPedido.Success)
            {
                string codigo = matchPedido.Value;
                var pedido = await _context.Pedidos
                    .Include(p => p.IdClienteNavigation)
                    .FirstOrDefaultAsync(p => p.CodigoPedido.Contains(codigo) || p.IdPedido.ToString() == codigo);

                if (pedido != null)
                {
                    string cliente = pedido.IdClienteNavigation != null ? $"{pedido.IdClienteNavigation.Nombre} {pedido.IdClienteNavigation.Apellido}".Trim() : "No asignado";
                    return $"📦 **Información del Pedido #{pedido.CodigoPedido}**\n\n" +
                           $"• **Cliente:** {cliente}\n" +
                           $"• **Estado:** {pedido.Estado}\n" +
                           $"• **Valor Total:** ${pedido.ValorTotal:N0} COP\n" +
                           $"• **Fecha de Entrega:** {pedido.FechaEntrega:dd/MM/yyyy}\n" +
                           $"• **Observaciones:** {(string.IsNullOrWhiteSpace(pedido.Observaciones) ? "Sin observaciones" : pedido.Observaciones)}";
                }
            }

            // Búsqueda de código FAC-XXXX
            var matchFactura = Regex.Match(original, @"(fac|FAC)[-_]?\d+", RegexOptions.IgnoreCase);
            if (matchFactura.Success)
            {
                string folio = matchFactura.Value;
                var factura = await _context.Facturas
                    .FirstOrDefaultAsync(f => f.Folio.Contains(folio));

                if (factura != null)
                {
                    return $"💰 **Información de la Factura #{factura.Folio}**\n\n" +
                           $"• **Cliente:** {factura.Cliente}\n" +
                           $"• **Total:** ${factura.Total:N0} COP\n" +
                           $"• **Estado:** {factura.Estado}\n" +
                           $"• **Fecha Emisión:** {factura.Fecha:dd/MM/yyyy}\n" +
                           $"• **Método de Pago:** {factura.MetodoPago ?? "No especificado"}";
                }
            }

            // Búsqueda de código PQR-XXXX
            var matchPqr = Regex.Match(original, @"(pqr|PQR)[-_]?\d+", RegexOptions.IgnoreCase);
            if (matchPqr.Success)
            {
                string radicado = matchPqr.Value;
                var pqr = await _context.Pqrs
                    .Include(p => p.IdClienteNavigation)
                    .FirstOrDefaultAsync(p => p.CodigoPqr.Contains(radicado));

                if (pqr != null)
                {
                    string cliente = pqr.IdClienteNavigation != null ? $"{pqr.IdClienteNavigation.Nombre} {pqr.IdClienteNavigation.Apellido}".Trim() : "No asignado";
                    return $"📩 **Radicado de PQR #{pqr.CodigoPqr}**\n\n" +
                           $"• **Tipo:** {pqr.Tipo}\n" +
                           $"• **Cliente:** {cliente}\n" +
                           $"• **Estado:** {pqr.Estado}\n" +
                           $"• **Asunto:** {pqr.Asunto}\n" +
                           $"• **Fecha:** {pqr.FechaRegistro:dd/MM/yyyy}\n" +
                           $"• **Respuesta:** {(string.IsNullOrWhiteSpace(pqr.Respuesta) ? "Pendiente de respuesta" : pqr.Respuesta)}";
                }
            }

            return string.Empty;
        }

        // ==========================================
        // 2. Resumen General / Estadísticas
        // ==========================================
        private async Task<string> ConsultarResumenGeneralAsync()
        {
            int totalProductos = await _context.Productos.CountAsync();
            int productosActivos = await _context.Productos.CountAsync(p => p.Estado == "Activo");
            int totalClientes = await _context.Clientes.CountAsync();
            int totalPedidos = await _context.Pedidos.CountAsync();
            int pedidosPendientes = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente" || p.Estado == "En proceso");
            int stockBajo = await _context.Inventarios.CountAsync(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15);
            decimal totalFacturado = await _context.Facturas.SumAsync(f => (decimal?)f.Total) ?? 0;
            int pqrPendientes = await _context.Pqrs.CountAsync(p => p.Estado == "Pendiente" || p.Estado == "En proceso");

            return $"📊 **Resumen Ejecutivo de CARPINTEC**\n\n" +
                   $"• 🪑 **Catálogo:** {productosActivos} productos activos de {totalProductos} registrados.\n" +
                   $"• 👥 **Clientes:** {totalClientes} clientes en el sistema.\n" +
                   $"• 📦 **Pedidos:** {totalPedidos} registrados ({pedidosPendientes} en curso/pendientes).\n" +
                   $"• ⚠️ **Inventario:** {stockBajo} materiales con stock bajo o crítico.\n" +
                   $"• 💰 **Facturación Total:** ${totalFacturado:N0} COP.\n" +
                   $"• 📩 **PQR:** {pqrPendientes} radicados pendientes por atender.\n\n" +
                   $"💡 *Puedes pedirme detalles sobre cualquiera de estas áreas.*";
        }

        // ==========================================
        // 3. Inventario y Stock
        // ==========================================
        private async Task<string> ConsultarInventarioAsync(string norm)
        {
            // A. Consulta de stock bajo / crítico
            if (ContienePalabra(norm, "bajo", "critico", "poco", "atencion", "falta", "urgente"))
            {
                var bajos = await _context.Inventarios
                    .Where(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15)
                    .OrderBy(i => i.StockActual)
                    .Take(8)
                    .ToListAsync();

                if (!bajos.Any())
                {
                    return "✅ ¡Buenas noticias! No hay materiales con stock crítico actualmente en la base de datos. Todos superan los niveles mínimos de inventario.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"⚠️ **Materiales con Stock Bajo ({bajos.Count} encontrados):**\n");
                foreach (var item in bajos)
                {
                    sb.AppendLine($"• **{item.NombreProducto}:** Quedan {item.StockActual} {item.UnidadMedida} (Mínimo: {item.StockMinimo})");
                }
                sb.AppendLine("\n💡 *Te recomiendo coordinar con compras o generar una solicitud de reposición.*");
                return sb.ToString();
            }

            // B. Materiales agotados (stock == 0)
            if (ContienePalabra(norm, "agotado", "agotados", "cero", "sin stock"))
            {
                var agotados = await _context.Inventarios
                    .Where(i => i.StockActual == 0)
                    .Take(8)
                    .ToListAsync();

                if (!agotados.Any())
                {
                    return "✅ En este momento no hay ningún material completamente agotado (stock en 0).";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🔴 **Materiales Agotados ({agotados.Count}):**\n");
                foreach (var item in agotados)
                {
                    sb.AppendLine($"• **{item.NombreProducto}:** 0 {item.UnidadMedida} disponibles.");
                }
                return sb.ToString();
            }

            // C. Valor total del inventario
            if (ContienePalabra(norm, "valor", "costo total", "cuanto vale", "dinero en inventario"))
            {
                var items = await _context.Inventarios.ToListAsync();
                decimal valorTotal = items.Sum(i => (decimal)i.StockActual * i.PrecioCompra);
                int cantidadItems = items.Count;
                int unidadesTotales = items.Sum(i => i.StockActual);

                return $"💵 **Valor del Inventario CARPINTEC:**\n\n" +
                       $"• **Valor total en compra:** ${valorTotal:N0} COP\n" +
                       $"• **Tipos de material:** {cantidadItems} referencias\n" +
                       $"• **Total unidades físicas:** {unidadesTotales:N0} unidades almacenadas";
            }

            // D. Búsqueda de un material específico
            string materialBuscar = ExtraerTerminoBusqueda(norm, "stock de", "inventario de", "cuanto hay de", "cuanto stock de", "queda de", "quedan", "hay de", "madera", "herraje", "tornillo");
            if (!string.IsNullOrWhiteSpace(materialBuscar) && materialBuscar.Length >= 3)
            {
                var encontrados = await _context.Inventarios
                    .Where(i => i.NombreProducto != null && i.NombreProducto.ToLower().Contains(materialBuscar))
                    .Take(5)
                    .ToListAsync();

                if (encontrados.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"📦 **Resultados de inventario para '{materialBuscar}':**\n");
                    foreach (var m in encontrados)
                    {
                        string alerta = m.StockActual <= m.StockMinimo ? "⚠️ (Stock Bajo)" : "✅ (Disponible)";
                        sb.AppendLine($"• **{m.NombreProducto}:** {m.StockActual} {m.UnidadMedida} {alerta} | Precio compra: ${m.PrecioCompra:N0}");
                    }
                    return sb.ToString();
                }
            }

            // E. Resumen general de inventario
            var resumenInventario = await _context.Inventarios.Take(6).ToListAsync();
            int totalMateriales = await _context.Inventarios.CountAsync();
            int totalBajos = await _context.Inventarios.CountAsync(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15);

            StringBuilder general = new StringBuilder();
            general.AppendLine($"📦 **Estado del Inventario ({totalMateriales} referencias totales, {totalBajos} en stock bajo):**\n");
            foreach (var item in resumenInventario)
            {
                general.AppendLine($"• **{item.NombreProducto}:** {item.StockActual} {item.UnidadMedida}");
            }
            general.AppendLine("\n💡 *Puedes preguntarme por 'stock bajo' o por un material específico como 'stock de tornillos'.*");
            return general.ToString();
        }

        // ==========================================
        // 4. Pedidos
        // ==========================================
        private async Task<string> ConsultarPedidosAsync(string norm)
        {
            // A. Pedidos pendientes / en proceso
            if (ContienePalabra(norm, "pendiente", "pendientes", "proceso", "en curso", "activos", "curso"))
            {
                var enCurso = await _context.Pedidos
                    .Include(p => p.IdClienteNavigation)
                    .Where(p => p.Estado == "Pendiente" || p.Estado == "En proceso")
                    .OrderByDescending(p => p.FechaEntrega)
                    .Take(5)
                    .ToListAsync();

                if (!enCurso.Any())
                {
                    return "✅ No hay pedidos pendientes ni en proceso en este momento. Todos los pedidos registrados han sido finalizados o entregados.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🚚 **Pedidos en Curso o Pendientes ({enCurso.Count}):**\n");
                foreach (var p in enCurso)
                {
                    string cliente = p.IdClienteNavigation != null ? $"{p.IdClienteNavigation.Nombre} {p.IdClienteNavigation.Apellido}".Trim() : "Cliente";
                    sb.AppendLine($"• **#{p.CodigoPedido}** - {cliente} | Estado: **{p.Estado}** | Entrega: {p.FechaEntrega:dd/MM/yyyy} | ${p.ValorTotal:N0} COP");
                }
                return sb.ToString();
            }

            // B. Conteo y desglose por estado
            if (ContienePalabra(norm, "cuantos", "total", "resumen", "estados"))
            {
                int total = await _context.Pedidos.CountAsync();
                int pendientes = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente");
                int enProceso = await _context.Pedidos.CountAsync(p => p.Estado == "En proceso");
                int entregados = await _context.Pedidos.CountAsync(p => p.Estado == "Entregado" || p.Estado == "Finalizado");
                decimal valorTotal = await _context.Pedidos.SumAsync(p => (decimal?)p.ValorTotal) ?? 0;

                return $"📦 **Estadísticas de Pedidos:**\n\n" +
                       $"• **Total pedidos:** {total}\n" +
                       $"• 🟡 **Pendientes:** {pendientes}\n" +
                       $"• 🔵 **En proceso:** {enProceso}\n" +
                       $"• 🟢 **Entregados/Finalizados:** {entregados}\n" +
                       $"• 💵 **Valor acumulado:** ${valorTotal:N0} COP";
            }

            // C. Listado general reciente
            var recientes = await _context.Pedidos
                .Include(p => p.IdClienteNavigation)
                .OrderByDescending(p => p.IdPedido)
                .Take(5)
                .ToListAsync();

            if (!recientes.Any())
            {
                return "ℹ️ No hay pedidos registrados en la base de datos.";
            }

            StringBuilder gral = new StringBuilder();
            gral.AppendLine("📋 **Últimos pedidos registrados:**\n");
            foreach (var p in recientes)
            {
                string cliente = p.IdClienteNavigation != null ? $"{p.IdClienteNavigation.Nombre} {p.IdClienteNavigation.Apellido}".Trim() : "Cliente";
                gral.AppendLine($"• **#{p.CodigoPedido}:** {cliente} | Estado: {p.Estado} | Total: ${p.ValorTotal:N0} COP");
            }
            gral.AppendLine("\n💡 *Puedes consultar el detalle escribiendo 'estado del pedido PED-...' o 'pedidos pendientes'.*");
            return gral.ToString();
        }

        // ==========================================
        // 5. Facturación y Ventas
        // ==========================================
        private async Task<string> ConsultarFacturasAsync(string norm)
        {
            // A. Total facturado y estadísticas
            if (ContienePalabra(norm, "total", "cuanto", "ganancia", "ingreso", "recaudo", "ventas"))
            {
                int totalFacturas = await _context.Facturas.CountAsync();
                decimal totalDinero = await _context.Facturas.SumAsync(f => (decimal?)f.Total) ?? 0;
                int pagadas = await _context.Facturas.CountAsync(f => f.Estado == "Pagada" || f.Estado == "Emitida");
                int pendientes = await _context.Facturas.CountAsync(f => f.Estado == "Pendiente" || f.Estado == "Borrador");

                return $"💰 **Reporte Financiero y de Facturación:**\n\n" +
                       $"• **Total Facturado:** ${totalDinero:N0} COP\n" +
                       $"• **Número de facturas emitidas:** {totalFacturas}\n" +
                       $"• 🟢 **Facturas pagadas/emitidas:** {pagadas}\n" +
                       $"• 🟡 **Facturas pendientes/borrador:** {pendientes}\n\n" +
                       $"💡 *Puedes buscar una factura por folio escribiendo 'factura FAC-...' o 'facturas pendientes'.*";
            }

            // B. Facturas pendientes
            if (ContienePalabra(norm, "pendiente", "pendientes", "cobrar", "deben"))
            {
                var pendientes = await _context.Facturas
                    .Where(f => f.Estado == "Pendiente" || f.Estado == "Borrador")
                    .Take(5)
                    .ToListAsync();

                if (!pendientes.Any())
                {
                    return "✅ No se registran facturas pendientes de cobro en este momento.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📄 **Facturas Pendientes ({pendientes.Count}):**\n");
                foreach (var f in pendientes)
                {
                    sb.AppendLine($"• **{f.Folio}:** {f.Cliente} | Total: ${f.Total:N0} COP | Vencimiento: {(f.FechaVencimiento.HasValue ? f.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "No fijado")}");
                }
                return sb.ToString();
            }

            // C. Listado general reciente
            var ultimas = await _context.Facturas
                .OrderByDescending(f => f.IdFactura)
                .Take(5)
                .ToListAsync();

            if (!ultimas.Any())
            {
                return "ℹ️ Actualmente no hay facturas registradas en la base de datos.";
            }

            StringBuilder listado = new StringBuilder();
            listado.AppendLine("📋 **Últimas facturas registradas:**\n");
            foreach (var f in ultimas)
            {
                listado.AppendLine($"• **{f.Folio}** - {f.Cliente} | ${f.Total:N0} COP | Estado: **{f.Estado}**");
            }
            return listado.ToString();
        }

        // ==========================================
        // 6. Cotizaciones
        // ==========================================
        private async Task<string> ConsultarCotizacionesAsync(string norm)
        {
            int total = await _context.Cotizaciones.CountAsync();
            int pendientes = await _context.Cotizaciones.CountAsync(c => c.Estado == "Pendiente");
            int aprobadas = await _context.Cotizaciones.CountAsync(c => c.Estado == "Aprobada" || c.Estado == "Aprobado");
            decimal totalCotizado = await _context.Cotizaciones.SumAsync(c => (decimal?)c.Total) ?? 0;

            if (ContienePalabra(norm, "pendiente", "pendientes"))
            {
                var cotPendientes = await _context.Cotizaciones
                    .Include(c => c.IdClienteNavigation)
                    .Where(c => c.Estado == "Pendiente")
                    .Take(5)
                    .ToListAsync();

                if (!cotPendientes.Any())
                {
                    return "✅ No hay cotizaciones pendientes en este momento. Todas han sido aprobadas o procesadas.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📄 **Cotizaciones Pendientes ({cotPendientes.Count}):**\n");
                foreach (var c in cotPendientes)
                {
                    string cliente = c.NombreCliente ?? (c.IdClienteNavigation != null ? $"{c.IdClienteNavigation.Nombre} {c.IdClienteNavigation.Apellido}" : "Cliente");
                    sb.AppendLine($"• **Folio #{c.Folio}:** {cliente} | Monto: ${c.Total:N0} COP | Fecha: {c.Fecha:dd/MM/yyyy}");
                }
                return sb.ToString();
            }

            return $"📄 **Métricas de Cotizaciones:**\n\n" +
                   $"• **Total cotizaciones registradas:** {total}\n" +
                   $"• 🟡 **Cotizaciones pendientes:** {pendientes}\n" +
                   $"• 🟢 **Cotizaciones aprobadas:** {aprobadas}\n" +
                   $"• 💵 **Monto total cotizado:** ${totalCotizado:N0} COP\n\n" +
                   $"💡 *Para ver una en específico, escribe el folio como 'folio COT-...' o consulta 'cotizaciones pendientes'.*";
        }

        // ==========================================
        // 7. PQR (Peticiones, Quejas, Reclamos)
        // ==========================================
        private async Task<string> ConsultarPQRAsync(string norm)
        {
            int total = await _context.Pqrs.CountAsync();
            int pendientes = await _context.Pqrs.CountAsync(p => p.Estado == "Pendiente");
            int enProceso = await _context.Pqrs.CountAsync(p => p.Estado == "En proceso");
            int resueltas = await _context.Pqrs.CountAsync(p => p.Estado == "Respondida" || p.Estado == "Resuelta" || p.Estado == "Cerrada");

            if (ContienePalabra(norm, "pendiente", "pendientes", "sin responder", "urgente"))
            {
                var listaPend = await _context.Pqrs
                    .Include(p => p.IdClienteNavigation)
                    .Where(p => p.Estado == "Pendiente" || p.Estado == "En proceso")
                    .Take(5)
                    .ToListAsync();

                if (!listaPend.Any())
                {
                    return "🎉 ¡Excelente! No hay peticiones, quejas o reclamos (PQR) pendientes. Todas han sido atendidas.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📩 **PQR Pendientes de Atención ({listaPend.Count}):**\n");
                foreach (var p in listaPend)
                {
                    string cliente = p.IdClienteNavigation != null ? $"{p.IdClienteNavigation.Nombre} {p.IdClienteNavigation.Apellido}".Trim() : "Cliente";
                    sb.AppendLine($"• **#{p.CodigoPqr}** ({p.Tipo}) - {cliente}\n  Asunto: *{p.Asunto}* | Estado: {p.Estado}");
                }
                return sb.ToString();
            }

            return $"📩 **Gestión de PQR (Total: {total}):**\n\n" +
                   $"• 🟡 **Pendientes:** {pendientes}\n" +
                   $"• 🔵 **En proceso:** {enProceso}\n" +
                   $"• 🟢 **Respondidas / Cerradas:** {resueltas}\n\n" +
                   $"💡 *Puedes consultar radicados específicos con 'pqr PQR-...' o preguntar por 'pqr pendientes'.*";
        }

        // ==========================================
        // 8. Clientes
        // ==========================================
        private async Task<string> ConsultarClientesAsync(string norm)
        {
            // Búsqueda por nombre o empresa
            string termino = ExtraerTerminoBusqueda(norm, "cliente", "clientes", "buscar cliente", "datos de", "contacto de", "empresa");
            if (!string.IsNullOrWhiteSpace(termino) && termino.Length >= 3 && !ContienePalabra(termino, "total", "cuantos", "inactivos", "activos"))
            {
                var encontrados = await _context.Clientes
                    .Where(c => (c.Nombre != null && c.Nombre.ToLower().Contains(termino)) ||
                                (c.Apellido != null && c.Apellido.ToLower().Contains(termino)) ||
                                (c.NombreEmpresa != null && c.NombreEmpresa.ToLower().Contains(termino)) ||
                                (c.Documento != null && c.Documento.Contains(termino)))
                    .Take(5)
                    .ToListAsync();

                if (encontrados.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"👥 **Clientes encontrados para '{termino}':**\n");
                    foreach (var c in encontrados)
                    {
                        string nombreCompleto = c.TipoCliente == "Empresa" ? c.NombreEmpresa : $"{c.Nombre} {c.Apellido}".Trim();
                        sb.AppendLine($"• **{nombreCompleto}** ({c.TipoCliente})\n  Teléfono: {c.Telefono} | Correo: {c.Correo} | Estado: {c.Estado}");
                    }
                    return sb.ToString();
                }
            }

            int total = await _context.Clientes.CountAsync();
            int activos = await _context.Clientes.CountAsync(c => c.Estado == "Activo");
            int inactivos = await _context.Clientes.CountAsync(c => c.Estado == "Inactivo");
            int empresas = await _context.Clientes.CountAsync(c => c.TipoCliente == "Empresa");
            int personas = total - empresas;

            return $"👥 **Directorio de Clientes:**\n\n" +
                   $"• **Total clientes registrados:** {total}\n" +
                   $"• 🟢 **Activos:** {activos} | ⚪ **Inactivos:** {inactivos}\n" +
                   $"• 🏢 **Empresas:** {empresas} | 👤 **Personas Naturales:** {personas}\n\n" +
                   $"💡 *Puedes buscar un cliente específico escribiendo por ejemplo: 'buscar cliente Carlos' o 'datos de Maderas del Norte'.*";
        }

        // ==========================================
        // 9. Empleados y Mano de Obra
        // ==========================================
        private async Task<string> ConsultarEmpleadosAsync(string norm)
        {
            int total = await _context.Empleados.CountAsync();
            int activos = await _context.Empleados.CountAsync(e => e.Estado == "Activo");

            if (ContienePalabra(norm, "ebanista", "ebanistas", "carpintero", "carpinteros", "cargo", "cargos"))
            {
                var empleadosPorCargo = await _context.Empleados
                    .Where(e => e.Estado == "Activo")
                    .Take(8)
                    .ToListAsync();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"👷 **Equipo de Taller y Producción ({activos} activos):**\n");
                foreach (var e in empleadosPorCargo)
                {
                    sb.AppendLine($"• **{e.Nombre} {e.Apellido}:** Cargo: *{e.Cargo}*");
                }
                return sb.ToString();
            }

            var empleados = await _context.Empleados.Take(5).ToListAsync();
            StringBuilder gen = new StringBuilder();
            gen.AppendLine($"👷 **Personal y Mano de Obra CARPINTEC:**\n\n" +
                           $"• **Empleados activos:** {activos} de {total} registrados.\n\n" +
                           $"**Integrantes del equipo:**\n");
            foreach (var e in empleados)
            {
                gen.AppendLine($"• {e.Nombre} {e.Apellido} — *{e.Cargo}* ({e.Estado})");
            }
            return gen.ToString();
        }

        // ==========================================
        // 10. Productos y Catálogo
        // ==========================================
        private async Task<string> ConsultarProductosAsync(string norm)
        {
            // A. Producto más caro / más económico
            if (ContienePalabra(norm, "mas caro", "mas costoso", "mayor precio"))
            {
                var topCaro = await _context.Productos
                    .Where(p => p.Precio.HasValue)
                    .OrderByDescending(p => p.Precio)
                    .FirstOrDefaultAsync();

                if (topCaro != null)
                {
                    return $"💎 **Producto más costoso del catálogo:**\n\n" +
                           $"• **Nombre:** {topCaro.Nombre}\n" +
                           $"• **Precio:** ${topCaro.Precio:N0} COP\n" +
                           $"• **Categoría:** {topCaro.Categoria ?? "General"}\n" +
                           $"• **Medidas:** {topCaro.Medidas ?? "Personalizadas"}";
                }
            }

            if (ContienePalabra(norm, "mas barato", "mas economico", "menor precio"))
            {
                var topBarato = await _context.Productos
                    .Where(p => p.Precio.HasValue && p.Precio > 0)
                    .OrderBy(p => p.Precio)
                    .FirstOrDefaultAsync();

                if (topBarato != null)
                {
                    return $"🏷️ **Producto más económico del catálogo:**\n\n" +
                           $"• **Nombre:** {topBarato.Nombre}\n" +
                           $"• **Precio:** ${topBarato.Precio:N0} COP\n" +
                           $"• **Categoría:** {topBarato.Categoria ?? "General"}";
                }
            }

            // B. Búsqueda por término específico (ej. "precio de cocina", "mesa comedor", etc.)
            string termino = ExtraerTerminoBusqueda(norm, "precio de", "cuanto vale", "cuanto cuesta", "producto", "productos", "mueble", "muebles", "catalogo de");
            if (!string.IsNullOrWhiteSpace(termino) && termino.Length >= 3 && !ContienePalabra(termino, "todos", "total", "cuantos", "disponibles"))
            {
                var encontrados = await _context.Productos
                    .Where(p => (p.Nombre != null && p.Nombre.ToLower().Contains(termino)) ||
                                (p.Categoria != null && p.Categoria.ToLower().Contains(termino)))
                    .Take(5)
                    .ToListAsync();

                if (encontrados.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"🪑 **Productos encontrados para '{termino}':**\n");
                    foreach (var p in encontrados)
                    {
                        sb.AppendLine($"• **{p.Nombre}:** ${p.Precio:N0} COP (Categoría: {p.Categoria ?? "General"})");
                    }
                    return sb.ToString();
                }
            }

            // C. Conteo y catálogo general
            int total = await _context.Productos.CountAsync();
            int activos = await _context.Productos.CountAsync(p => p.Estado == "Activo");
            var catalogo = await _context.Productos.Where(p => p.Estado == "Activo").Take(6).ToListAsync();

            StringBuilder cat = new StringBuilder();
            cat.AppendLine($"🪑 **Catálogo de Productos CARPINTEC ({activos} activos de {total}):**\n");
            foreach (var p in catalogo)
            {
                cat.AppendLine($"• **{p.Nombre}:** ${p.Precio:N0} COP — *{p.Categoria ?? "General"}*");
            }
            cat.AppendLine("\n💡 *Puedes preguntar por precios de productos concretos como 'precio de cocina integral' o 'producto más caro'.*");
            return cat.ToString();
        }

        // ==========================================
        // 11. Coincidencias directas y extracción
        // ==========================================
        private async Task<string> BuscarCoincidenciaDirectaAsync(string original, string norm)
        {
            if (norm.Length < 3) return string.Empty;

            // Intentar buscar en productos
            var prod = await _context.Productos
                .FirstOrDefaultAsync(p => p.Nombre.ToLower().Contains(norm));
            if (prod != null)
            {
                return $"🪑 **Producto encontrado:**\n\n" +
                       $"• **{prod.Nombre}**\n" +
                       $"• **Precio:** ${prod.Precio:N0} COP\n" +
                       $"• **Categoría:** {prod.Categoria ?? "General"}\n" +
                       $"• **Descripción:** {prod.Descripcion ?? "Mueble de alta artesanía en madera."}";
            }

            // Intentar buscar en inventario
            var inv = await _context.Inventarios
                .FirstOrDefaultAsync(i => i.NombreProducto != null && i.NombreProducto.ToLower().Contains(norm));
            if (inv != null)
            {
                return $"📦 **Material en Inventario:**\n\n" +
                       $"• **{inv.NombreProducto}**\n" +
                       $"• **Stock actual:** {inv.StockActual} {inv.UnidadMedida}\n" +
                       $"• **Precio compra:** ${inv.PrecioCompra:N0} COP\n" +
                       $"• **Estado:** {inv.Estado}";
            }

            // Intentar buscar en clientes
            var cli = await _context.Clientes
                .FirstOrDefaultAsync(c => (c.Nombre != null && c.Nombre.ToLower().Contains(norm)) ||
                                          (c.Apellido != null && c.Apellido.ToLower().Contains(norm)) ||
                                          (c.NombreEmpresa != null && c.NombreEmpresa.ToLower().Contains(norm)));
            if (cli != null)
            {
                string nombre = cli.TipoCliente == "Empresa" ? cli.NombreEmpresa : $"{cli.Nombre} {cli.Apellido}".Trim();
                return $"👤 **Cliente registrado:**\n\n" +
                       $"• **Nombre:** {nombre} ({cli.TipoCliente})\n" +
                       $"• **Teléfono:** {cli.Telefono}\n" +
                       $"• **Correo:** {cli.Correo}\n" +
                       $"• **Estado:** {cli.Estado}";
            }

            return string.Empty;
        }

        private static string ExtraerTerminoBusqueda(string texto, params string[] prefijos)
        {
            foreach (var prefijo in prefijos)
            {
                int index = texto.IndexOf(prefijo, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    string resto = texto.Substring(index + prefijo.Length).Trim();
                    if (!string.IsNullOrEmpty(resto)) return resto;
                }
            }
            return string.Empty;
        }

        #endregion

        #region Respuestas Predefinidas y Menús

        private static string GenerarSaludo()
        {
            return "👋 ¡Hola! Soy tu **Asistente Inteligente de CARPINTEC** 🪵\n\n" +
                   "Estoy conectado en tiempo real a la base de datos para ayudarte a gestionar la carpintería.\n\n" +
                   "**¿Qué te gustaría consultar hoy?**\n" +
                   "• 📊 *'Resumen general'* (estadísticas del negocio)\n" +
                   "• ⚠️ *'Stock bajo'* (materiales que necesitan reposición)\n" +
                   "• 🚚 *'Pedidos pendientes'* (entregas en curso)\n" +
                   "• 💰 *'Total facturado'* (ventas y recaudos)\n" +
                   "• 🪑 *'Productos'* (precios y catálogo)\n" +
                   "• 📩 *'PQR pendientes'* (quejas o reclamos de clientes)\n\n" +
                   "¡Escribe tu pregunta o selecciona una opción!";
        }

        private static string GenerarMenuAyuda()
        {
            return "🤖 **Guía de Consultas que entiendo:**\n\n" +
                   "📦 **Inventario:**\n" +
                   "  • *'¿Qué productos tienen stock bajo?'*\n" +
                   "  • *'¿Cuánto stock hay de tornillos?'*\n" +
                   "  • *'¿Cuál es el valor total del inventario?'*\n\n" +
                   "🚚 **Pedidos:**\n" +
                   "  • *'¿Cuántos pedidos hay pendientes?'*\n" +
                   "  • *'Estado del pedido PED-2024-001'*\n\n" +
                   "💰 **Finanzas y Facturación:**\n" +
                   "  • *'¿Cuánto se ha facturado en total?'*\n" +
                   "  • *'Facturas pendientes de pago'*\n\n" +
                   "🪑 **Catálogo:**\n" +
                   "  • *'Precio de cocinas integrales'*\n" +
                   "  • *'¿Cuál es el producto más caro?'*\n\n" +
                   "👥 **Clientes y PQR:**\n" +
                   "  • *'Buscar cliente Carlos'*\n" +
                   "  • *'¿Cuántas PQR hay pendientes?'*\n\n" +
                   "📊 O simplemente escribe *'resumen'* para ver el estado general.";
        }

        private static string GenerarRespuestaPorDefecto(string pregunta)
        {
            return $"🤔 No encontré datos exactos para *\"{pregunta}\"* en la base de datos.\n\n" +
                   $"Puedo responder preguntas como:\n" +
                   $"• ⚠️ *'Stock bajo'* (materiales críticos)\n" +
                   $"• 🚚 *'Pedidos pendientes'*\n" +
                   $"• 💰 *'Total facturado'*\n" +
                   $"• 🪑 *'Precio de [producto]'*\n" +
                   $"• 👥 *'Buscar cliente [nombre]'*\n" +
                   $"• 📩 *'PQR pendientes'*\n" +
                   $"• 📊 *'Resumen general'*\n\n" +
                   $"Intenta preguntar de nuevo con alguna de estas palabras clave.";
        }

        #endregion
    }
    /* FIN: Servicio Inteligente de Chatbot conectado a la Base de Datos */
}