using System;

namespace CARPINTEC_App.Services
{
    public class ChatbotService
    {
        public string Responder(string mensaje)
        {
            mensaje = mensaje.ToLower();

            if (mensaje.Contains("hola"))
            {
                return "¡Hola! Soy el asistente virtual de CARPINTEC. ¿En qué puedo ayudarte?";
            }

            if (mensaje.Contains("producto"))
            {
                return "Puedes consultar nuestro catálogo de productos de carpintería.";
            }

            if (mensaje.Contains("pedido"))
            {
                return "Puedes consultar el estado de tu pedido desde el módulo de pedidos.";
            }

            if (mensaje.Contains("cotizacion") || mensaje.Contains("cotización"))
            {
                return "Puedes generar una cotización desde el módulo de cotizaciones.";
            }

            if (mensaje.Contains("pqr"))
            {
                return "Puedes registrar una petición, queja o reclamo en el módulo PQR.";
            }

            return "No entendí tu solicitud. Puedes preguntarme por productos, pedidos, cotizaciones o PQR.";
        }
    }
}