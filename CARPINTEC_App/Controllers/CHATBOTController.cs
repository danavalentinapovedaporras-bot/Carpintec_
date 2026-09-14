using Microsoft.AspNetCore.Mvc;
using CARPINTEC_App.Models;
using CARPINTEC_App.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace CARPINTEC_App.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly CarpintecContext _context;

        public ChatbotController(CarpintecContext context)
        {
            _context = context;
        }


        [HttpPost]
        public async Task<IActionResult> Preguntar([FromBody] MensajeChatbot mensaje)
        {

            string pregunta = mensaje.Mensaje.ToLower().Trim();

            string respuesta = "";

            if (pregunta.Contains("hola") || pregunta.Contains("buenas"))
            {
                respuesta =
                "👋 ¡Hola! Bienvenido a CARPINTEC 🪵\n\n" +
                "Soy tu asistente virtual. ¿Cómo puedo ayudarte?\n\n" +
                "Selecciona una opción:\n\n" +
                "🪑 1. Productos disponibles\n" +
                "🏠 2. Servicios de carpintería\n" +
                "📄 3. Solicitar una cotización\n" +
                "📦 4. Consultar mi pedido\n" +
                "👨‍💼 5. Contactar un asesor\n\n" +
                "Escribe el número de la opción.";
            }



            else if (pregunta.Contains("servicio") ||
                     pregunta.Contains("servicios"))
            {
                respuesta = "Ofrecemos cocinas integrales, closets, muebles personalizados, escritorios, puertas y centros de entretenimiento.";
            }


            else if (pregunta.Contains("producto") ||
                     pregunta.Contains("productos") ||
                     pregunta.Contains("mueble"))
            {

                var productos = await _context.Productos
                    .Where(p => p.Estado == "Activo")
                    .Take(5)
                    .ToListAsync();


                if (productos.Count > 0)
                {
                    respuesta = "Estos son algunos productos disponibles:\n";

                    foreach (var producto in productos)
                    {
                        respuesta +=
                        $"- {producto.Nombre} | Precio: ${producto.Precio}\n";
                    }
                }
                else
                {
                    respuesta = "Actualmente no hay productos disponibles.";
                }

            }


            else if (pregunta.Contains("pedido") ||
                     pregunta.Contains("pedidos"))
            {

                var pedidos = await _context.Pedidos
                    .Take(5)
                    .ToListAsync();


                if (pedidos.Count > 0)
                {
                    respuesta = "Estos son algunos pedidos registrados:\n";

                    foreach (var pedido in pedidos)
                    {
                        respuesta +=
                        $"- Pedido: {pedido.CodigoPedido} | Estado: {pedido.Estado}\n";
                    }
                }
                else
                {
                    respuesta = "Actualmente no hay pedidos registrados.";
                }

            }


            else if (pregunta.Contains("cotizacion") ||
                     pregunta.Contains("cotización") ||
                     pregunta.Contains("cotizaciones"))
            {

                var cotizaciones = await _context.Cotizaciones
                    .Take(5)
                    .ToListAsync();


                if (cotizaciones.Count > 0)
                {
                    respuesta = "Estas son algunas cotizaciones registradas:\n";


                    foreach (var cotizacion in cotizaciones)
                    {
                        respuesta +=
                        $"- Folio: {cotizacion.Folio} | Estado: {cotizacion.Estado} | Total: ${cotizacion.Total}\n";
                    }
                }
                else
                {
                    respuesta = "Actualmente no hay cotizaciones registradas.";
                }

            }


            else if (pregunta.Contains("cliente") ||
                     pregunta.Contains("clientes"))
            {

                var clientes = await _context.Clientes
                    .Take(5)
                    .ToListAsync();


                if (clientes.Count > 0)
                {
                    respuesta = "Estos son algunos clientes registrados:\n";


                    foreach (var cliente in clientes)
                    {

                        if (cliente.TipoCliente == "Empresa")
                        {
                            respuesta +=
                            $"- Empresa: {cliente.NombreEmpresa} | Estado: {cliente.Estado}\n";
                        }
                        else
                        {
                            respuesta +=
                            $"- Cliente: {cliente.Nombre} {cliente.Apellido} | Estado: {cliente.Estado}\n";
                        }

                    }
                }
                else
                {
                    respuesta = "Actualmente no hay clientes registrados.";
                }

            }


            else if (pregunta.Contains("inventario") ||
                     pregunta.Contains("stock") ||
                     pregunta.Contains("disponible"))
            {

                var inventario = await _context.Inventarios
                    .Include(i => i.IdProductoNavigation)
                    .Take(5)
                    .ToListAsync();


                if (inventario.Count > 0)
                {
                    respuesta = "Productos disponibles en inventario:\n";


                    foreach (var item in inventario)
                    {
                        respuesta +=
                        $"- Producto: {item.IdProductoNavigation.Nombre} | Stock: {item.StockActual} | Estado: {item.Estado}\n";
                    }

                }
                else
                {
                    respuesta = "No hay productos registrados en inventario.";
                }

            }


            else if (pregunta.Contains("factura") ||
         pregunta.Contains("facturas"))
            {
                var facturas = await _context.Facturas
                    .Take(5)
                    .ToListAsync();


                if (facturas.Count > 0)
                {
                    respuesta = "Facturas encontradas:\n";

                    foreach (var factura in facturas)
                    {
                        respuesta +=
                        $"- Factura: {factura.Folio} | Cliente: {factura.Cliente} | Total: ${factura.Total} | Estado: {factura.Estado}\n";
                    }

                }
                else
                {
                    respuesta = "Actualmente no hay facturas registradas.";
                }
            }


            // Usuario conectado por JWT

            var idUsuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            Console.WriteLine("Usuario JWT: " + idUsuario);



            // Guardar conversación
            ChatBot chat = new ChatBot
            {
                IdUsuario = idUsuario != null ? int.Parse(idUsuario) : null,

                MensajeUsuario = mensaje.Mensaje,

                RespuestaBot = respuesta,

                Fecha = DateTime.Now
            };


            _context.ChatBots.Add(chat);

            await _context.SaveChangesAsync();
            return Json(new
            {
                respuesta = respuesta
            });

        }
    }
}