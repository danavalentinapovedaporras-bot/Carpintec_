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
        // Asigna una imagen de mueble representativa de alta calidad
        // según la categoría o nombre del producto si no tiene foto subida
        // ============================================================
        public static string ObtenerImagenPorDefecto(string? categoria, string? nombre)
        {
            var cat = (categoria ?? "").ToLowerInvariant();
            var nom = (nombre ?? "").ToLowerInvariant();

            // 1. Escritorios (evaluar antes de mesas para evitar falsos positivos con 'mesa escritorio')
            if (cat.Contains("escritorio") || nom.Contains("escritorio") || nom.Contains("despacho") || cat.Contains("despacho") || nom.Contains("estudio") || cat.Contains("estudio"))
                return "/images/escritorio.jpeg";

            // 2. Sillas
            if (cat.Contains("silla") || nom.Contains("silla") || nom.Contains("sillon") || nom.Contains("sillón"))
            {
                if (nom.Contains("cedro")) return "/images/silladecedro.jpeg";
                return "/images/sillas.jpeg";
            }

            // 3. Mesas
            if (cat.Contains("mesa") || nom.Contains("mesa"))
            {
                if (nom.Contains("noche")) return "/images/mesadenoche.jpeg";
                if (nom.Contains("centro") || nom.Contains("mesita") || nom.Contains("cafe") || nom.Contains("café")) return "/images/mesita.jpeg";
                return "/images/mesa.jpeg";
            }

            // 4. Camas
            if (cat.Contains("cama") || nom.Contains("cama") || cat.Contains("dormitorio") || nom.Contains("dormitorio") || nom.Contains("cabecero"))
                return "/images/cama.jpeg";

            // 5. Closets / Armarios
            if (cat.Contains("closet") || nom.Contains("closet") || cat.Contains("armario") || nom.Contains("armario") || nom.Contains("ropero"))
                return nom.Contains("roble") ? "/images/closetroble.jpeg" : "/images/closet.jpeg";

            // 6. Estantes / Repisas / Libreros
            if (cat.Contains("estante") || nom.Contains("estante") || nom.Contains("librero") || cat.Contains("repisa") || nom.Contains("repisa") || nom.Contains("biblioteca") || cat.Contains("biblioteca"))
                return "/images/estanteria.jpeg";

            // 7. Comedores
            if (cat.Contains("comedor") || nom.Contains("comedor"))
                return "/images/comedor.jpeg";

            // 8. Cocinas
            if (cat.Contains("cocina") || nom.Contains("cocina"))
            {
                if (nom.Contains("cedro")) return "/images/Cocinadecedro.jpeg";
                if (nom.Contains("roble")) return "/images/Cocinaderoble.jpeg";
                if (nom.Contains("rústica") || nom.Contains("rustica")) return "/images/Cocina_rústica.jpeg";
                return "/images/cocina.jpeg";
            }

            // 9. Accesorios / Organizadores
            if (cat.Contains("accesorio") || nom.Contains("organiza") || cat.Contains("organiza") || nom.Contains("caja") || nom.Contains("repisa pequeña"))
                return "/images/organizador.jpeg";

            // 10. Zapateros
            if (nom.Contains("zapatero") || cat.Contains("zapatero") || nom.Contains("calzado") || cat.Contains("calzado"))
                return "/images/Zapatero.jpg";

            // 11. Barras de cocina
            if (nom.Contains("barra") || cat.Contains("barra"))
                return "/images/Barra de la cocina.jpg";

            // 12. Tocadores / Vanity
            if (nom.Contains("vanity") || nom.Contains("tocador") || cat.Contains("vanity") || cat.Contains("tocador"))
                return "/images/Tocador elegante.jpg";

            return "/images/Wooden.jpeg";
        }

        // ============================================================
        // GET: /Productos
        // Lista todos los productos y calcula estadísticas para la vista
        // ============================================================
        public async Task<IActionResult> Index()
        {
            // Garantizar que la imagen del escritorio esté disponible físicamente en wwwroot/images
            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesDir = Path.Combine(webRoot, "images");
                var destEscritorio = Path.Combine(imagesDir, "escritorio.jpeg");
                var destMesac = Path.Combine(imagesDir, "mesac.jpeg");
                var brainImage = @"C:\Users\danav\.gemini\antigravity-ide\brain\991df03f-c337-43fe-9ea4-3b5076d53547\escritorio_madera_1792171003144.jpg";

                if (System.IO.File.Exists(brainImage))
                {
                    if (!System.IO.File.Exists(destEscritorio) || new FileInfo(destEscritorio).Length == 0)
                    {
                        System.IO.File.Copy(brainImage, destEscritorio, true);
                    }
                    // Actualizar también mesac.jpeg para compatibilidad con registros antiguos
                    System.IO.File.Copy(brainImage, destMesac, true);
                }
                else if (!System.IO.File.Exists(destEscritorio))
                {
                    using var http = new System.Net.Http.HttpClient();
                    var imgBytes = http.GetByteArrayAsync("https://images.unsplash.com/photo-1518455027359-f3f8164ba6bd?w=800").GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(destEscritorio, imgBytes);
                    System.IO.File.WriteAllBytes(destMesac, imgBytes);
                }
            }
            catch
            {
                // Silencioso en caso de restricciones de I/O
            }

            // Obtener todos los productos ordenados por fecha de registro (más recientes primero)
            var productos = await _context.Productos
                .OrderByDescending(p => p.FechaRegistro)
                .ToListAsync();

            // Garantizar que todos los productos existentes tengan la imagen correspondiente en la BD
            bool huboActualizaciones = false;
            foreach (var prod in productos)
            {
                var cat = (prod.Categoria ?? "").ToLowerInvariant();
                var nom = (prod.Nombre ?? "").ToLowerInvariant();
                var esEscritorio = cat.Contains("escritorio") || nom.Contains("escritorio") || nom.Contains("despacho");

                if (esEscritorio)
                {
                    // Si es escritorio y tiene imagen de comedor antigua o genérica
                    if (string.IsNullOrWhiteSpace(prod.Imagen) ||
                        prod.Imagen == "/images/mesac.jpeg" ||
                        prod.Imagen == "/images/mesa.jpeg" ||
                        prod.Imagen == "/images/Wooden.jpeg")
                    {
                        prod.Imagen = "/images/escritorio.jpeg";
                        huboActualizaciones = true;
                    }
                }
                else if (string.IsNullOrWhiteSpace(prod.Imagen) || prod.Imagen == "/images/Wooden.jpeg")
                {
                    var nuevaImg = ObtenerImagenPorDefecto(prod.Categoria, prod.Nombre);
                    if (prod.Imagen != nuevaImg)
                    {
                        prod.Imagen = nuevaImg;
                        huboActualizaciones = true;
                    }
                }
            }

            if (huboActualizaciones)
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Si ocurre cualquier contingencia, la lista en memoria ya cuenta con la imagen
                }
            }

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
                Nombre = string.IsNullOrWhiteSpace(Nombre) ? "Nuevo Producto" : Nombre.Trim(),
                Codigo = Codigo,
                Descripcion = Descripcion,
                Precio = Precio,
                Categoria = string.IsNullOrWhiteSpace(Categoria) ? "Otro" : Categoria.Trim(),
                Material = Material,
                Medidas = Medidas,
                Estado = "Activo",                    // Estado por defecto al crear
                FechaRegistro = DateTime.Now          // Fecha de creación
            };

            // --- Manejo de subida de imagen ---
            if (Imagen != null && Imagen.Length > 0)
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var uploadsFolder = Path.Combine(webRoot, "uploads", "productos");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var ext = Path.GetExtension(Imagen.FileName);
                    if (string.IsNullOrWhiteSpace(ext))
                    {
                        ext = ".jpg";
                    }

                    var nombreArchivo = Guid.NewGuid().ToString() + ext;
                    var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await Imagen.CopyToAsync(stream);
                    }

                    producto.Imagen = "/uploads/productos/" + nombreArchivo;
                }
                catch
                {
                    producto.Imagen = ObtenerImagenPorDefecto(Categoria, Nombre);
                }
            }
            else
            {
                // Si no se suministra archivo, asignar imagen representativa automáticamente
                producto.Imagen = ObtenerImagenPorDefecto(Categoria, Nombre);
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

            var cat = (producto.Categoria ?? "").ToLowerInvariant();
            var nom = (producto.Nombre ?? "").ToLowerInvariant();
            var esEscritorio = cat.Contains("escritorio") || nom.Contains("escritorio") || nom.Contains("despacho");

            string imagenFinal;
            if (esEscritorio && (string.IsNullOrWhiteSpace(producto.Imagen) || producto.Imagen == "/images/mesac.jpeg" || producto.Imagen == "/images/mesa.jpeg" || producto.Imagen == "/images/Wooden.jpeg"))
            {
                imagenFinal = "/images/escritorio.jpeg";
            }
            else
            {
                imagenFinal = !string.IsNullOrWhiteSpace(producto.Imagen) && producto.Imagen != "/images/Wooden.jpeg" && producto.Imagen != "/images/mesac.jpeg"
                    ? producto.Imagen
                    : ObtenerImagenPorDefecto(producto.Categoria, producto.Nombre);
            }

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
                Imagen = imagenFinal
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

            if (producto == null && RouteData.Values.ContainsKey("id") && int.TryParse(RouteData.Values["id"]?.ToString(), out int rId))
            {
                producto = await _context.Productos.FindAsync(rId);
            }

            // --- Actualizar cada campo con los datos recibidos evitando valores NULL en columnas obligatorias ---
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                producto.Nombre = Nombre.Trim();
            }

            producto.Codigo = Codigo;
            producto.Descripcion = Descripcion;
            producto.Precio = Precio;

            // Evitar que Categoria sea NULL (la columna en BD no admite nulos)
            if (!string.IsNullOrWhiteSpace(Categoria))
            {
                producto.Categoria = Categoria.Trim();
            }
            else if (string.IsNullOrWhiteSpace(producto.Categoria))
            {
                producto.Categoria = "Otro";
            }

            producto.Material = Material;
            producto.Medidas = Medidas;

            if (!string.IsNullOrWhiteSpace(Estado))
            {
                producto.Estado = Estado.Trim();
            }
            else if (string.IsNullOrWhiteSpace(producto.Estado))
            {
                producto.Estado = "Activo";
            }

            // --- Actualizar imagen solo si se subió una nueva ---
            if (Imagen != null && Imagen.Length > 0)
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var uploadsFolder = Path.Combine(webRoot, "uploads", "productos");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var ext = Path.GetExtension(Imagen.FileName);
                    if (string.IsNullOrWhiteSpace(ext))
                    {
                        ext = ".jpg";
                    }

                    // Generar nombre único para el nuevo archivo
                    var nombreArchivo = Guid.NewGuid().ToString() + ext;
                    var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                    // Guardar nuevo archivo en disco
                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await Imagen.CopyToAsync(stream);
                    }

                    // Actualizar la ruta de imagen en el producto
                    producto.Imagen = "/uploads/productos/" + nombreArchivo;
                }
                catch
                {
                    // Si ocurre contingencia de I/O, mantener la imagen previa si ya tenía una
                    if (string.IsNullOrWhiteSpace(producto.Imagen))
                    {
                        producto.Imagen = ObtenerImagenPorDefecto(Categoria, Nombre);
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(producto.Imagen) || producto.Imagen == "/images/mesac.jpeg" || producto.Imagen == "/images/Wooden.jpeg")
            {
                // Si aún no tiene imagen asignada o conservaba una antigua/genérica, colocarle la correspondiente
                producto.Imagen = ObtenerImagenPorDefecto(Categoria, Nombre);
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
