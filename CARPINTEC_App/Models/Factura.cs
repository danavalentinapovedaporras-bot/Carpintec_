using System.ComponentModel.DataAnnotations;

namespace CARPINTEC_App.Models
{
    public class Factura
    {
        [Key]
        public int IdFactura { get; set; }

        [Required]
        public string Folio { get; set; } = string.Empty;

        [Required]
        public string Cliente { get; set; } = string.Empty;
        [Required]
        public DateTime Fecha { get; set; }

        public DateTime? FechaVencimiento { get; set; }
        public string? MetodoPago { get; set; }

        public string? Observaciones { get; set; }

        public decimal? Subtotal { get; set; }

        public decimal? IVA { get; set; }

        public decimal? Descuento { get; set; }

        public decimal? CostoInstalacion { get; set; }

        public decimal? CostoTransporte { get; set; }

        [Required]
        public decimal Total { get; set; }

        [Required]
        public string Estado { get; set; } = string.Empty;
        public virtual ICollection<DetalleFactura> DetallesFactura { get; set; } = new List<DetalleFactura>();

    }
}