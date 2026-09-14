using System;

namespace CARPINTEC_App.Models;

public partial class ChatBot
{
    public int IdChat { get; set; }

    public int? IdUsuario { get; set; }

    public string MensajeUsuario { get; set; } = null!;

    public string RespuestaBot { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public virtual Usuario? Usuario { get; set; }
}