using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Models.ViewModels;
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

        public static readonly string[] CargosPermitidos = new[]
        {
            "Supervisor",
            "Asesor comercial",
            "Diseñador de muebles",
            "Jefe de producción",
            "Operario de carpintería",
            "Encargado de inventario"
        };

        public static readonly string[] RolesPermitidos = new[]
        {
            "Cliente",
            "Empleado",
            "Administrador"
        };

        public UsuariosController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: Usuarios con búsqueda, filtro de rol y paginación
        public async Task<IActionResult> Index(string? search, string? rol, int pagina = 1)
        {
            if (User.IsInRole("Empleado"))
            {
                return RedirectToAction("Index", "DashboardEmpleado");
            }

            int pageSize = 6;

            var query = _context.Usuarios.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(u =>
                    (u.Correo != null && u.Correo.Contains(s)) ||
                    (u.Nombre != null && u.Nombre.Contains(s)) ||
                    (u.Apellido != null && u.Apellido.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(rol) && rol != "Todos los roles" && rol != "Todos")
            {
                query = query.Where(u => u.Rol == rol);
            }

            int totalRegistros = await query.CountAsync();
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / pageSize);
            if (totalPaginas == 0) totalPaginas = 1;
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas) pagina = totalPaginas;

            var usuarios = await query
                .OrderByDescending(u => u.IdUsuario)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Métricas sobre el total general
            var todos = await _context.Usuarios.ToListAsync();
            ViewBag.TotalUsuarios = todos.Count;
            ViewBag.TotalAdministradores = todos.Count(x => x.Rol == "Administrador");
            ViewBag.TotalEmpleados = todos.Count(x => x.Rol == "Empleado");
            ViewBag.TotalClientes = todos.Count(x => x.Rol == "Cliente");
            ViewBag.TotalInactivos = todos.Count(x => x.Estado == "Inactivo");
            ViewBag.TotalBloqueados = todos.Count(x => x.Estado == "Bloqueado");

            // Datos de búsqueda y paginación
            ViewBag.Search = search;
            ViewBag.Rol = rol;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistroInicio = totalRegistros == 0 ? 0 : (pagina - 1) * pageSize + 1;
            ViewBag.RegistroFin = Math.Min(pagina * pageSize, totalRegistros);
            ViewBag.Cargos = CargosPermitidos;

            return View(usuarios);
        }

        // GET: Usuarios/NuevoUsuario
        [HttpGet]
        public IActionResult NuevoUsuario()
        {
            ViewBag.Cargos = CargosPermitidos;
            return View(new UsuarioGestionViewModel());
        }

        // POST: Usuarios/NuevoUsuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevoUsuario(UsuarioGestionViewModel model)
        {
            ViewBag.Cargos = CargosPermitidos;

            string rolNormalizado = string.IsNullOrWhiteSpace(model.Rol) ? "Cliente" : model.Rol.Trim();

            // 1. Validar rol permitido
            if (!RolesPermitidos.Any(r => r.Equals(rolNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Rol", "Seleccione un rol válido (Cliente, Empleado o Administrador).");
                return View(model);
            }
            model.Rol = RolesPermitidos.First(r => r.Equals(rolNormalizado, StringComparison.OrdinalIgnoreCase));

            // 2. Validar contraseña obligatoria al crear
            if (string.IsNullOrWhiteSpace(model.Contraseña))
            {
                ModelState.AddModelError("Contraseña", "La contraseña es obligatoria para crear el usuario.");
                return View(model);
            }

            // 3. Validar duplicidad de correo en Usuarios
            string correoLimpio = (model.Correo ?? "").Trim();
            if (string.IsNullOrEmpty(correoLimpio))
            {
                ModelState.AddModelError("Correo", "El correo electrónico es obligatorio.");
                return View(model);
            }

            bool correoExisteUsuario = await _context.Usuarios
                .AnyAsync(u => u.Correo != null && u.Correo.ToLower() == correoLimpio.ToLower());

            if (correoExisteUsuario)
            {
                ModelState.AddModelError("Correo", "Este correo electrónico ya está registrado.");
                return View(model);
            }

            // 4. Si el rol es Empleado, validar datos requeridos de Empleado
            if (model.Rol == "Empleado")
            {
                if (string.IsNullOrWhiteSpace(model.Documento))
                {
                    ModelState.AddModelError("Documento", "El número de documento es obligatorio para un empleado.");
                    return View(model);
                }

                string docLimpio = model.Documento.Trim();
                bool docExisteEmpleado = await _context.Empleados
                    .AnyAsync(e => e.Documento == docLimpio);

                if (docExisteEmpleado)
                {
                    ModelState.AddModelError("Documento", "Este número de documento ya está registrado para otro empleado.");
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Cargo))
                {
                    ModelState.AddModelError("Cargo", "Debe seleccionar un puesto/cargo para el empleado.");
                    return View(model);
                }

                if (!CargosPermitidos.Contains(model.Cargo.Trim()))
                {
                    ModelState.AddModelError("Cargo", "El cargo seleccionado no es válido.");
                    return View(model);
                }

                bool correoExisteEmpleado = await _context.Empleados
                    .AnyAsync(e => e.Correo != null && e.Correo.ToLower() == correoLimpio.ToLower());

                if (correoExisteEmpleado)
                {
                    ModelState.AddModelError("Correo", "Este correo ya está registrado en la lista de empleados.");
                    return View(model);
                }
            }

            // 5. Crear el registro en Usuarios
            var nuevoUsuario = new Usuario
            {
                Nombre = model.Nombre.Trim(),
                Apellido = model.Apellido.Trim(),
                Correo = correoLimpio,
                Rol = model.Rol,
                Estado = "Activo",
                IntentosFallidos = 0
            };

            var hasher = new PasswordHasher<Usuario>();
            nuevoUsuario.Contraseña = hasher.HashPassword(nuevoUsuario, model.Contraseña);

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // 6. Actuar según el rol:
            if (model.Rol == "Empleado")
            {
                // Crear automáticamente el registro en Empleados y relacionarlo con IdUsuario
                var nuevoEmpleado = new Empleado
                {
                    IdUsuario = nuevoUsuario.IdUsuario,
                    TipoDocumento = string.IsNullOrWhiteSpace(model.TipoDocumento) ? "CC" : model.TipoDocumento.Trim(),
                    Documento = model.Documento!.Trim(),
                    Nombre = nuevoUsuario.Nombre,
                    Apellido = nuevoUsuario.Apellido,
                    Cargo = model.Cargo!.Trim(),
                    Correo = nuevoUsuario.Correo,
                    Telefono = model.Telefono?.Trim(),
                    Direccion = model.Direccion?.Trim(),
                    FechaIngreso = model.FechaIngreso ?? DateOnly.FromDateTime(DateTime.Today),
                    Salario = model.Salario ?? 0,
                    Estado = "Activo",
                    Foto = model.Foto
                };

                _context.Empleados.Add(nuevoEmpleado);
                await _context.SaveChangesAsync();
            }
            // Si es Cliente o Administrador, NO se crea registro en Empleados.

            TempData["Success"] = $"Usuario con rol {model.Rol} creado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Usuarios/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Empleados)
                .FirstOrDefaultAsync(u => u.IdUsuario == id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // Obtener usuario para modales (VER y EDITAR)
        [HttpGet]
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Empleados)
                .FirstOrDefaultAsync(x => x.IdUsuario == id);

            if (usuario == null)
            {
                return NotFound();
            }

            // Buscar empleado activo o asociado
            var empleado = usuario.Empleados.FirstOrDefault(e => e.Estado == "Activo")
                           ?? usuario.Empleados.FirstOrDefault();

            return Json(new
            {
                idUsuario = usuario.IdUsuario,
                nombre = usuario.Nombre,
                apellido = usuario.Apellido,
                correo = usuario.Correo,
                rol = usuario.Rol,
                estado = usuario.Estado,
                // Datos de Empleado (si existen)
                idEmpleado = empleado?.IdEmpleado,
                tipoDocumento = empleado?.TipoDocumento ?? "CC",
                documento = empleado?.Documento ?? "",
                cargo = empleado?.Cargo ?? "",
                telefono = empleado?.Telefono ?? "",
                direccion = empleado?.Direccion ?? "",
                fechaIngreso = empleado?.FechaIngreso.ToString("yyyy-MM-dd") ?? "",
                salario = empleado?.Salario,
                foto = empleado?.Foto
            });
        }

        // GET: Usuarios/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Empleados)
                .FirstOrDefaultAsync(u => u.IdUsuario == id);

            if (usuario == null)
            {
                return NotFound();
            }

            var empleado = usuario.Empleados.FirstOrDefault(e => e.Estado == "Activo")
                           ?? usuario.Empleados.FirstOrDefault();

            var model = new UsuarioGestionViewModel
            {
                IdUsuario = usuario.IdUsuario,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Correo = usuario.Correo ?? "",
                Rol = usuario.Rol ?? "Cliente",
                Estado = usuario.Estado ?? "Activo",
                IdEmpleado = empleado?.IdEmpleado,
                TipoDocumento = empleado?.TipoDocumento ?? "CC",
                Documento = empleado?.Documento ?? "",
                Cargo = empleado?.Cargo ?? "",
                Telefono = empleado?.Telefono ?? "",
                Direccion = empleado?.Direccion ?? "",
                FechaIngreso = empleado?.FechaIngreso,
                Salario = empleado?.Salario,
                Foto = empleado?.Foto
            };

            ViewBag.Cargos = CargosPermitidos;
            return View(model);
        }

        // POST: Usuarios/Edit (Edición desde modal vía JSON)
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] UsuarioGestionViewModel model)
        {
            if (model == null || model.IdUsuario <= 0)
            {
                return BadRequest(new { mensaje = "Datos de usuario inválidos." });
            }

            var usuarioBD = await _context.Usuarios
                .Include(u => u.Empleados)
                .FirstOrDefaultAsync(x => x.IdUsuario == model.IdUsuario);

            if (usuarioBD == null)
            {
                return NotFound(new { mensaje = "El usuario no fue encontrado." });
            }

            // Validar correo único si fue modificado
            string correoNuevo = (model.Correo ?? "").Trim();
            if (!string.IsNullOrEmpty(correoNuevo) && !correoNuevo.Equals(usuarioBD.Correo, StringComparison.OrdinalIgnoreCase))
            {
                bool correoEnUso = await _context.Usuarios
                    .AnyAsync(u => u.Correo != null && u.Correo.ToLower() == correoNuevo.ToLower() && u.IdUsuario != model.IdUsuario);

                if (correoEnUso)
                {
                    return BadRequest(new { mensaje = "El correo electrónico ya está registrado por otro usuario." });
                }
            }

            string rolAnterior = (usuarioBD.Rol ?? "").Trim();
            string rolNuevoNormalizado = string.IsNullOrWhiteSpace(model.Rol) ? "Cliente" : model.Rol.Trim();

            if (!RolesPermitidos.Any(r => r.Equals(rolNuevoNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { mensaje = "El rol seleccionado no es válido." });
            }

            string rolNuevo = RolesPermitidos.First(r => r.Equals(rolNuevoNormalizado, StringComparison.OrdinalIgnoreCase));

            // Actualizar datos de Usuario
            usuarioBD.Nombre = model.Nombre?.Trim() ?? usuarioBD.Nombre;
            usuarioBD.Apellido = model.Apellido?.Trim() ?? usuarioBD.Apellido;
            usuarioBD.Correo = correoNuevo;
            usuarioBD.Rol = rolNuevo;
            if (!string.IsNullOrWhiteSpace(model.Estado))
            {
                usuarioBD.Estado = model.Estado;
            }

            // ========================================================
            // Lógica según cambio de rol y sincronización con Empleados
            // ========================================================
            if (rolNuevo.Equals("Empleado", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Debe tener cargo y documento
                if (string.IsNullOrWhiteSpace(model.Cargo) || !CargosPermitidos.Contains(model.Cargo.Trim()))
                {
                    return BadRequest(new { mensaje = "Debe seleccionar un puesto/cargo válido para el empleado." });
                }

                if (string.IsNullOrWhiteSpace(model.Documento))
                {
                    return BadRequest(new { mensaje = "El documento es obligatorio para un empleado." });
                }

                string docLimpio = model.Documento.Trim();

                // Buscar si ya existe empleado con este IdUsuario o Documento
                var empleadoBD = usuarioBD.Empleados.FirstOrDefault()
                               ?? await _context.Empleados.FirstOrDefaultAsync(e => e.IdUsuario == usuarioBD.IdUsuario || e.Documento == docLimpio);

                if (empleadoBD != null)
                {
                    // Si ya existe empleado con este documento pero pertenece a otro usuario, rechazar
                    if (empleadoBD.IdUsuario != null && empleadoBD.IdUsuario != usuarioBD.IdUsuario)
                    {
                        return BadRequest(new { mensaje = "El documento ya está asignado a otro empleado." });
                    }

                    // Actualizar empleado existente
                    empleadoBD.IdUsuario = usuarioBD.IdUsuario;
                    empleadoBD.TipoDocumento = string.IsNullOrWhiteSpace(model.TipoDocumento) ? (empleadoBD.TipoDocumento ?? "CC") : model.TipoDocumento.Trim();
                    empleadoBD.Documento = docLimpio;
                    empleadoBD.Nombre = usuarioBD.Nombre;
                    empleadoBD.Apellido = usuarioBD.Apellido;
                    empleadoBD.Cargo = model.Cargo.Trim();
                    empleadoBD.Correo = usuarioBD.Correo;
                    empleadoBD.Telefono = model.Telefono?.Trim();
                    empleadoBD.Direccion = model.Direccion?.Trim();
                    if (model.Salario.HasValue) empleadoBD.Salario = model.Salario.Value;
                    if (model.FechaIngreso.HasValue) empleadoBD.FechaIngreso = model.FechaIngreso.Value;
                    empleadoBD.Estado = "Activo";

                    _context.Update(empleadoBD);
                }
                else
                {
                    // Validar que el documento no esté registrado para otro empleado
                    bool docEnUso = await _context.Empleados.AnyAsync(e => e.Documento == docLimpio);
                    if (docEnUso)
                    {
                        return BadRequest(new { mensaje = "El documento ya está registrado para otro empleado." });
                    }

                    // Crear nuevo registro de empleado
                    var nuevoEmpleado = new Empleado
                    {
                        IdUsuario = usuarioBD.IdUsuario,
                        TipoDocumento = string.IsNullOrWhiteSpace(model.TipoDocumento) ? "CC" : model.TipoDocumento.Trim(),
                        Documento = docLimpio,
                        Nombre = usuarioBD.Nombre,
                        Apellido = usuarioBD.Apellido,
                        Cargo = model.Cargo.Trim(),
                        Correo = usuarioBD.Correo,
                        Telefono = model.Telefono?.Trim(),
                        Direccion = model.Direccion?.Trim(),
                        FechaIngreso = model.FechaIngreso ?? DateOnly.FromDateTime(DateTime.Today),
                        Salario = model.Salario ?? 0,
                        Estado = "Activo",
                        Foto = model.Foto
                    };

                    _context.Empleados.Add(nuevoEmpleado);
                }
            }
            else if (rolAnterior.Equals("Empleado", StringComparison.OrdinalIgnoreCase))
            {
                // Cambio de Empleado -> Cliente o Empleado -> Administrador
                // Desvincular de forma segura sin borrar para preservar integridad referencial de cotizaciones/mano de obra
                var empleadoBD = usuarioBD.Empleados.FirstOrDefault()
                               ?? await _context.Empleados.FirstOrDefaultAsync(e => e.IdUsuario == usuarioBD.IdUsuario);

                if (empleadoBD != null)
                {
                    empleadoBD.IdUsuario = null;
                    empleadoBD.Estado = "Inactivo";
                    _context.Update(empleadoBD);
                }
            }
            // Si es Cliente -> Administrador o Administrador -> Cliente, solo se actualiza Usuarios.Rol

            _context.Update(usuarioBD);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, mensaje = "Usuario y datos asociados actualizados correctamente." });
        }

        // POST: Usuarios/EditForm (Edición tradicional desde vista Edit.cshtml)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("EditForm")]
        public async Task<IActionResult> EditForm(UsuarioGestionViewModel model)
        {
            var resultado = await Edit(model);
            if (resultado is OkObjectResult)
            {
                TempData["Success"] = "Usuario actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            if (resultado is BadRequestObjectResult badRequest && badRequest.Value != null)
            {
                var dict = badRequest.Value as IDictionary<string, object>;
                string errorMsg = dict != null && dict.ContainsKey("mensaje") ? dict["mensaje"]?.ToString() ?? "Error" : "Error al actualizar";
                TempData["Error"] = errorMsg;
            }
            else
            {
                TempData["Error"] = "Error al actualizar el usuario.";
            }

            return RedirectToAction(nameof(Edit), new { id = model.IdUsuario });
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