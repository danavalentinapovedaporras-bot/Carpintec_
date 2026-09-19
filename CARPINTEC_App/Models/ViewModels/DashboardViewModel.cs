using CARPINTEC_App.Models;

namespace CARPINTEC_App.Models.ViewModels
{
    public class DashboardViewModel
    {
        // Cantidad total de usuarios registrados
        public int TotalUsuarios { get; set; }


        // Cantidad total de empleados
        public int TotalEmpleados { get; set; }


        // Cantidad total de clientes
        public int TotalClientes { get; set; }


        // Cantidad total de productos
        public int TotalProductos { get; set; }


        // Cantidad de pedidos activos
        public int PedidosActivos { get; set; }


        // Cantidad de cotizaciones pendientes
        public int CotizacionesPendientes { get; set; }


        // Total de ventas del mes
        public decimal VentasMes { get; set; }

        public List<Pedido> UltimosPedidos { get; set; } = new List<Pedido>();

        public List<Pedido> ProyectosProduccion { get; set; } = new List<Pedido>();

        public List<ActividadRecienteViewModel> ActividadesRecientes { get; set; } = new List<ActividadRecienteViewModel>();
    }
}