namespace CARPINTEC_App.Models
{
    public class PasarelaPagoViewModel
    {
        public int IdVenta { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public string MetodoPago { get; set; } = "Tarjeta"; // "Tarjeta", "PSE", "Nequi", "Daviplata", "Cuotas"
        public int? NumeroCuotas { get; set; }

        // Datos de Dirección y Despacho
        public string? Ciudad { get; set; }
        public string? Municipio { get; set; }
        public string? CodigoPostal { get; set; }
        public string? Direccion { get; set; }
        public string? Localidad { get; set; }
        public string? Piso { get; set; }
        public string? HotelEmpresa { get; set; }
        public string? TipoLugar { get; set; } // Casa, Apto, Hotel, Empresa

        // Datos de Pago específicos
        public string? TitularTarjeta { get; set; }
        public string? BancoPse { get; set; }
        public string? TipoClientePse { get; set; }
        public string? TelefonoNequi { get; set; }
        public string? TelefonoDaviplata { get; set; }
    }
}
