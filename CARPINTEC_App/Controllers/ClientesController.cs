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


        // LISTAR CLIENTES CON BÚSQUEDA Y PAGINACIÓN
        public async Task<IActionResult> Index(string? search, int pagina = 1)
        {
            int pageSize = 6;

            var query = _context.Clientes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(c =>
                    (c.Nombre != null && c.Nombre.Contains(s)) ||
                    (c.Apellido != null && c.Apellido.Contains(s)) ||
                    (c.Documento != null && c.Documento.Contains(s)) ||
                    (c.Correo != null && c.Correo.Contains(s)) ||
                    (c.Telefono != null && c.Telefono.Contains(s)) ||
                    (c.NombreEmpresa != null && c.NombreEmpresa.Contains(s)) ||
                    (c.Contacto != null && c.Contacto.Contains(s)));
            }

            int totalRegistros = await query.CountAsync();
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);
            if (totalPaginas == 0) totalPaginas = 1;
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas) pagina = totalPaginas;

            var clientes = await query
                .OrderByDescending(c => c.FechaRegistro)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Métricas sobre el total general
            ViewBag.ClientesTotales = await _context.Clientes.CountAsync();
            ViewBag.ClientesActivos = await _context.Clientes.CountAsync(c => c.Estado == "Activo");
            ViewBag.ClientesInactivos = await _context.Clientes.CountAsync(c => c.Estado == "Inactivo");
            ViewBag.NuevosClientes = await _context.Clientes
                .CountAsync(c => c.FechaRegistro != null &&
                                 c.FechaRegistro.Value.Month == DateTime.Now.Month &&
                                 c.FechaRegistro.Value.Year == DateTime.Now.Year);

            // Datos de paginación y búsqueda
            ViewBag.Search = search;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistroInicio = totalRegistros == 0 ? 0 : (pagina - 1) * pageSize + 1;
            ViewBag.RegistroFin = Math.Min(pagina * pageSize, totalRegistros);

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