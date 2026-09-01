

    using System;

    namespace CARPINTEC_App.Models
    {
    public class AsignacionManoObra
    {
        public int Id { get; set; }
        public required string PedidoAsociado { get; set; }
        public required string EmpleadoResponsable { get; set; }
        public string? ProductoProyecto { get; set; } // Si puede ir vacío, usa el ?
        public required string TipoDeActividad { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFinalizacion { get; set; }
        public decimal HorasTrabajadas { get; set; }
        public decimal CostoPorHora { get; set; }
        public decimal CostoTotal { get; set; }
        public required string Estado { get; set; }
        public string? Observaciones { get; set; }
    }
}