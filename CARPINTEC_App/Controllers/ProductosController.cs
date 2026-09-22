// ============================================================
// ProductosController.cs
// Controlador para la gestión completa de productos (CRUD)
// Permite: listar, crear, editar, cambiar estado y eliminar
// ============================================================

using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using CARPINTEC_App.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    // Solo usuarios autenticados pueden acceder a este controlador
    [Authorize]
    [PermisoCargo("Productos")]
    public class ProductosController : Controller
    {
        // Contexto de base de datos inyectado por DI
        private readonly CarpintecContext _context;

        // Entorno de hospedaje para acceder a wwwroot (subida de imágenes)
        private readonly IWebHostEnvironment _env;

        // Constructor: recibe el contexto de BD y el entorno de hospedaje
        public ProductosController(CarpintecContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ============================================================
        // GET: /Productos
        // Lista todos los productos y calcula estadísticas para la vista
        // ============================================================
        public async Task<IActionResult> Index()
        {
            // Obtener todos los productos ordenados por fecha de registro (más recientes primero)
            var productos = await _context.Productos
                .OrderByDescending(p => p.FechaRegistro)
                .ToListAsync();

            // --- Estadísticas para las tarjetas superiores ---

            // Total de productos registrados
            ViewBag.TotalProductos = productos.Count;

            // Cantidad de productos con estado "Activo"
            ViewBag.ProductosActivos = productos.Count(p => p.Estado == "Activo");

            // Cantidad de productos con estado "Inactivo"
            ViewBag.ProductosInactivos = productos.Count(p => p.Estado == "Inactivo");

            // Cantidad de categorías únicas (distintas)
            ViewBag.TotalCategorias = productos
                .Where(p => !string.IsNullOrEmpty(p.Categoria))
                .Select(p => p.Categoria)
                .Distinct()
                .Count();

            // Enviar la lista de productos al modelo de la vista
            return View(productos);
        }

        // ============================================================
        // POST: /Productos/Crear
        // Crea un nuevo producto y lo guarda en la base de datos
        // Recibe los datos del formulario modal + archivo de imagen
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            string Nombre,
            string? Codigo,
            string? Descripcion,
            decimal? Precio,
            string? Categoria,
            string? Material,
            string? Medidas,
            IFormFile? Imagen)
        {
            // Crear nueva instancia del producto con los datos recibidos
            var producto = new Producto
            {
                Nombre = Nombre,
                Codigo = Codigo,
                Descripcion = Descripcion,
                Precio = Precio,
                Categoria = Categoria,
                Material = Material,
                Medidas = Medidas,
                Estado = "Activo",                    // Estado por defecto al crear
                FechaRegistro = DateTime.Now          // Fecha de creación
            };

            // --- Manejo de subida de imagen ---
            if (Imagen != null && Imagen.Length > 0)
            {
                // Crear la carpeta de uploads si no existe
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "productos");
                Directory.CreateDirectory(uploadsFolder);

                // Generar nombre único para evitar colisiones (GUID + extensión original)
                var nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(Imagen.FileName);
                var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                // Guardar el archivo en disco
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await Imagen.CopyToAsync(stream);
                }

                // Guardar la ruta relativa en la BD (para usar en <img src="...">)
                producto.Imagen = "/uploads/productos/" + nombreArchivo;
            }

            // Agregar el producto al contexto y guardar en la BD
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            // Redirigir al listado de productos
            return RedirectToAction("Index");
        }

        // ============================================================
        // GET: /Productos/Editar/{id}
        // Retorna los datos del producto en formato JSON
        // Se usa desde JavaScript (fetch) para llenar el modal de edición
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            // Buscar el producto por su ID
            var producto = await _context.Productos.FindAsync(id);

            // Si no se encuentra, retornar error 404
            if (producto == null)
                return NotFound();

            // Retornar los datos como JSON para el frontend
            return Json(new
            {
                producto.IdProducto,
                producto.Nombre,
                producto.Codigo,
                producto.Descripcion,
                producto.Precio,
                producto.Categoria,
                producto.Material,
                producto.Medidas,
                producto.Estado,
                producto.Imagen
            });
        }

        // ============================================================
        // POST: /Productos/Editar
        // Actualiza un producto existente en la base de datos
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int IdProducto,
            string Nombre,
            string? Codigo,
            string? Descripcion,
            decimal? Precio,
            string? Categoria,
            string? Material,
            string? Medidas,
            string? Estado,
            IFormFile? Imagen)
        {
            // Buscar el producto existente en la BD
            var producto = await _context.Productos.FindAsync(IdProducto);

            // Si no existe, retornar error 404
            if (producto == null)
                return NotFound();

            // --- Actualizar cada campo con los datos recibidos ---
            producto.Nombre = Nombre;
            producto.Codigo = Codigo;
            producto.Descripcion = Descripcion;
            producto.Precio = Precio;
            producto.Categoria = Categoria;
            producto.Material = Material;
            producto.Medidas = Medidas;
            producto.Estado = Estado;

            // --- Actualizar imagen solo si se subió una nueva ---
            if (Imagen != null && Imagen.Length > 0)
            {
                // Crear carpeta si no existe
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "productos");
                Directory.CreateDirectory(uploadsFolder);

                // Generar nombre único para el nuevo archivo
                var nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(Imagen.FileName);
                var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                // Guardar nuevo archivo en disco
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await Imagen.CopyToAsync(stream);
                }

                // Actualizar la ruta de imagen en el producto
                producto.Imagen = "/uploads/productos/" + nombreArchivo;
            }

            // Guardar los cambios en la BD
            _context.Productos.Update(producto);
            await _context.SaveChangesAsync();

            // Redirigir al listado
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: /Productos/CambiarEstado
        // Cambia el estado de un producto (Activo ↔ Inactivo)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id)
        {
            // Buscar el producto por su ID
            var producto = await _context.Productos.FindAsync(id);

            // Si no existe, retornar error 404
            if (producto == null)
                return NotFound();

            // Alternar el estado: si es Activo → Inactivo, si no → Activo
            producto.Estado = producto.Estado == "Activo" ? "Inactivo" : "Activo";

            // Guardar el cambio en la BD
            await _context.SaveChangesAsync();

            // Redirigir al listado
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: /Productos/Eliminar
        // Elimina un producto de la base de datos
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            // Buscar el producto por su ID
            var producto = await _context.Productos.FindAsync(id);

            // Si no existe, retornar error 404
            if (producto == null)
                return NotFound();

            // Eliminar el producto del contexto
            _context.Productos.Remove(producto);

            // Guardar los cambios (ejecutar DELETE en la BD)
            await _context.SaveChangesAsync();

            // Redirigir al listado
            return RedirectToAction("Index");
        }
    }
}
