using System;
using System.ComponentModel.DataAnnotations;

namespace CARPINTEC_App.Models.ViewModels
{
    public class UsuarioGestionViewModel
    {
        // ==========================
        // Datos del Usuario
        // ==========================
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido")]
        [StringLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres")]
        public string Correo { get; set; } = string.Empty;

        public string? Contraseña { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio")]
        public string Rol { get; set; } = "Cliente";

        public string Estado { get; set; } = "Activo";

        // ==========================
        // Datos específicos de Empleado
        // ==========================
        public int? IdEmpleado { get; set; }

        public string TipoDocumento { get; set; } = "CC";

        public string? Documento { get; set; }

        public string? Cargo { get; set; }

        public string? Telefono { get; set; }

        public string? Direccion { get; set; }

        public DateOnly? FechaIngreso { get; set; }

        public decimal? Salario { get; set; }

        public string? Foto { get; set; }
    }
}
