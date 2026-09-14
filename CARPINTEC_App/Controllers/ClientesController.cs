using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class ClientesController : Controller
    {
        private readonly CarpintecContext _context;

        public ClientesController(CarpintecContext context)
        {
            _context = context;
        }


        // LISTAR CLIENTES
        public IActionResult Index()
        {
            var clientes = _context.Clientes
                                   .OrderByDescending(c => c.FechaRegistro)
                                   .ToList();


            ViewBag.ClientesTotales = clientes.Count();

            ViewBag.ClientesActivos = clientes
                .Count(c => c.Estado == "Activo");


            ViewBag.ClientesInactivos = clientes
                .Count(c => c.Estado == "Inactivo");


            ViewBag.NuevosClientes = clientes
     .Count(c => c.FechaRegistro != null &&
                 c.FechaRegistro.Value.Month == DateTime.Now.Month &&
                 c.FechaRegistro.Value.Year == DateTime.Now.Year);


            return View(clientes);
        }


        // VER DETALLES DEL CLIENTE
        public IActionResult Details(int id)
        {
            var cliente = _context.Clientes
                                  .FirstOrDefault(c => c.IdCliente == id);

            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }

        // DATOS PARA MODAL VER CLIENTE
        public IActionResult VerCliente(int id)
        {
            var cliente = _context.Clientes
                                  .FirstOrDefault(c => c.IdCliente == id);

            if (cliente == null)
            {
                return NotFound();
            }

            return Json(cliente);
        }

        // DATOS PARA EDITAR CLIENTE (MODAL)
        public IActionResult EditarCliente(int id)
        {
            var cliente = _context.Clientes
                                  .FirstOrDefault(c => c.IdCliente == id);

            if (cliente == null)
            {
                return NotFound();
            }

            return Json(cliente);
        }


        // FORMULARIO CREAR CLIENTE
        public IActionResult Create()
        {
            return View();
        }


        // GUARDAR CLIENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Cliente cliente)
        {

            // Valores automáticos
            cliente.Estado = "Activo";
            cliente.FechaRegistro = DateTime.Now;


            // Guardar cliente
            _context.Clientes.Add(cliente);

            try
            {
                _context.SaveChanges();

                TempData["Mensaje"] = "Cliente creado correctamente";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;

                return View(cliente);
            }

        }


        // FORMULARIO EDITAR
        public IActionResult Edit(int id)
        {
            var cliente = _context.Clientes
                                  .Find(id);

            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }


        // ACTUALIZAR CLIENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Cliente cliente)
        {
            var clienteBD = _context.Clientes
                                    .FirstOrDefault(c => c.IdCliente == cliente.IdCliente);

            if (clienteBD == null)
            {
                return NotFound();
            }


            clienteBD.TipoCliente = cliente.TipoCliente;
            clienteBD.Documento = cliente.Documento;
            clienteBD.Nombre = cliente.Nombre;
            clienteBD.Apellido = cliente.Apellido;
            clienteBD.NombreEmpresa = cliente.NombreEmpresa;
            clienteBD.Contacto = cliente.Contacto;
            clienteBD.Telefono = cliente.Telefono;
            clienteBD.Correo = cliente.Correo;
            clienteBD.Direccion = cliente.Direccion;
            clienteBD.Ciudad = cliente.Ciudad;


            _context.SaveChanges();


            TempData["Mensaje"] = "Cliente actualizado correctamente";


            return RedirectToAction(nameof(Index));
        }



        // CAMBIAR ESTADO ACTIVO / INACTIVO
        public IActionResult CambiarEstado(int id)
        {
            var cliente = _context.Clientes
                                  .Find(id);


            if (cliente == null)
            {
                return NotFound();
            }


            if (cliente.Estado == "Activo")
            {
                cliente.Estado = "Inactivo";
            }
            else
            {
                cliente.Estado = "Activo";
            }


            _context.SaveChanges();


            TempData["Mensaje"] = "Estado actualizado correctamente";


            return RedirectToAction(nameof(Index));
        }



        // ELIMINAR CLIENTE
        public IActionResult Delete(int id)
        {
            var cliente = _context.Clientes
                                  .Find(id);


            if (cliente == null)
            {
                return NotFound();
            }


            _context.Clientes.Remove(cliente);
            _context.SaveChanges();


            TempData["Mensaje"] = "Cliente eliminado correctamente";


            return RedirectToAction(nameof(Index));
        }
    }
}