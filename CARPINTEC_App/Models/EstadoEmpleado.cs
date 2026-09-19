namespace CARPINTEC_App.Models
{
    public class EstadoEmpleado
    {

        public int IdEmpleado { get; set; }

        public string Estado { get; set; } = null!;

        public DateOnly? FechaInicioVacaciones { get; set; }

        public DateOnly? FechaFinVacaciones { get; set; }

        public DateOnly? FechaInicioEstado { get; set; }

        public DateOnly? FechaFinEstado { get; set; }

    }
}