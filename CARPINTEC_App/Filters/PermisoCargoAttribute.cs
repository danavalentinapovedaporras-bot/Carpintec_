using System;
using System.Threading.Tasks;
using CARPINTEC_App.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CARPINTEC_App.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PermisoCargoAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _modulo;

        public PermisoCargoAttribute(string modulo)
        {
            _modulo = modulo;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            // 1. Administrador siempre tiene acceso a todos los módulos
            if (user.IsInRole("Administrador"))
            {
                await next();
                return;
            }

            // 2. Si es Cliente, redirigir al portal de clientes
            if (user.IsInRole("Cliente"))
            {
                context.Result = new RedirectToActionResult("Index", "DashboardCliente", null);
                return;
            }

            // 3. Si es Empleado, verificar su cargo
            if (user.IsInRole("Empleado"))
            {
                string? cargo = context.HttpContext.Session.GetString("Cargo");

                // Si no está en sesión, consultarlo de la base de datos
                if (string.IsNullOrWhiteSpace(cargo))
                {
                    int? idUsuario = context.HttpContext.Session.GetInt32("IdUsuario");
                    if (idUsuario.HasValue)
                    {
                        var db = context.HttpContext.RequestServices.GetRequiredService<CarpintecContext>();
                        var emp = await db.Empleados.FirstOrDefaultAsync(e => e.IdUsuario == idUsuario.Value);
                        if (emp != null && !string.IsNullOrWhiteSpace(emp.Cargo))
                        {
                            cargo = emp.Cargo.Trim();
                            context.HttpContext.Session.SetString("Cargo", cargo);
                        }
                    }
                }

                cargo ??= "Empleado de Taller";

                // Validar acceso según el cargo
                if (!PermisosCargoHelper.TieneAcceso(cargo, _modulo))
                {
                    if (context.Controller is Controller controller)
                    {
                        controller.TempData["Error"] = $"El cargo '{cargo}' no tiene permiso para ingresar al módulo de {_modulo}.";
                    }

                    context.Result = new RedirectToActionResult("Index", "DashboardEmpleado", null);
                    return;
                }

                await next();
                return;
            }

            // Si no tiene rol reconocido, dejar continuar a la autorización normal
            await next();
        }
    }
}
