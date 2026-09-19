using System;
using System.Collections.Generic;

namespace CARPINTEC_App.Models.ViewModels
{
    public class ReportesViewModel
    {
        // Filtros
        public string PeriodoSeleccionado { get; set; } = "mes"; // "mes", "trimestre", "anio"
        public int AnioSeleccionado { get; set; } = DateTime.Now.Year;
        public List<int> AniosDisponibles { get; set; } = new List<int>();

        // 4 Tarjetas Métricas (KPIs)
        public decimal VentasTotales { get; set; }
        public string VariacionVentas { get; set; } = "+0%";
        public bool VariacionVentasPositiva { get; set; } = true;

        public decimal GananciasNetas { get; set; }
        public string VariacionGanancias { get; set; } = "+0%";
        public bool VariacionGananciasPositiva { get; set; } = true;

        public int PedidosCompletados { get; set; }
        public int CotizacionesPendientes { get; set; }

        // Gráfica de Ventas Mensuales
        public List<VentaMensualItem> VentasPorMes { get; set; } = new List<VentaMensualItem>();

        // Gráfica de Pedidos por Estado
        public int TotalPedidos { get; set; }
        public int PedidosProduccion { get; set; }
        public double PorcentajeProduccion { get; set; }
        public int PedidosEntregados { get; set; }
        public double PorcentajeEntregados { get; set; }
        public int PedidosEnEspera { get; set; }
        public double PorcentajeEnEspera { get; set; }
        public int PedidosCancelados { get; set; }
        public double PorcentajeCancelados { get; set; }

        // Top Productos más solicitados
        public List<TopProductoItem> TopProductos { get; set; } = new List<TopProductoItem>();
    }

    public class VentaMensualItem
    {
        public string Mes { get; set; } = "";
        public int MesNumero { get; set; }
        public int Anio { get; set; }
        public decimal Total { get; set; }
        public int PorcentajeAltura { get; set; }
        public bool EsMesMaximo { get; set; }
    }

    public class TopProductoItem
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; } = "";
        public string? Imagen { get; set; }
        public int TotalPedidos { get; set; }
        public decimal PrecioPromedio { get; set; }
        public int Ranking { get; set; }
        public int PorcentajeBarra { get; set; }
    }
}
