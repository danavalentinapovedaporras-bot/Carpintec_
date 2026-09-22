using System;
using System.Collections.Generic;

namespace CARPINTEC_App.Filters
{
    public static class PermisosCargoHelper
    {
        // Matriz oficial de módulos por cargo según especificación de CARPINTEC
        private static readonly Dictionary<string, HashSet<string>> ModulosPorCargo =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Supervisor"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Clientes", "Cotizaciones", "Pedidos", "Diseños", "Producción", "Inventario", "Reportes"
                },
                ["Asesor comercial"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Clientes", "Cotizaciones", "Pedidos", "Ventas"
                },
                ["Diseñador de muebles"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Clientes", "Diseños", "Cotizaciones", "Pedidos"
                },
                ["Jefe de producción"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Pedidos", "Producción", "Inventario", "Reportes"
                },
                ["Operario de carpintería"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Producción", "Mis tareas", "Pedidos"
                },
                ["Encargado de inventario"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    "Inicio", "Inventario", "Materiales", "Entradas", "Salidas", "Reportes"
                }
            };

        public static string NormalizarCargo(string? cargo)
        {
            if (string.IsNullOrWhiteSpace(cargo)) return "Supervisor";

            string c = cargo.Trim().ToLowerInvariant()
                .Replace("ó", "o").Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ú", "u").Replace("ñ", "n");

            if (c.Contains("supervisor")) return "Supervisor";
            if (c.Contains("asesor") || c.Contains("comercial")) return "Asesor comercial";
            if (c.Contains("disen")) return "Diseñador de muebles";
            if (c.Contains("jefe")) return "Jefe de producción";
            if (c.Contains("operario") || c.Contains("carpinter")) return "Operario de carpintería";
            if (c.Contains("inventario") || c.Contains("bodega") || c.Contains("logist")) return "Encargado de inventario";

            return cargo.Trim();
        }

        public static bool TieneAcceso(string? cargo, string modulo)
        {
            if (string.IsNullOrWhiteSpace(modulo)) return true;
            if (modulo.Equals("Inicio", StringComparison.OrdinalIgnoreCase)) return true;

            string cargoOficial = NormalizarCargo(cargo);

            if (ModulosPorCargo.TryGetValue(cargoOficial, out var modulos))
            {
                // Coincidencia directa
                if (modulos.Contains(modulo)) return true;

                // Mapeo de Productos a Diseños o Materiales
                if (modulo.Equals("Productos", StringComparison.OrdinalIgnoreCase) &&
                    (modulos.Contains("Diseños") || modulos.Contains("Materiales")))
                {
                    return true;
                }

                // Mapeo de Calendario a Producción
                if (modulo.Equals("Calendario", StringComparison.OrdinalIgnoreCase) && modulos.Contains("Producción"))
                {
                    return true;
                }

                // Mapeo de ManoObra a Producción o Tareas
                if (modulo.Equals("ManoObra", StringComparison.OrdinalIgnoreCase) &&
                    (modulos.Contains("Producción") || modulos.Contains("Mis tareas")))
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        public static HashSet<string> ObtenerModulos(string? cargo)
        {
            string cargoOficial = NormalizarCargo(cargo);
            if (ModulosPorCargo.TryGetValue(cargoOficial, out var modulos))
            {
                return modulos;
            }
            return ModulosPorCargo["Supervisor"];
        }
    }
}
