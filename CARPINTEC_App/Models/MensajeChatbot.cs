namespace CARPINTEC_App.Models
{
    public class MensajeChatbot
    {
        public string Mensaje { get; set; } = "";
        
        // Propiedad agregada para recibir la ruta de la vista actual desde el cliente
        public string? ContextoVista { get; set; }
    }
}