using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class UsuariosController : Controller
    {
        private readonly CarpintecContext _context;

        public UsuariosController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: Usuarios
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Usuarios.ToListAsync();
            return View(usuarios);
        }

        // GET: Usuarios/NuevoUsuario
        [HttpGet]
        public IActionResult NuevoUsuario()
        {
            return View();
        }

        // POST: Usuarios/NuevoUsuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevoUsuario(Usuario usuario)
        {
            if (!ModelState.IsValid)
            {
                return View(usuario);
            }

            // Verificar si el correo ya existe
            if (!string.IsNullOrEmpty(usuario.Correo))
            {
                bool correoExiste = await _context.Usuarios
                    .AnyAsync(u => u.Correo == usuario.Correo);

                if (correoExiste)
                {
                    ModelState.AddModelError(
                        "Correo",
                        "Este correo ya está registrado."
                    );

                    return View(usuario);
                }
            }

            // El usuario se crea activo
            usuario.Estado = "Activo";

            // Inicializar intentos de inicio de sesión
            usuario.IntentosFallidos = 0;


            // Crear hash de la contraseña
            var hasher = new PasswordHasher<Usuario>();

            usuario.Contraseña = hasher.HashPassword(
                usuario,
                usuario.Contraseña
            );


            // Guardar usuario
            _context.Usuarios.Add(usuario);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Usuario creado correctamente.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Usuarios/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // Obtener usuario para modal VER
        [HttpGet]
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(x => x.IdUsuario == id);

            if (usuario == null)
            {
                return NotFound();
            }

            return Json(usuario);
        }

        // GET: Usuarios/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // Editar usuario desde modal
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] Usuario usuario)
        {

            var usuarioBD = await _context.Usuarios
                .FirstOrDefaultAsync(x => x.IdUsuario == usuario.IdUsuario);


            if (usuarioBD == null)
            {
                return NotFound();
            }


            usuarioBD.Nombre = usuario.Nombre;

            usuarioBD.Apellido = usuario.Apellido;

            usuarioBD.Correo = usuario.Correo;

            usuarioBD.Rol = usuario.Rol;


            _context.Update(usuarioBD);

            await _context.SaveChangesAsync();


            return Ok();

        }

        // Cambiar estado desde modal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, string estado)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(x => x.IdUsuario == id);


            if (usuario == null)
            {
                return NotFound();
            }


            usuario.Estado = estado;


            _context.Update(usuario);

            await _context.SaveChangesAsync();


            TempData["Success"] = "Estado actualizado correctamente";


            return RedirectToAction(nameof(Index));
        }
    }
}