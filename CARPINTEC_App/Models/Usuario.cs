using System;
using System.Collections.Generic;

namespace CARPINTEC_App.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Apellido { get; set; } = null!;

    public string? Correo { get; set; }

    public string Contraseña { get; set; } = null!;

    public string? Rol { get; set; }

    public string? Estado { get; set; }

    // NUEVO:
    // Guarda la cantidad de intentos fallidos de inicio de sesión.
    // Se usa para bloquear al usuario después de 3 intentos.
    public int IntentosFallidos { get; set; }

    public virtual ICollection<ActividadTaller> ActividadTallers { get; set; } = new List<ActividadTaller>();

    public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}