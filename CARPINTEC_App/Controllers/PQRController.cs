using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class PQRController : Controller
    {
        private readonly CarpintecContext _context;

        public PQRController(CarpintecContext context)
        {
            _context = context;
        }


        // LISTADO DE PQR
        public async Task<IActionResult> Index()
        {
            var listaPqr = await _context.Pqrs
                .Include(p => p.IdClienteNavigation)
                .OrderByDescending(p => p.FechaRegistro)
                .ToListAsync();


            // Estadísticas
            ViewBag.Pendientes = listaPqr.Count(p => p.Estado == "Pendiente");
            ViewBag.EnProceso = listaPqr.Count(p => p.Estado == "En proceso");
            ViewBag.Respondidas = listaPqr.Count(p => p.Estado == "Respondida");
            ViewBag.Cerradas = listaPqr.Count(p => p.Estado == "Cerrada");


            // Clientes para el modal de crear PQR
            ViewBag.Clientes = await _context.Clientes
                .Where(c => c.Estado == "Activo")
                .ToListAsync();


            return View(listaPqr);
        }

        // VER DETALLE DE PQR
        public async Task<IActionResult> Details(int id)
        {
            var pqr = await _context.Pqrs
                .Include(p => p.IdClienteNavigation)
                .FirstOrDefaultAsync(p => p.IdPqr == id);

            if (pqr == null)
            {
                return NotFound();
            }

            return View(pqr);
        }
        // OBTENER DATOS DE UNA PQR PARA EL MODAL
        [HttpGet]
        public async Task<IActionResult> ObtenerPQR(int id)
        {
            var pqr = await _context.Pqrs
                .Include(p => p.IdClienteNavigation)
                .FirstOrDefaultAsync(p => p.IdPqr == id);

            if (pqr == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = pqr.IdPqr,
                idCliente = pqr.IdCliente,
                codigo = pqr.CodigoPqr,

                cliente = pqr.IdClienteNavigation != null
                    ? pqr.IdClienteNavigation.Nombre + " " + pqr.IdClienteNavigation.Apellido
                    : "",

                documento = pqr.IdClienteNavigation?.Documento,
                correo = pqr.IdClienteNavigation?.Correo,
                telefono = pqr.IdClienteNavigation?.Telefono,

                tipo = pqr.Tipo,
                asunto = pqr.Asunto,
                descripcion = pqr.Descripcion,

                estado = pqr.Estado,

                fecha = pqr.FechaRegistro.ToString("dd/MM/yyyy"),

                respuesta = pqr.Respuesta
            });
        }
        // GUARDAR NUEVA PQR DESDE EL MODAL DEL INDEX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Pqr pqr)
        {

            Console.WriteLine("ENTRÓ AL CREATE");
            Console.WriteLine("Cliente: " + pqr.IdCliente);
            Console.WriteLine("Asunto: " + pqr.Asunto);
            Console.WriteLine("Descripcion: " + pqr.Descripcion);


            pqr.CodigoPqr = "PQR-" + DateTime.Now.ToString("yyyyMMddHHmmss");

            pqr.FechaRegistro = DateOnly.FromDateTime(DateTime.Now);

            pqr.Estado = "Pendiente";


            _context.Pqrs.Add(pqr);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // RESPONDER PQR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Responder(int id, string respuesta, string estado)
        {
            Console.WriteLine("ENTRÓ A RESPONDER");
            Console.WriteLine("ID: " + id);
            Console.WriteLine("Respuesta: " + respuesta);
            Console.WriteLine("Estado: " + estado);

            var pqr = await _context.Pqrs
                .FirstOrDefaultAsync(p => p.IdPqr == id);


            if (pqr == null)
            {
                return NotFound();
            }


            pqr.Respuesta = respuesta;
            pqr.Estado = estado;
            pqr.FechaRespuesta = DateOnly.FromDateTime(DateTime.Now);


            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR AL GUARDAR PQR:");
                Console.WriteLine(ex.ToString());

                return BadRequest(ex.Message);
            }

            return Ok();
        }
        // EDITAR PQR - MOSTRAR FORMULARIO
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pqr = await _context.Pqrs
                .Include(p => p.IdClienteNavigation)
                .FirstOrDefaultAsync(p => p.IdPqr == id);


            if (pqr == null)
            {
                return NotFound();
            }


            ViewBag.Clientes = await _context.Clientes
                .Where(c => c.Estado == "Activo")
                .ToListAsync();


            return View(pqr);
        }


        // GUARDAR CAMBIOS DE PQR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Pqr pqr)
        {

            if (id != pqr.IdPqr)
            {
                return NotFound();
            }


            var pqrBD = await _context.Pqrs
                .FirstOrDefaultAsync(p => p.IdPqr == id);


            if (pqrBD == null)
            {
                return NotFound();
            }


            pqrBD.CodigoPqr = pqr.CodigoPqr;
            pqrBD.Tipo = pqr.Tipo;
            pqrBD.Asunto = pqr.Asunto;
            pqrBD.Descripcion = pqr.Descripcion;
            pqrBD.Estado = pqr.Estado;
            pqrBD.Respuesta = pqr.Respuesta;
            pqrBD.FechaRegistro = pqr.FechaRegistro;


            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return Content(ex.ToString());
            }
            return RedirectToAction(nameof(Index));
        } // ← ESTA LLAVE CIERRA EL MÉTODO EDIT


        // CAMBIAR ESTADO DE PQR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, string estado)
        {

            var pqr = await _context.Pqrs.FindAsync(id);

            if (pqr == null)
            {
                return NotFound();
            }

            pqr.Estado = estado;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
    