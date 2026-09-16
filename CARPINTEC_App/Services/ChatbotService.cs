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
    /* INICIO: Servicio Inteligente de Chatbot conectado a la Base de Datos y Guías de Usuario */
    public class ChatbotService
    {
        private readonly CarpintecContext _context;

        public ChatbotService(CarpintecContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Procesa la pregunta del usuario en lenguaje natural y genera una respuesta basada en datos de la base de datos o guías paso a paso.
        /// </summary>
        public async Task<string> ResponderAsync(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                return "👋 ¡Hola! Soy tu asistente virtual de CARPINTEC 🪵. ¿En qué puedo orientarte hoy? Puedes consultarme sobre la base de datos (clientes, inventario, pedidos, cotizaciones, facturas) o preguntarme cómo realizar cualquier proceso en el sistema (ej: cómo añadir un cliente).";
            }

            string normalizado = NormalizarTexto(mensaje);

            // 1. Saludos y bienvenida
            if (EsSaludo(normalizado))
            {
                return GenerarSaludo();
            }

            // 2. Agradecimientos y despedidas
            if (EsAgradecimiento(normalizado))
            {
                return GenerarAgradecimiento();
            }

            // 3. Guías operativas paso a paso (How-To / Diligenciamiento de formularios)
            // PRIORIDAD ALTA: Si el usuario pregunta "cómo hacer X" o "cómo llenar campos", se responde con la guía antes de buscar en la BD.
            string respuestaGuia = ConsultarGuiaSistema(normalizado);
            if (!string.IsNullOrEmpty(respuestaGuia))
            {
                return respuestaGuia;
            }

            // 4. Ayuda general y menú de capacidades
            if (EsAyuda(normalizado))
            {
                return GenerarMenuAyuda();
            }

            // 5. Detección de códigos específicos (PED-, FAC-, PQR-, COT-)
            string respuestaCodigo = await BuscarPorCodigoAsync(mensaje, normalizado);
            if (!string.IsNullOrEmpty(respuestaCodigo))
            {
                return respuestaCodigo;
            }

            // 6. Resumen general / Balance / KPIs de la empresa
            if (EsResumenGeneral(normalizado))
            {
                return await ConsultarResumenGeneralAsync();
            }

            // 7. Inventario y Stock
            if (EsConsultaInventario(normalizado))
            {
                return await ConsultarInventarioAsync(normalizado);
            }

            // 8. Pedidos
            if (EsConsultaPedidos(normalizado))
            {
                return await ConsultarPedidosAsync(normalizado);
            }

            // 9. Facturas y Ventas
            if (EsConsultaFacturas(normalizado))
            {
                return await ConsultarFacturasAsync(normalizado);
            }

            // 10. Cotizaciones
            if (EsConsultaCotizaciones(normalizado))
            {
                return await ConsultarCotizacionesAsync(normalizado);
            }

            // 11. PQR (Peticiones, Quejas, Reclamos)
            if (EsConsultaPQR(normalizado))
            {
                return await ConsultarPQRAsync(normalizado);
            }

            // 12. Clientes (Directorio, totales, filtros, búsquedas)
            if (EsConsultaClientes(normalizado))
            {
                return await ConsultarClientesAsync(normalizado);
            }

            // 13. Empleados y Mano de Obra
            if (EsConsultaEmpleados(normalizado))
            {
                return await ConsultarEmpleadosAsync(normalizado);
            }

            // 14. Usuarios del Sistema y Seguridad
            if (EsConsultaUsuarios(normalizado))
            {
                return await ConsultarUsuariosAsync(normalizado);
            }

            // 15. Productos y Catálogo
            if (EsConsultaProductos(normalizado))
            {
                return await ConsultarProductosAsync(normalizado);
            }

            // 16. Fallback inteligente: buscar coincidencia directa en nombres de clientes, productos o inventario
            string busquedaDirecta = await BuscarCoincidenciaDirectaAsync(mensaje, normalizado);
            if (!string.IsNullOrEmpty(busquedaDirecta))
            {
                return busquedaDirecta;
            }

            // 17. Respuesta por defecto con sugerencias amigables
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
            return ContienePalabra(t, "hola", "buenos dias", "buenas tardes", "buenas noches", "que tal", "saludos", "buenas", "hey", "buen dia");
        }

        private static bool EsAgradecimiento(string t)
        {
            return ContienePalabra(t, "gracias", "muchas gracias", "mil gracias", "te agradezco", "chao", "adios", "hasta luego", "excelente gracias", "vale gracias", "perfecto gracias");
        }

        private static bool EsAyuda(string t)
        {
            return ContienePalabra(t, "ayuda", "que puedes hacer", "que sabes hacer", "opciones", "comandos", "menu", "como funciona", "que puedo preguntar", "instrucciones", "que funciones tienes");
        }

        private static bool EsResumenGeneral(string t)
        {
            return ContienePalabra(t, "resumen", "balance", "estadistica", "estadisticas", "metricas", "como va el negocio", "reporte general", "dashboard", "kpi", "resumen ejecutivo", "estado del negocio", "como esta el sistema", "como vamos", "panorama general");
        }

        private static bool EsConsultaInventario(string t)
        {
            return ContienePalabra(t, "inventario", "stock", "existencia", "existencias", "material", "materiales", "madera", "herraje", "agotado", "agotados", "bodega", "quedan", "queda", "solicitudes de reposicion", "reposicion");
        }

        private static bool EsConsultaPedidos(string t)
        {
            return ContienePalabra(t, "pedido", "pedidos", "orden", "ordenes", "solicitud de entrega", "entregas");
        }

        private static bool EsConsultaFacturas(string t)
        {
            return ContienePalabra(t, "factura", "facturas", "facturacion", "venta", "ventas", "cobro", "cobros", "ingreso", "ingresos", "ganancia", "ganancias", "dinero", "recaudo", "facturado");
        }

        private static bool EsConsultaCotizaciones(string t)
        {
            return ContienePalabra(t, "cotizacion", "cotizaciones", "cotizar", "presupuesto", "presupuestos", "folio");
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
            return ContienePalabra(t, "empleado", "empleados", "trabajador", "trabajadores", "personal", "mano de obra", "ebanista", "ebanistas", "carpintero", "carpinteros", "operario", "operarios", "plantilla");
        }

        private static bool EsConsultaUsuarios(string t)
        {
            return ContienePalabra(t, "usuario", "usuarios", "administrador", "administradores", "cuenta", "cuentas", "bloqueado", "bloqueados", "intentos fallidos", "roles", "login");
        }

        private static bool EsConsultaProductos(string t)
        {
            return ContienePalabra(t, "producto", "productos", "catalogo", "mueble", "muebles", "precio", "precios", "cuanto cuesta", "cuanto vale", "cocina", "closet", "mesa", "silla", "escritorio", "puerta");
        }

        #endregion

        #region Guías de Usuario del Sistema (How-To)

        /// <summary>
        /// Detecta si el usuario está preguntando cómo realizar una acción o llenar campos en el sistema.
        /// </summary>
        private string ConsultarGuiaSistema(string norm)
        {
            bool esPreguntaComo = ContienePalabra(norm,
                "como", "pasos", "formulario", "llenar", "campos", "guia", "procedimiento",
                "crear", "anadir", "agregar", "registrar", "nuevo", "nueva", "registrar nuevo",
                "dar de alta", "donde se crea", "explicame", "explica", "requisitos");

            if (!esPreguntaComo) return string.Empty;

            // 1. Cómo añadir / crear un cliente y llenar los campos
            if (ContienePalabra(norm, "cliente", "clientes"))
            {
                return "👤 **Guía Paso a Paso: Cómo Registrar un Cliente en CARPINTEC**\n\n" +
                       "📍 **Ruta de acceso:**\n" +
                       "1. En el menú lateral izquierdo, ve a **Gestión Comercial** > **Gestión de clientes**.\n" +
                       "2. En la parte superior derecha, haz clic en el botón dorado **\"Nuevo Cliente\"**. Se abrirá el formulario modal.\n\n" +
                       "📝 **Cómo llenar cada campo:**\n" +
                       "• **Tipo de Cliente (Obligatorio):**\n" +
                       "  - *Natural:* Para personas particulares. Muestra los campos de *Nombre* y *Apellido*.\n" +
                       "  - *Empresa:* Para empresas o negocios. Activa automáticamente el campo *Nombre Empresa* (Razón Social).\n" +
                       "• **Documento (Obligatorio y Único):**\n" +
                       "  - Cédula de ciudadanía, extranjería o NIT (sin puntos ni guiones).\n" +
                       "  - ⚠️ *Validación:* El sistema no permite documentos repetidos.\n" +
                       "• **Nombre y Apellido:** Nombres y apellidos completos (para clientes Naturales).\n" +
                       "• **Nombre Empresa:** Razón social completa (solo si elegiste Empresa).\n" +
                       "• **Contacto (Obligatorio):** Nombre de la persona encargada o punto de contacto comercial.\n" +
                       "• **Teléfono (Obligatorio):** Número celular o fijo (10 dígitos) para llamadas y avisos de entrega.\n" +
                       "• **Correo Electrónico (Obligatorio y Único):**\n" +
                       "  - Dirección de correo válida (ej: `contacto@ejemplo.com`).\n" +
                       "  - ⚠️ *Validación:* Debe ser único en la base de datos.\n" +
                       "• **Ciudad y Dirección:** Ubicación exacta para despachos, instalación y facturación.\n\n" +
                       "⚙️ **Valores automáticos del sistema:**\n" +
                       "• **Estado:** Se registra automáticamente como **Activo**.\n" +
                       "• **Fecha de Registro:** Se asigna la fecha y hora actual automáticamente.\n\n" +
                       "💾 **Guardar:** Haz clic en **\"Guardar\"**. El cliente quedará registrado y disponible para cotizaciones, pedidos y facturas.";
            }

            // 2. Cómo crear una cotización
            if (ContienePalabra(norm, "cotizacion", "cotizaciones", "cotizar"))
            {
                return "📄 **Guía: Cómo Generar una Nueva Cotización**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Gestión Comercial** > **Cotizaciones** > Botón **\"Nueva Cotización\"**.\n\n" +
                       "📝 **Campos a Diligenciar:**\n" +
                       "• **Cliente:** Selecciona un cliente registrado en la base de datos o ingresa su información básica.\n" +
                       "• **Empleado Asesor:** Empleado responsable de cotizar y dar seguimiento al cliente.\n" +
                       "• **Detalle del Producto:** Descripción del mueble requerido (ej: *Cocina integral en L, Closet 3 cuerpos, Escritorio de roble*).\n" +
                       "• **Tipo de Madera / Material:** Madera maciza (Roble, Cedro, Pino), MDF melamínico, etc.\n" +
                       "• **Medidas:** Dimensiones en centímetros o metros (Alto x Ancho x Profundidad).\n" +
                       "• **Cantidad y Tiempo de Entrega:** Plazo estimado en días hábiles para el taller.\n" +
                       "• **Valor Total:** Presupuesto final acordado en pesos COP.\n\n" +
                       "💡 *Una vez aprobada la cotización por el cliente, puede convertirse directamente en un Pedido de fabricación.*";
            }

            // 3. Cómo registrar un pedido
            if (ContienePalabra(norm, "pedido", "pedidos"))
            {
                return "📦 **Guía: Cómo Crear y Gestionar un Pedido de Producción**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Gestión Comercial** > **Pedidos** > Botón **\"Nuevo Pedido\"**.\n\n" +
                       "📝 **Campos Principales:**\n" +
                       "• **Código de Pedido:** Identificador único (ej: `PED-2026-001`).\n" +
                       "• **Cliente y Cotización:** Selecciona el cliente y vincula la cotización base aprobada.\n" +
                       "• **Producto:** Especificación del mobiliario o piezas a elaborar.\n" +
                       "• **Fecha de Solicitud:** Fecha de ingreso al taller.\n" +
                       "• **Fecha de Entrega:** Fecha comprometida con el cliente para la entrega o instalación.\n" +
                       "• **Valor Total:** Costo acordado de la orden.\n" +
                       "• **Observaciones:** Instrucciones técnicas especiales para los carpinteros o ebanistas.\n\n" +
                       "🔄 **Ciclo de Estados:**\n" +
                       "1. *Pendiente* ➜ 2. *En proceso* ➜ 3. *Terminado* ➜ 4. *Entregado*.";
            }

            // 4. Cómo registrar un empleado
            if (ContienePalabra(norm, "empleado", "empleados", "personal", "trabajador"))
            {
                return "👷 **Guía: Cómo Registrar un Empleado en CARPINTEC**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Administración** > **Gestión de empleados** > Botón **\"Nuevo Empleado\"**.\n\n" +
                       "📝 **Campos Requeridos:**\n" +
                       "• **Tipo de Documento:** Cédula de Ciudadanía (CC), Cédula de Extranjería (CE) o Tarjeta de Identidad (TI).\n" +
                       "• **Documento:** Número de identificación único sin puntos ni caracteres especiales.\n" +
                       "• **Nombres y Apellidos:** Nombre completo del trabajador.\n" +
                       "• **Cargo:** Maestro Carpintero, Ebanista, Pintor/Lustrador, Diseñador, Administrador, etc.\n" +
                       "• **Correo y Teléfono:** Información de contacto laboral y personal.\n" +
                       "• **Fecha de Ingreso:** Fecha de inicio de labores.\n" +
                       "• **Salario:** Salario mensual asignado.\n" +
                       "• **Usuario Asociado (Opcional):** Si el empleado requiere ingresar al sistema web, se le vincula su cuenta de usuario.\n\n" +
                       "💾 Al guardar, el empleado se registrará con estado **Activo**.";
            }

            // 5. Cómo registrar un producto o agregar a inventario
            if (ContienePalabra(norm, "producto", "productos", "inventario", "stock", "material"))
            {
                return "🪑 **Guía: Cómo Registrar Productos y Materiales en Inventario**\n\n" +
                       "📍 **Para Productos del Catálogo:**\n" +
                       "• Ve a **Producción** > **Productos** > **\"Nuevo Producto\"**.\n" +
                       "• Diligencia: *Nombre*, *Código*, *Categoría* (Cocinas, Closets, Puertas, etc.), *Material principal*, *Medidas* y *Precio de venta*.\n\n" +
                       "📍 **Para Materiales de Inventario:**\n" +
                       "• Ve a **Producción** > **Inventario**.\n" +
                       "• Diligencia: *Nombre del Material* (Tornillos, Bisagras, Lámina MDF, etc.), *Stock Actual*, *Stock Mínimo* (umbral para alertas automáticas de reposición), *Unidad de Medida* y *Precio de Compra*.\n\n" +
                       "⚠️ *Recuerda mantener el stock mínimo configurado para que el sistema te avise cuando falte material.*";
            }

            // 6. Cómo generar una factura o registrar venta
            if (ContienePalabra(norm, "factura", "facturas", "venta", "ventas", "facturar"))
            {
                return "💰 **Guía: Cómo Emitir una Factura y Registrar Ventas**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Gestión Comercial** > **Ventas y facturación** > **\"Nueva Factura\"**.\n\n" +
                       "📝 **Campos del Formulario:**\n" +
                       "• **Folio:** Número consecutivo de factura (ej: `FAC-001`).\n" +
                       "• **Cliente:** Selecciona el cliente a facturar.\n" +
                       "• **Fecha de Emisión y Vencimiento:** Plazo otorgado para el pago.\n" +
                       "• **Método de Pago:** Efectivo, Transferencia Bancaria, Tarjeta Débito/Crédito.\n" +
                       "• **Desglose Económico:** Subtotal, IVA (19%), Descuento (si aplica), Costo de Transporte y Costo de Instalación.\n" +
                       "• **Estado:** *Pendiente* (si está por cobrar) o *Pagada*.";
            }

            // 7. Cómo gestionar o responder un PQR
            if (ContienePalabra(norm, "pqr", "pqrs", "queja", "reclamo", "sugerencia"))
            {
                return "📩 **Guía: Cómo Gestionar y Atender PQRs**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Reportes y soporte** > **PQR**.\n\n" +
                       "📝 **Procedimiento:**\n" +
                       "1. En el listado de PQRs, ubica el radicado pendiente (identificado con código `PQR-...`).\n" +
                       "2. Revisa el tipo (*Petición, Queja, Reclamo o Sugerencia*), el cliente y la descripción del caso.\n" +
                       "3. Cambia el estado a **En proceso** mientras se realiza la investigación con el taller o despacho.\n" +
                       "4. Diligencia la solución brindada en el campo **Respuesta** y actualiza el estado a **Resuelta / Respondida**.";
            }

            // 8. Cómo gestionar usuarios y desbloqueos
            if (ContienePalabra(norm, "usuario", "usuarios", "bloqueado", "bloqueo", "rol", "roles", "contrasena"))
            {
                return "🔐 **Guía: Gestión de Usuarios y Seguridad de Cuentas**\n\n" +
                       "📍 **Ruta:** Menú lateral > **Administración** > **Gestión de usuarios**.\n\n" +
                       "🛡️ **Políticas de Seguridad de CARPINTEC:**\n" +
                       "• **Roles disponibles:** *Administrador*, *Empleado*, *Cliente*.\n" +
                       "• **Bloqueo de seguridad:** Los usuarios con rol Cliente o Empleado se bloquean automáticamente si acumulan **3 intentos fallidos** de contraseña.\n" +
                       "• **Desbloquear usuario:** Desde este módulo, el Administrador puede editar el usuario, restablecer los *Intentos Fallidos a 0* y cambiar el estado a **Activo**.";
            }

            return string.Empty;
        }

        #endregion

        #region Consultas a la Base de Datos

        // ==========================================
        // 1. Búsqueda directa por códigos (PED-, FAC-, PQR-, COT-)
        // ==========================================
        private async Task<string> BuscarPorCodigoAsync(string original, string norm)
        {
            // Búsqueda de código PED-XXXX o ID de pedido
            var matchPedido = Regex.Match(original, @"(ped|PED)[-_]?\d+", RegexOptions.IgnoreCase);
            if (matchPedido.Success)
            {
                string codigo = matchPedido.Value;
                var pedido = await _context.Pedidos
                    .Include(p => p.IdClienteNavigation)
                    .FirstOrDefaultAsync(p => p.CodigoPedido.Contains(codigo) || p.IdPedido.ToString() == codigo);

                if (pedido != null)
                {
                    string cliente = pedido.IdClienteNavigation != null ? $"{pedido.IdClienteNavigation.Nombre} {pedido.IdClienteNavigation.Apellido}".Trim() : (pedido.IdClienteNavigation?.NombreEmpresa ?? "No asignado");
                    return $"📦 **Información del Pedido #{pedido.CodigoPedido}**\n\n" +
                           $"• **Cliente:** {cliente}\n" +
                           $"• **Producto:** {pedido.Producto ?? "Mobiliario a medida"}\n" +
                           $"• **Estado:** {pedido.Estado}\n" +
                           $"• **Valor Total:** ${pedido.ValorTotal:N0} COP\n" +
                           $"• **Fecha de Solicitud:** {pedido.FechaSolicitud:dd/MM/yyyy}\n" +
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
                           $"• **Subtotal:** ${factura.Subtotal:N0} COP | **IVA:** ${factura.IVA:N0} COP\n" +
                           $"• **Estado:** {factura.Estado}\n" +
                           $"• **Fecha Emisión:** {factura.Fecha:dd/MM/yyyy}\n" +
                           $"• **Fecha Vencimiento:** {(factura.FechaVencimiento.HasValue ? factura.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "No fijado")}\n" +
                           $"• **Método de Pago:** {factura.MetodoPago ?? "No especificado"}";
                }
            }

            // Búsqueda de código COT-XXXX
            var matchCot = Regex.Match(original, @"(cot|COT)[-_]?\d+", RegexOptions.IgnoreCase);
            if (matchCot.Success)
            {
                string folio = matchCot.Value;
                var cot = await _context.Cotizaciones
                    .Include(c => c.IdClienteNavigation)
                    .Include(c => c.IdEmpleadoNavigation)
                    .FirstOrDefaultAsync(c => c.Folio.Contains(folio));

                if (cot != null)
                {
                    string cliente = cot.NombreCliente ?? (cot.IdClienteNavigation != null ? $"{cot.IdClienteNavigation.Nombre} {cot.IdClienteNavigation.Apellido}".Trim() : "Cliente");
                    string asesor = cot.IdEmpleadoNavigation != null ? $"{cot.IdEmpleadoNavigation.Nombre} {cot.IdEmpleadoNavigation.Apellido}".Trim() : "No asignado";

                    return $"📄 **Información de la Cotización #{cot.Folio}**\n\n" +
                           $"• **Cliente:** {cliente}\n" +
                           $"• **Asesor Responsable:** {asesor}\n" +
                           $"• **Mueble / Detalle:** {cot.DetalleProducto ?? "Mobiliario personalizado"}\n" +
                           $"• **Tipo de Madera:** {cot.TipoMadera ?? "No especificado"}\n" +
                           $"• **Medidas:** {cot.Medidas ?? "Según diseño"}\n" +
                           $"• **Total Cotizado:** ${cot.Total:N0} COP\n" +
                           $"• **Estado:** {cot.Estado}\n" +
                           $"• **Fecha:** {cot.Fecha:dd/MM/yyyy}";
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
                           $"• **Descripción:** {pqr.Descripcion}\n" +
                           $"• **Fecha:** {pqr.FechaRegistro:dd/MM/yyyy}\n" +
                           $"• **Respuesta:** {(string.IsNullOrWhiteSpace(pqr.Respuesta) ? "Pendiente de respuesta" : pqr.Respuesta)}";
                }
            }

            return string.Empty;
        }

        // ==========================================
        // 2. Resumen General / Balance Ejecutivo
        // ==========================================
        private async Task<string> ConsultarResumenGeneralAsync()
        {
            int totalProductos = await _context.Productos.CountAsync();
            int productosActivos = await _context.Productos.CountAsync(p => p.Estado == "Activo");
            int totalClientes = await _context.Clientes.CountAsync();
            int clientesActivos = await _context.Clientes.CountAsync(c => c.Estado == "Activo");
            int totalPedidos = await _context.Pedidos.CountAsync();
            int pedidosPendientes = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente" || p.Estado == "En proceso");
            int stockBajo = await _context.Inventarios.CountAsync(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15);
            decimal totalFacturado = await _context.Facturas.SumAsync(f => (decimal?)f.Total) ?? 0;
            int facturasPendientes = await _context.Facturas.CountAsync(f => f.Estado == "Pendiente" || f.Estado == "Borrador");
            int pqrPendientes = await _context.Pqrs.CountAsync(p => p.Estado == "Pendiente" || p.Estado == "En proceso");
            int totalEmpleados = await _context.Empleados.CountAsync(e => e.Estado == "Activo");

            return $"📊 **Resumen Ejecutivo 360° de CARPINTEC**\n\n" +
                   $"• 👥 **Clientes:** {totalClientes} registrados ({clientesActivos} activos)\n" +
                   $"• 🪑 **Catálogo de Productos:** {productosActivos} activos ({totalProductos} totales)\n" +
                   $"• 📦 **Pedidos de Fabricación:** {totalPedidos} registrados ({pedidosPendientes} en curso/pendientes)\n" +
                   $"• ⚠️ **Inventario & Stock:** {stockBajo} materiales con stock bajo o crítico\n" +
                   $"• 💰 **Facturación Total:** ${totalFacturado:N0} COP ({facturasPendientes} facturas por cobrar)\n" +
                   $"• 👷 **Equipo de Trabajo:** {totalEmpleados} empleados activos en taller y ventas\n" +
                   $"• 📩 **Soporte & PQR:** {pqrPendientes} casos pendientes por atender\n\n" +
                   $"💡 *Pregúntame por detalles específicos de cualquiera de estas áreas o por cómo realizar una tarea.*";
        }

        // ==========================================
        // 3. Inventario, Stock y Reposiciones
        // ==========================================
        private async Task<string> ConsultarInventarioAsync(string norm)
        {
            // A. Solicitudes de reposición
            if (ContienePalabra(norm, "solicitud", "solicitudes", "reposicion"))
            {
                var solicitudes = await _context.SolicitudesReposicion
                    .OrderByDescending(s => s.FechaCreacion)
                    .Take(5)
                    .ToListAsync();

                if (!solicitudes.Any())
                {
                    return "✅ No hay solicitudes de reposición pendientes registradas en la base de datos.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📋 **Solicitudes de Reposición Recientes ({solicitudes.Count}):**\n");
                foreach (var s in solicitudes)
                {
                    sb.AppendLine($"• **Solicitud #{s.Id}:** Cantidad: {s.Cantidad} | Prioridad: **{s.Prioridad ?? "Normal"}** | Motivo: {s.Motivo ?? "Abastecimiento"} | Proveedor: {s.Proveedor ?? "Por definir"}");
                }
                return sb.ToString();
            }

            // B. Consulta de stock bajo / crítico
            if (ContienePalabra(norm, "bajo", "critico", "poco", "atencion", "falta", "urgente", "escaso"))
            {
                var bajos = await _context.Inventarios
                    .Where(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15)
                    .OrderBy(i => i.StockActual)
                    .Take(8)
                    .ToListAsync();

                if (!bajos.Any())
                {
                    return "✅ ¡Excelente! No hay materiales con stock crítico actualmente en la base de datos. Todos superan los niveles mínimos de inventario.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"⚠️ **Materiales con Stock Bajo ({bajos.Count} en alerta):**\n");
                foreach (var item in bajos)
                {
                    sb.AppendLine($"• **{item.NombreProducto}:** Quedan **{item.StockActual} {item.UnidadMedida}** (Mínimo: {item.StockMinimo}) | Ubicación: {item.Ubicacion ?? "Taller"}");
                }
                sb.AppendLine("\n💡 *Te sugiero coordinar con compras o generar una solicitud de reposición para estos materiales.*");
                return sb.ToString();
            }

            // C. Materiales agotados (stock == 0)
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
                sb.AppendLine($"🔴 **Materiales Completamente Agotados ({agotados.Count}):**\n");
                foreach (var item in agotados)
                {
                    sb.AppendLine($"• **{item.NombreProducto}:** 0 {item.UnidadMedida} disponibles.");
                }
                return sb.ToString();
            }

            // D. Valor total del inventario
            if (ContienePalabra(norm, "valor", "costo total", "cuanto vale", "dinero en inventario", "inversion"))
            {
                var items = await _context.Inventarios.ToListAsync();
                decimal valorTotal = items.Sum(i => (decimal)i.StockActual * i.PrecioCompra);
                int cantidadItems = items.Count;
                int unidadesTotales = items.Sum(i => i.StockActual);

                return $"💵 **Valorización del Inventario CARPINTEC:**\n\n" +
                       $"• **Valor total en costo de compra:** ${valorTotal:N0} COP\n" +
                       $"• **Referencias en bodega:** {cantidadItems} materiales distintos\n" +
                       $"• **Unidades físicas totales:** {unidadesTotales:N0} unidades/metros almacenados";
            }

            // E. Búsqueda de un material específico
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
                    sb.AppendLine($"📦 **Resultados en Inventario para '{materialBuscar}':**\n");
                    foreach (var m in encontrados)
                    {
                        string alerta = m.StockActual <= m.StockMinimo ? "⚠️ (Stock Bajo)" : "✅ (Disponible)";
                        sb.AppendLine($"• **{m.NombreProducto}:** {m.StockActual} {m.UnidadMedida} {alerta} | Precio compra: ${m.PrecioCompra:N0} COP");
                    }
                    return sb.ToString();
                }
            }

            // F. Resumen general de inventario
            int totalMateriales = await _context.Inventarios.CountAsync();
            int totalBajos = await _context.Inventarios.CountAsync(i => i.StockActual <= i.StockMinimo || i.StockActual <= 15);
            var resumenInventario = await _context.Inventarios.Take(6).ToListAsync();

            StringBuilder general = new StringBuilder();
            general.AppendLine($"📦 **Estado del Inventario ({totalMateriales} referencias, {totalBajos} en stock bajo):**\n");
            foreach (var item in resumenInventario)
            {
                string estado = item.StockActual <= item.StockMinimo ? "⚠️ Alerta" : "✅ Normal";
                general.AppendLine($"• **{item.NombreProducto}:** {item.StockActual} {item.UnidadMedida} ({estado})");
            }
            general.AppendLine("\n💡 *Puedes preguntar: 'stock bajo', 'materiales agotados', 'valor total del inventario' o 'stock de [material]'.*");
            return general.ToString();
        }

        // ==========================================
        // 4. Pedidos
        // ==========================================
        private async Task<string> ConsultarPedidosAsync(string norm)
        {
            // A. Pedidos pendientes o en proceso
            if (ContienePalabra(norm, "pendiente", "pendientes", "en proceso", "por entregar", "curso", "fabricacion"))
            {
                var pedidosPendientes = await _context.Pedidos
                    .Include(p => p.IdClienteNavigation)
                    .Where(p => p.Estado == "Pendiente" || p.Estado == "En proceso")
                    .OrderBy(p => p.FechaEntrega)
                    .Take(5)
                    .ToListAsync();

                if (!pedidosPendientes.Any())
                {
                    return "🎉 ¡Excelente! No hay pedidos pendientes ni en proceso. Todas las órdenes han sido finalizadas o entregadas.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🚚 **Pedidos en Fabricación / Pendientes ({pedidosPendientes.Count}):**\n");
                foreach (var p in pedidosPendientes)
                {
                    string cliente = p.IdClienteNavigation != null ? $"{p.IdClienteNavigation.Nombre} {p.IdClienteNavigation.Apellido}".Trim() : (p.IdClienteNavigation?.NombreEmpresa ?? "Cliente");
                    sb.AppendLine($"• **#{p.CodigoPedido}** - {cliente}\n  Producto: *{p.Producto ?? "Mueble a medida"}* | Entrega: **{p.FechaEntrega:dd/MM/yyyy}** | Estado: **{p.Estado}** | Total: ${p.ValorTotal:N0} COP");
                }
                return sb.ToString();
            }

            // B. Pedidos terminados o entregados
            if (ContienePalabra(norm, "entregado", "entregados", "terminado", "terminados", "finalizados"))
            {
                int entregados = await _context.Pedidos.CountAsync(p => p.Estado == "Entregado");
                int terminados = await _context.Pedidos.CountAsync(p => p.Estado == "Terminado");

                return $"✅ **Pedidos Completados:**\n\n" +
                       $"• 🏁 **Terminados (listos para despacho):** {terminados}\n" +
                       $"• 📦 **Entregados con éxito:** {entregados}";
            }

            // C. Conteo total y métricas
            int total = await _context.Pedidos.CountAsync();
            int pendientes = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente");
            int enProceso = await _context.Pedidos.CountAsync(p => p.Estado == "En proceso");
            int terminadosCount = await _context.Pedidos.CountAsync(p => p.Estado == "Terminado");
            int entregadosCount = await _context.Pedidos.CountAsync(p => p.Estado == "Entregado");
            decimal valorTotalPedidos = await _context.Pedidos.SumAsync(p => (decimal?)p.ValorTotal) ?? 0;

            return $"📦 **Métricas de Pedidos de Fabricación:**\n\n" +
                   $"• **Total de pedidos registrados:** {total}\n" +
                   $"• 🟡 **Pendientes:** {pendientes}\n" +
                   $"• 🔵 **En proceso (taller):** {enProceso}\n" +
                   $"• 🟢 **Terminados:** {terminadosCount}\n" +
                   $"• 🚚 **Entregados:** {entregadosCount}\n" +
                   $"• 💵 **Valor acumulado de pedidos:** ${valorTotalPedidos:N0} COP\n\n" +
                   $"💡 *Para consultar una orden puntual, escribe su código como 'PED-2026-001' o pregunta por 'pedidos pendientes'.*";
        }

        // ==========================================
        // 5. Facturas y Ventas
        // ==========================================
        private async Task<string> ConsultarFacturasAsync(string norm)
        {
            // A. Facturación total
            if (ContienePalabra(norm, "total", "cuanto se ha facturado", "ingresos", "ganancias", "recaudo", "dinero"))
            {
                decimal total = await _context.Facturas.SumAsync(f => (decimal?)f.Total) ?? 0;
                decimal subtotal = await _context.Facturas.SumAsync(f => (decimal?)f.Subtotal) ?? 0;
                decimal totalIva = await _context.Facturas.SumAsync(f => (decimal?)f.IVA) ?? 0;
                int cantidadFacturas = await _context.Facturas.CountAsync();

                return $"💰 **Finanzas y Facturación CARPINTEC:**\n\n" +
                       $"• **Total Facturado Histórico:** ${total:N0} COP\n" +
                       $"• **Subtotal neto:** ${subtotal:N0} COP\n" +
                       $"• **IVA recaudado:** ${totalIva:N0} COP\n" +
                       $"• **Cantidad de facturas emitidas:** {cantidadFacturas}\n\n" +
                       $"💡 *Puedes consultar las 'facturas pendientes de cobro' para ver saldos por recaudar.*";
            }

            // B. Facturas pendientes de cobro
            if (ContienePalabra(norm, "pendiente", "pendientes", "por cobrar", "deben", "sin pagar"))
            {
                var pendientes = await _context.Facturas
                    .Where(f => f.Estado == "Pendiente" || f.Estado == "Borrador")
                    .Take(5)
                    .ToListAsync();

                if (!pendientes.Any())
                {
                    return "✅ ¡Al día! No se registran facturas pendientes de cobro en este momento.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📄 **Facturas Pendientes de Cobro ({pendientes.Count}):**\n");
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
            listado.AppendLine("📋 **Últimas Facturas Registradas:**\n");
            foreach (var f in ultimas)
            {
                listado.AppendLine($"• **{f.Folio}** - {f.Cliente} | ${f.Total:N0} COP | Estado: **{f.Estado}** | Fecha: {f.Fecha:dd/MM/yyyy}");
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

            if (ContienePalabra(norm, "pendiente", "pendientes", "por aprobar"))
            {
                var cotPendientes = await _context.Cotizaciones
                    .Include(c => c.IdClienteNavigation)
                    .Where(c => c.Estado == "Pendiente")
                    .Take(5)
                    .ToListAsync();

                if (!cotPendientes.Any())
                {
                    return "✅ No hay cotizaciones pendientes en este momento. Todas han sido aprobadas o tramitadas.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📄 **Cotizaciones Pendientes de Aprobación ({cotPendientes.Count}):**\n");
                foreach (var c in cotPendientes)
                {
                    string cliente = c.NombreCliente ?? (c.IdClienteNavigation != null ? $"{c.IdClienteNavigation.Nombre} {c.IdClienteNavigation.Apellido}" : "Cliente");
                    sb.AppendLine($"• **Folio #{c.Folio}:** {cliente} | Monto: ${c.Total:N0} COP | Mueble: {c.DetalleProducto ?? "Personalizado"} | Fecha: {c.Fecha:dd/MM/yyyy}");
                }
                return sb.ToString();
            }

            return $"📄 **Métricas de Cotizaciones:**\n\n" +
                   $"• **Total cotizaciones registradas:** {total}\n" +
                   $"• 🟡 **Cotizaciones pendientes:** {pendientes}\n" +
                   $"• 🟢 **Cotizaciones aprobadas:** {aprobadas}\n" +
                   $"• 💵 **Monto total cotizado acumulado:** ${totalCotizado:N0} COP\n\n" +
                   $"💡 *Para ver una en específico, escribe su folio (ej: 'folio COT-001') o consulta 'cotizaciones pendientes'.*";
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

            if (ContienePalabra(norm, "pendiente", "pendientes", "sin responder", "urgente", "quejas"))
            {
                var listaPend = await _context.Pqrs
                    .Include(p => p.IdClienteNavigation)
                    .Where(p => p.Estado == "Pendiente" || p.Estado == "En proceso")
                    .Take(5)
                    .ToListAsync();

                if (!listaPend.Any())
                {
                    return "🎉 ¡Excelente! No hay peticiones, quejas o reclamos (PQR) pendientes. Todas han sido atendidas a tiempo.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📩 **PQRs Pendientes de Atención ({listaPend.Count}):**\n");
                foreach (var p in listaPend)
                {
                    string cliente = p.IdClienteNavigation != null ? $"{p.IdClienteNavigation.Nombre} {p.IdClienteNavigation.Apellido}".Trim() : "Cliente";
                    sb.AppendLine($"• **#{p.CodigoPqr}** ({p.Tipo}) - {cliente}\n  Asunto: *{p.Asunto}* | Estado: **{p.Estado}** | Fecha: {p.FechaRegistro:dd/MM/yyyy}");
                }
                return sb.ToString();
            }

            return $"📩 **Gestión de PQR (Total: {total}):**\n\n" +
                   $"• 🟡 **Pendientes:** {pendientes}\n" +
                   $"• 🔵 **En proceso:** {enProceso}\n" +
                   $"• 🟢 **Respondidas / Cerradas:** {resueltas}\n\n" +
                   $"💡 *Puedes consultar un radicado escribiendo por ejemplo 'pqr PQR-001' o preguntar por 'pqr pendientes'.*";
        }

        // ==========================================
        // 8. Clientes
        // ==========================================
        private async Task<string> ConsultarClientesAsync(string norm)
        {
            // A. Últimos clientes registrados
            if (ContienePalabra(norm, "ultimos", "recientes", "nuevos", "ultimo"))
            {
                var ultimos = await _context.Clientes
                    .OrderByDescending(c => c.FechaRegistro ?? DateTime.MinValue)
                    .Take(5)
                    .ToListAsync();

                if (ultimos.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("👥 **Últimos Clientes Registrados:**\n");
                    foreach (var c in ultimos)
                    {
                        string nombre = c.TipoCliente == "Empresa" ? c.NombreEmpresa ?? "Empresa" : $"{c.Nombre} {c.Apellido}".Trim();
                        sb.AppendLine($"• **{nombre}** ({c.TipoCliente}) | Tel: {c.Telefono} | Estado: **{c.Estado}** | Registrado: {(c.FechaRegistro.HasValue ? c.FechaRegistro.Value.ToString("dd/MM/yyyy") : "N/A")}");
                    }
                    return sb.ToString();
                }
            }

            // B. Búsqueda por nombre, empresa, documento o correo
            string termino = ExtraerTerminoBusqueda(norm, "cliente", "clientes", "buscar cliente", "datos de", "contacto de", "empresa", "documento", "cedula");
            if (!string.IsNullOrWhiteSpace(termino) && termino.Length >= 3 && !ContienePalabra(termino, "total", "cuantos", "inactivos", "activos", "todos"))
            {
                var encontrados = await _context.Clientes
                    .Where(c => (c.Nombre != null && c.Nombre.ToLower().Contains(termino)) ||
                                (c.Apellido != null && c.Apellido.ToLower().Contains(termino)) ||
                                (c.NombreEmpresa != null && c.NombreEmpresa.ToLower().Contains(termino)) ||
                                (c.Documento != null && c.Documento.Contains(termino)) ||
                                (c.Correo != null && c.Correo.ToLower().Contains(termino)))
                    .Take(5)
                    .ToListAsync();

                if (encontrados.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"👥 **Clientes Encontrados para '{termino}':**\n");
                    foreach (var c in encontrados)
                    {
                        string nombreCompleto = c.TipoCliente == "Empresa" ? c.NombreEmpresa ?? "Empresa" : $"{c.Nombre} {c.Apellido}".Trim();
                        sb.AppendLine($"• **{nombreCompleto}** ({c.TipoCliente})\n  Doc: {c.Documento ?? "N/A"} | Tel: {c.Telefono} | Correo: {c.Correo}\n  Ciudad: {c.Ciudad ?? "No especificada"} | Estado: **{c.Estado}**");
                    }
                    return sb.ToString();
                }
            }

            // C. Conteo general y métricas
            int total = await _context.Clientes.CountAsync();
            int activos = await _context.Clientes.CountAsync(c => c.Estado == "Activo");
            int inactivos = await _context.Clientes.CountAsync(c => c.Estado == "Inactivo");
            int empresas = await _context.Clientes.CountAsync(c => c.TipoCliente == "Empresa");
            int personas = total - empresas;

            return $"👥 **Directorio de Clientes CARPINTEC:**\n\n" +
                   $"• **Total clientes registrados:** {total}\n" +
                   $"• 🟢 **Clientes activos:** {activos}\n" +
                   $"• ⚪ **Clientes inactivos:** {inactivos}\n" +
                   $"• 🏢 **Empresas:** {empresas}\n" +
                   $"• 👤 **Personas Naturales:** {personas}\n\n" +
                   $"💡 *Para buscar un cliente puntual, escribe: 'buscar cliente [nombre o cédula]' o consulta 'últimos clientes'. Si quieres saber cómo registrar uno, pregunta '¿Cómo añadir un cliente?'.*";
        }

        // ==========================================
        // 9. Empleados y Mano de Obra
        // ==========================================
        private async Task<string> ConsultarEmpleadosAsync(string norm)
        {
            int total = await _context.Empleados.CountAsync();
            int activos = await _context.Empleados.CountAsync(e => e.Estado == "Activo");
            int inactivos = await _context.Empleados.CountAsync(e => e.Estado == "Inactivo");

            // Búsqueda por cargo
            if (ContienePalabra(norm, "ebanista", "ebanistas", "carpintero", "carpinteros", "cargo", "cargos", "disenador", "taller"))
            {
                var empleadosPorCargo = await _context.Empleados
                    .Where(e => e.Estado == "Activo")
                    .Take(8)
                    .ToListAsync();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"👷 **Equipo de Producción y Taller ({activos} activos):**\n");
                foreach (var e in empleadosPorCargo)
                {
                    sb.AppendLine($"• **{e.Nombre} {e.Apellido}:** Cargo: *{e.Cargo}* | Tel: {e.Telefono ?? "N/A"}");
                }
                return sb.ToString();
            }

            // Búsqueda específica por nombre o cédula
            string termino = ExtraerTerminoBusqueda(norm, "empleado", "empleados", "buscar empleado", "cedula");
            if (!string.IsNullOrWhiteSpace(termino) && termino.Length >= 3 && !ContienePalabra(termino, "total", "cuantos", "todos"))
            {
                var encontrados = await _context.Empleados
                    .Where(e => (e.Nombre != null && e.Nombre.ToLower().Contains(termino)) ||
                                (e.Apellido != null && e.Apellido.ToLower().Contains(termino)) ||
                                (e.Documento != null && e.Documento.Contains(termino)) ||
                                (e.Cargo != null && e.Cargo.ToLower().Contains(termino)))
                    .Take(5)
                    .ToListAsync();

                if (encontrados.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"👷 **Empleados Encontrados para '{termino}':**\n");
                    foreach (var e in encontrados)
                    {
                        sb.AppendLine($"• **{e.Nombre} {e.Apellido}** (Doc: {e.Documento})\n  Cargo: *{e.Cargo}* | Estado: **{e.Estado}** | Tel: {e.Telefono ?? "N/A"}");
                    }
                    return sb.ToString();
                }
            }

            var empleados = await _context.Empleados.Take(6).ToListAsync();
            StringBuilder gen = new StringBuilder();
            gen.AppendLine($"👷 **Nómina y Personal CARPINTEC:**\n\n" +
                           $"• **Total empleados registrados:** {total}\n" +
                           $"• 🟢 **Activos:** {activos} | ⚪ **Inactivos:** {inactivos}\n\n" +
                           $"**Integrantes destacados:**\n");
            foreach (var e in empleados)
            {
                gen.AppendLine($"• {e.Nombre} {e.Apellido} — *{e.Cargo}* ({e.Estado})");
            }
            gen.AppendLine("\n💡 *Puedes buscar un colaborador con 'buscar empleado [nombre]' o preguntar '¿Cómo registrar un empleado?'.*");
            return gen.ToString();
        }

        // ==========================================
        // 10. Usuarios del Sistema y Seguridad
        // ==========================================
        private async Task<string> ConsultarUsuariosAsync(string norm)
        {
            int total = await _context.Usuarios.CountAsync();
            int adminCount = await _context.Usuarios.CountAsync(u => u.Rol == "Administrador");
            int empleadoCount = await _context.Usuarios.CountAsync(u => u.Rol == "Empleado");
            int clienteCount = await _context.Usuarios.CountAsync(u => u.Rol == "Cliente");
            int bloqueados = await _context.Usuarios.CountAsync(u => u.IntentosFallidos >= 3 || u.Estado == "Inactivo" || u.Estado == "Bloqueado");

            // Si pregunta por usuarios bloqueados
            if (ContienePalabra(norm, "bloqueado", "bloqueados", "intentos fallidos", "bloqueo"))
            {
                var listaBloqueados = await _context.Usuarios
                    .Where(u => u.IntentosFallidos >= 3 || u.Estado == "Inactivo" || u.Estado == "Bloqueado")
                    .Take(5)
                    .ToListAsync();

                if (!listaBloqueados.Any())
                {
                    return "🛡️ ¡Excelente! No hay usuarios bloqueados ni con exceso de intentos fallidos en este momento.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🔒 **Usuarios Bloqueados o Inactivos ({listaBloqueados.Count}):**\n");
                foreach (var u in listaBloqueados)
                {
                    sb.AppendLine($"• **{u.Nombre} {u.Apellido}** ({u.Rol})\n  Correo: {u.Correo} | Intentos fallidos: {u.IntentosFallidos} | Estado: **{u.Estado}**");
                }
                sb.AppendLine("\n💡 *El administrador puede desbloquearlos desde Gestión de usuarios reiniciando sus intentos a 0.*");
                return sb.ToString();
            }

            return $"👥 **Usuarios del Sistema CARPINTEC (Total: {total}):**\n\n" +
                   $"• 👑 **Administradores:** {adminCount}\n" +
                   $"• 👷 **Cuentas de Empleados:** {empleadoCount}\n" +
                   $"• 👤 **Cuentas de Clientes:** {clienteCount}\n" +
                   $"• 🔒 **Usuarios bloqueados/inactivos:** {bloqueados}\n\n" +
                   $"💡 *Pregunta por 'usuarios bloqueados' para revisar cuentas con problemas de acceso.*";
        }

        // ==========================================
        // 11. Productos y Catálogo
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
                           $"• **Material:** {topCaro.Material ?? "Madera fina"}\n" +
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
                    return $"🏷️ **Producto más accesible del catálogo:**\n\n" +
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
                    sb.AppendLine($"🪑 **Productos Encontrados para '{termino}':**\n");
                    foreach (var p in encontrados)
                    {
                        sb.AppendLine($"• **{p.Nombre}:** ${p.Precio:N0} COP | Categoría: *{p.Categoria ?? "General"}* | Medidas: {p.Medidas ?? "A medida"}");
                    }
                    return sb.ToString();
                }
            }

            // C. Conteo y catálogo general
            int total = await _context.Productos.CountAsync();
            int activos = await _context.Productos.CountAsync(p => p.Estado == "Activo");
            var catalogo = await _context.Productos.Where(p => p.Estado == "Activo").Take(6).ToListAsync();

            StringBuilder cat = new StringBuilder();
            cat.AppendLine($"🪑 **Catálogo de Productos CARPINTEC ({activos} activos de {total} totales):**\n");
            foreach (var p in catalogo)
            {
                cat.AppendLine($"• **{p.Nombre}:** ${p.Precio:N0} COP — *{p.Categoria ?? "General"}*");
            }
            cat.AppendLine("\n💡 *Puedes consultar precios de productos concretos como 'precio de cocina integral' o preguntar '¿Cómo crear un producto?'.*");
            return cat.ToString();
        }

        // ==========================================
        // 12. Coincidencias directas y extracción
        // ==========================================
        private async Task<string> BuscarCoincidenciaDirectaAsync(string original, string norm)
        {
            if (norm.Length < 3) return string.Empty;

            // Intentar buscar en productos
            var prod = await _context.Productos
                .FirstOrDefaultAsync(p => p.Nombre.ToLower().Contains(norm));
            if (prod != null)
            {
                return $"🪑 **Producto encontrado en catálogo:**\n\n" +
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
                       $"• **Stock mínimo:** {inv.StockMinimo} {inv.UnidadMedida}\n" +
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
                string nombre = cli.TipoCliente == "Empresa" ? cli.NombreEmpresa ?? "Empresa" : $"{cli.Nombre} {cli.Apellido}".Trim();
                return $"👤 **Cliente encontrado en base de datos:**\n\n" +
                       $"• **Nombre:** {nombre} ({cli.TipoCliente})\n" +
                       $"• **Documento:** {cli.Documento ?? "N/A"}\n" +
                       $"• **Teléfono:** {cli.Telefono}\n" +
                       $"• **Correo:** {cli.Correo}\n" +
                       $"• **Estado:** {cli.Estado}";
            }

            // Intentar buscar en empleados
            var emp = await _context.Empleados
                .FirstOrDefaultAsync(e => (e.Nombre != null && e.Nombre.ToLower().Contains(norm)) ||
                                          (e.Apellido != null && e.Apellido.ToLower().Contains(norm)));
            if (emp != null)
            {
                return $"👷 **Empleado encontrado:**\n\n" +
                       $"• **{emp.Nombre} {emp.Apellido}**\n" +
                       $"• **Cargo:** {emp.Cargo}\n" +
                       $"• **Documento:** {emp.Documento}\n" +
                       $"• **Teléfono:** {emp.Telefono ?? "N/A"}\n" +
                       $"• **Estado:** {emp.Estado}";
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
            return "👋 ¡Hola! Soy tu **Asistente Virtual de CARPINTEC** 🪵\n\n" +
                   "Estoy conectado directamente a la base de datos para resolver tus dudas y orientarte en el manejo del sistema.\n\n" +
                   "**¿En qué te puedo apoyar?**\n" +
                   "• ➕ *'¿Cómo añadir un cliente?'* (te explico los campos paso a paso)\n" +
                   "• 📊 *'Resumen general'* (balance de clientes, pedidos, facturas y stock)\n" +
                   "• ⚠️ *'Stock bajo'* (materiales que requieren reposición)\n" +
                   "• 🚚 *'Pedidos pendientes'* (entregas en curso en taller)\n" +
                   "• 💰 *'Total facturado'* (ventas y cobros)\n" +
                   "• 👥 *'Directorio de clientes'* o buscar por nombre\n" +
                   "• 📩 *'PQR pendientes'* (quejas o solicitudes de clientes)\n\n" +
                   "¡Escribe tu pregunta o haz clic en las opciones rápidas abajo!";
        }

        private static string GenerarAgradecimiento()
        {
            return "🪵 ¡Con gusto! Si requieres consultar algo más de la base de datos o necesitas otra guía sobre el sistema, aquí estaré para ayudarte. ¡Muchos éxitos en la jornada de hoy!";
        }

        private static string GenerarMenuAyuda()
        {
            return "🤖 **¿Qué puedes preguntarme?**\n\n" +
                   "📘 **Guías y Procedimientos del Sistema:**\n" +
                   "  • *'¿Cómo añadir un cliente?'* (explicación de cada campo)\n" +
                   "  • *'¿Cómo crear una cotización?'*\n" +
                   "  • *'¿Cómo crear un pedido?'*\n" +
                   "  • *'¿Cómo registrar un empleado?'*\n" +
                   "  • *'¿Cómo generar una factura?'*\n" +
                   "  • *'¿Cómo gestionar usuarios o desbloquearlos?'*\n\n" +
                   "🗄️ **Consultas a la Base de Datos:**\n" +
                   "  • *'¿Cuántos clientes tenemos?'* o *'Buscar cliente Carlos'*\n" +
                   "  • *'¿Qué productos tienen stock bajo?'* o *'Valor del inventario'*\n" +
                   "  • *'¿Qué pedidos están pendientes?'* o por código *'PED-2026-001'*\n" +
                   "  • *'¿Cuánto se ha facturado?'* o *'Facturas pendientes de cobro'*\n" +
                   "  • *'¿Cuántos empleados hay en taller?'*\n" +
                   "  • *'¿Hay PQR pendientes?'*\n" +
                   "  • *'Resumen general'* (vista ejecutiva completa)";
        }

        private static string GenerarRespuestaPorDefecto(string pregunta)
        {
            return $"🤔 No encontré una coincidencia exacta para *\"{pregunta}\"* en la base de datos ni en las guías operativas.\n\n" +
                   $"**Puedes probar preguntando:**\n" +
                   $"• ➕ *'¿Cómo añadir un cliente?'* (o cotización, pedido, empleado)\n" +
                   $"• ⚠️ *'Stock bajo'* (materiales críticos)\n" +
                   $"• 🚚 *'Pedidos pendientes'*\n" +
                   $"• 👥 *'Buscar cliente [nombre o cédula]'*\n" +
                   $"• 💰 *'Total facturado'*\n" +
                   $"• 🪑 *'Precio de [mueble]'*\n" +
                   $"• 📊 *'Resumen general'*\n\n" +
                   $"O escribe *'ayuda'* para ver todas las preguntas que puedo responder.";
        }

        #endregion
    }
    /* FIN: Servicio Inteligente de Chatbot conectado a la Base de Datos y Guías de Usuario */
}