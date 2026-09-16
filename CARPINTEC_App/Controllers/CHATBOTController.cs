using Microsoft.AspNetCore.Mvc;
using CARPINTEC_App.Models;
using CARPINTEC_App.Data;
using CARPINTEC_App.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    /// <summary>
    /// Controlador que gestiona los dos chatbots independientes de CARPINTEC:
    /// 1. Chatbot Administrativo: Atiende a los usuarios con sesión iniciada en el panel administrativo (_Layout.cshtml),
    ///    conectado a la base de datos completa (clientes, inventario, pedidos, cotizaciones, etc.).
    /// 2. Chatbot Público: Atiende exclusivamente a visitantes de la página web (Home/Index.cshtml) con información
    ///    institucional, servicios, contacto y la regla obligatoria de registro para cotizaciones y pedidos.
    /// </summary>
    public class ChatbotController : Controller
    {
        private readonly CarpintecContext _context;
        private readonly ChatbotService _chatbotService;
        private readonly ChatbotPublicoService _chatbotPublicoService;

        public ChatbotController(
            CarpintecContext context,
            ChatbotService chatbotService,
            ChatbotPublicoService chatbotPublicoService)
        {
            _context = context;
            _chatbotService = chatbotService;
            _chatbotPublicoService = chatbotPublicoService;
        }

        // =========================================================================
        // 1. CHATBOT ADMINISTRATIVO (Panel de Administración / Layout)
        // =========================================================================
        /// <summary>
        /// Endpoint exclusivo para el panel de administración.
        /// Consulta la base de datos, métricas ERP y guías paso a paso del sistema.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Preguntar([FromBody] MensajeChatbot mensaje)
        {
            if (mensaje == null || string.IsNullOrWhiteSpace(mensaje.Mensaje))
            {
                return Json(new { respuesta = "Por favor escribe un mensaje o selecciona una de las opciones disponibles." });
            }

            // Procesar respuesta a través del servicio administrativo
            string respuesta = await _chatbotService.ResponderAsync(mensaje.Mensaje);

            // Identificar usuario conectado por JWT o Sesión
            var idUsuarioClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? idUsuario = null;

            if (!string.IsNullOrEmpty(idUsuarioClaim) && int.TryParse(idUsuarioClaim, out int parsedId))
            {
                idUsuario = parsedId;
            }
            else
            {
                idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            }

            // Guardar registro de la conversación en la base de datos
            try
            {
                ChatBot chat = new ChatBot
                {
                    IdUsuario = idUsuario,
                    MensajeUsuario = mensaje.Mensaje,
                    RespuestaBot = respuesta,
                    Fecha = DateTime.Now
                };

                _context.ChatBots.Add(chat);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nota: No se pudo registrar historial del chat administrativo: {ex.Message}");
            }

            return Json(new
            {
                respuesta = respuesta
            });
        }

        // =========================================================================
        // 2. CHATBOT PÚBLICO (Página de Inicio / Visitantes Web)
        // =========================================================================
        /// <summary>
        /// Endpoint exclusivo para visitantes de la página principal (Home/Index.cshtml).
        /// Brinda información general de la empresa, productos, servicios, contacto y
        /// exige inicio de sesión / registro para cotizaciones y pedidos.
        /// No tiene acceso a datos administrativos ni de nómina o ERP.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PreguntarPublico([FromBody] MensajeChatbot mensaje)
        {
            if (mensaje == null || string.IsNullOrWhiteSpace(mensaje.Mensaje))
            {
                return Json(new { respuesta = "👋 ¡Hola! ¿En qué te podemos asesorar sobre CARPINTEC hoy?" });
            }

            // Procesar la consulta mediante el servicio especializado de atención pública
            string respuesta = await _chatbotPublicoService.ResponderPublicoAsync(mensaje.Mensaje);

            // Opcional: Registrar consulta pública sin usuario asociado para métricas de atención
            try
            {
                ChatBot chat = new ChatBot
                {
                    IdUsuario = null, // Visitante anónimo de la página web
                    MensajeUsuario = "[Público] " + mensaje.Mensaje,
                    RespuestaBot = respuesta,
                    Fecha = DateTime.Now
                };

                _context.ChatBots.Add(chat);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nota: No se pudo registrar consulta del chatbot público: {ex.Message}");
            }

            return Json(new
            {
                respuesta = respuesta
            });
        }
    }
}