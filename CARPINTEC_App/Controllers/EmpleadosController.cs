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


            // Fecha no anterior
            if (empleado.FechaIngreso < DateOnly.FromDateTime(DateTime.Now))
            {
                TempData["Error"] = "La fecha de ingreso no puede ser anterior a la fecha actual";
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



            _context.Update(empleadoBD);



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



            await _context.SaveChangesAsync();



            return Ok(new
            {
                mensaje = "Estado actualizado correctamente"
            });

        }


    }
}