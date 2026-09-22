using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class EmpleadosController : Controller
    {

        private readonly CarpintecContext _context;


        public EmpleadosController(CarpintecContext context)
        {
            _context = context;
        }


        // LISTAR EMPLEADOS CON PAGINACIÓN

        public async Task<IActionResult> Index(int pagina = 1)
        {
            if (User.IsInRole("Empleado"))
            {
                return RedirectToAction("Index", "DashboardEmpleado");
            }

            int registrosPorPagina = 4;


            var totalEmpleados = await _context.Empleados.CountAsync();


            var empleados = await _context.Empleados
                .Skip((pagina - 1) * registrosPorPagina)
                .Take(registrosPorPagina)
                .ToListAsync();



            // Datos de tarjetas superiores

            ViewBag.TotalEmpleados = totalEmpleados;


            ViewBag.EnTaller = await _context.Empleados
                .CountAsync(e => e.Estado == "Activo");


            ViewBag.Vacaciones = await _context.Empleados
                .CountAsync(e => e.Estado == "Vacaciones");

            // Lista de empleados con vacaciones o periodos programados para el componente de calendario
            ViewBag.EmpleadosVacaciones = await _context.Empleados
                .Where(e => e.Estado == "Vacaciones"
                         || (e.FechaInicioVacaciones != null && e.FechaFinVacaciones != null)
                         || (e.FechaInicioEstado != null && e.FechaFinEstado != null))
                .OrderBy(e => e.FechaInicioEstado ?? e.FechaInicioVacaciones)
                .ToListAsync();



            // Datos para paginación

            ViewBag.PaginaActual = pagina;


            ViewBag.TotalPaginas =
                (int)Math.Ceiling((double)totalEmpleados / registrosPorPagina);



            return View(empleados);
        }




        // CREAR EMPLEADO






        // OBTENER DATOS PARA MODAL VER

        [HttpGet]
        public async Task<IActionResult> ObtenerEmpleado(int id)
        {

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.IdEmpleado == id);



            if (empleado == null)
            {
                return NotFound();
            }


            return Json(empleado);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevoEmpleado(Empleado empleado)
        {

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Hay datos inválidos en el formulario";
                return RedirectToAction(nameof(Index));
            }


            // Documento máximo 10 números
            if (empleado.Documento.Length > 10 || !empleado.Documento.All(char.IsDigit))
            {
                TempData["Error"] = "El documento debe tener máximo 10 números";
                return RedirectToAction(nameof(Index));
            }


            // Salario positivo
            if (empleado.Salario < 0)
            {
                TempData["Error"] = "El salario no puede ser negativo";
                return RedirectToAction(nameof(Index));
            }


            // Fecha no muy futura (máximo 1 año adelante)
            if (empleado.FechaIngreso > DateOnly.FromDateTime(DateTime.Now.AddYears(1)))
            {
                TempData["Error"] = "La fecha de ingreso no puede ser más de 1 año en el futuro";
                return RedirectToAction(nameof(Index));
            }


            // Estado inicial
            empleado.Estado = "Activo";


            _context.Empleados.Add(empleado);


            await _context.SaveChangesAsync();


            TempData["Success"] = "Empleado creado correctamente";


            return RedirectToAction(nameof(Index));

        }



        // EDITAR EMPLEADO

        [HttpPost]
        public async Task<IActionResult> EditarEmpleado([FromBody] Empleado empleado)
        {


            var empleadoBD = await _context.Empleados
                .FirstOrDefaultAsync(e => e.IdEmpleado == empleado.IdEmpleado);



            if (empleadoBD == null)
            {
                return NotFound();
            }



            empleadoBD.Documento = empleado.Documento;

            empleadoBD.Nombre = empleado.Nombre;

            empleadoBD.Apellido = empleado.Apellido;

            empleadoBD.Cargo = empleado.Cargo;

            empleadoBD.Correo = empleado.Correo;

            empleadoBD.Telefono = empleado.Telefono;

            empleadoBD.Direccion = empleado.Direccion;

            empleadoBD.Salario = empleado.Salario;

            empleadoBD.FechaInicioVacaciones = empleado.FechaInicioVacaciones;
            empleadoBD.FechaFinVacaciones = empleado.FechaFinVacaciones;
            empleadoBD.FechaInicioEstado = empleado.FechaInicioEstado ?? empleado.FechaInicioVacaciones;
            empleadoBD.FechaFinEstado = empleado.FechaFinEstado ?? empleado.FechaFinVacaciones;

            _context.Update(empleadoBD);

            // Sincronizar con Usuario si existe vinculación
            if (empleadoBD.IdUsuario.HasValue)
            {
                var usuarioBD = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == empleadoBD.IdUsuario.Value);

                if (usuarioBD != null)
                {
                    usuarioBD.Nombre = empleadoBD.Nombre;
                    usuarioBD.Apellido = empleadoBD.Apellido;
                    if (!string.IsNullOrWhiteSpace(empleadoBD.Correo))
                    {
                        usuarioBD.Correo = empleadoBD.Correo;
                    }
                    _context.Update(usuarioBD);
                }
            }

            await _context.SaveChangesAsync();

            return Ok();

        }

        // CAMBIAR ESTADO
        [HttpPost]
        public async Task<IActionResult> CambiarEstado([FromBody] EstadoEmpleado datos)
        {
            // Estados permitidos
            var estadosPermitidos = new[]
            {
                "Activo",
                "Inactivo",
                "Vacaciones",
                "Incapacidad",
                "Permiso",
                "Suspendido"
            };

            if (!estadosPermitidos.Contains(datos.Estado))
            {
                return BadRequest("Estado no permitido");
            }

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.IdEmpleado == datos.IdEmpleado);

            if (empleado == null)
            {
                return NotFound();
            }

            empleado.Estado = datos.Estado;

            // Extraer las fechas enviadas (pueden venir en FechaInicioEstado o FechaInicioVacaciones)
            var fechaInicio = datos.FechaInicioEstado ?? datos.FechaInicioVacaciones;
            var fechaFin = datos.FechaFinEstado ?? datos.FechaFinVacaciones;

            empleado.FechaInicioEstado = fechaInicio;
            empleado.FechaFinEstado = fechaFin;

            // Si el estado es Vacaciones, también se sincronizan las columnas de vacaciones
            if (datos.Estado == "Vacaciones")
            {
                empleado.FechaInicioVacaciones = fechaInicio;
                empleado.FechaFinVacaciones = fechaFin;
            }
            else if (datos.Estado == "Activo")
            {
                // Si vuelve a Activo y no se especificaron fechas, se limpian las fechas de estado y vacaciones
                if (fechaInicio == null && fechaFin == null)
                {
                    empleado.FechaInicioVacaciones = null;
                    empleado.FechaFinVacaciones = null;
                    empleado.FechaInicioEstado = null;
                    empleado.FechaFinEstado = null;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Estado actualizado correctamente"
            });
        }


    }
}