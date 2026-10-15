using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    public class MiCotizacionController : Controller
    {
        private readonly CarpintecContext _context;

        public MiCotizacionController(CarpintecContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /MiCotizacion/Index
        // ==========================================
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Obtener datos del usuario en sesión o a través de claims JWT
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");

                if (!idUsuario.HasValue && User.Identity?.IsAuthenticated == true)
                {
                    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value;
                    if (int.TryParse(claimId, out int parsedId))
                    {
                        idUsuario = parsedId;
                    }
                }

                Usuario? usuario = null;
                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                if (usuario == null && User.Identity?.IsAuthenticated == true)
                {
                    var claimEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(claimEmail))
                    {
                        usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo != null && u.Correo.ToLower() == claimEmail.ToLower());
                    }
                }

                string correoUsuario = (usuario?.Correo ?? correoSession ?? "").Trim();
                string nombreUsuario = (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : (nombreSession ?? "")).Trim();

                // 2. Buscar o sincronizar el registro en la tabla Clientes
                Cliente? cliente = null;

                if (!string.IsNullOrEmpty(correoUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoUsuario.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreUsuario.ToLower()) ||
                        (c.Contacto != null && c.Contacto.ToLower() == nombreUsuario.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreUsuario.ToLower())
                    );
                }

                // Si el usuario existe en sesión pero no tiene registro en Clientes, lo creamos para asignarle su propio IdCliente
                if (cliente == null && usuario != null)
                {
                    cliente = new Cliente
                    {
                        Nombre = usuario.Nombre,
                        Apellido = usuario.Apellido,
                        Correo = usuario.Correo ?? "",
                        Telefono = "",
                        TipoCliente = "Natural",
                        Contacto = $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                        Estado = "Activo",
                        FechaRegistro = DateTime.Now
                    };
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync();
                }

                // 3. Preparar ViewBag para el formulario y cabecera
                ViewBag.NombreCliente = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : nombreUsuario;
                ViewBag.CorreoCliente = !string.IsNullOrEmpty(cliente?.Correo) ? cliente.Correo : correoUsuario;
                ViewBag.TelefonoCliente = cliente?.Telefono ?? "";
                ViewBag.IdCliente = cliente?.IdCliente ?? 0;

                // 4. Consultar las cotizaciones del cliente desde la base de datos
                List<Cotizacion> cotizaciones = new List<Cotizacion>();

                if (cliente != null || !string.IsNullOrEmpty(correoUsuario) || !string.IsNullOrEmpty(nombreUsuario))
                {
                    int idCliente = cliente?.IdCliente ?? 0;
                    string correoFiltro = correoUsuario.ToLower();
                    string nombreFiltro = nombreUsuario.ToLower();

                    cotizaciones = await _context.Cotizaciones
                        .Include(c => c.IdClienteNavigation)
                        .Include(c => c.DetalleCotizacions)
                            .ThenInclude(d => d.IdProductoNavigation)
                        .Where(c =>
                            (idCliente > 0 && c.IdCliente == idCliente) ||
                            (!string.IsNullOrEmpty(correoFiltro) && c.CorreoCliente != null && c.CorreoCliente.ToLower() == correoFiltro) ||
                            (!string.IsNullOrEmpty(nombreFiltro) && c.NombreCliente != null && c.NombreCliente.ToLower() == nombreFiltro)
                        )
                        .OrderByDescending(c => c.FechaRegistro)
                        .ThenByDescending(c => c.IdCotizacion)
                        .ToListAsync();
                }

                // 5. Calcular métricas para las tarjetas de resumen
                ViewBag.TotalCotizaciones = cotizaciones.Count;
                ViewBag.Aprobadas = cotizaciones.Count(c => c.Estado != null && (c.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Pagada", StringComparison.OrdinalIgnoreCase) || c.Estado.Equals("Finalizada", StringComparison.OrdinalIgnoreCase)));
                ViewBag.Pendientes = cotizaciones.Count(c => c.Estado != null && c.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase));

                return View(cotizaciones);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar cotizaciones: " + ex.Message;
                ViewBag.TotalCotizaciones = 0;
                ViewBag.Aprobadas = 0;
                ViewBag.Pendientes = 0;
                return View(new List<Cotizacion>());
            }
        }

        // ==========================================
        private static decimal ParseDecimalFlexible(string? input, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            string clean = input.Replace("$", "").Replace("COP", "").Replace("cop", "").Trim();
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valInv))
            {
                return valInv;
            }
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal valCur))
            {
                return valCur;
            }
            string cleanThousands = clean.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(cleanThousands, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valClean))
            {
                return valClean;
            }
            return fallback;
        }

        // ==========================================
        // POST: /MiCotizacion/GuardarCotizacion
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GuardarCotizacion(
             string? nombreCliente,
             string? correo,
             string? telefono,
             string? producto,
             string? madera,
             string? cantidad,
             string? alto,
             string? ancho,
             string? profundidad,
             string? tiempoEntrega,
             string? observaciones,
             string? total,
             IFormFile? fotoLugar,
             string? tipoPago = "Contado",
             int? numeroCuotas = null,
             string? municipio = null,
             string? ciudad = null,
             string? codigoPostal = null,
             string? direccion = null,
             string? localidad = null,
             string? pisoDepto = null,
             string? hotelEmpresa = null,
             string? tipoLugar = null,
             string? metodoPago = null,
             string? costoEnvio = null,
             string? titularTarjeta = null,
             string? bancoPse = null,
             string? tipoClientePse = null,
             string? docPse = null,
             string? correoPse = null,
             string? telefonoNequi = null,
             string? telefonoDaviplata = null)
        {
            try
            {
                string productoFinal = !string.IsNullOrWhiteSpace(producto) ? producto.Trim() : "Mueble de Madera Personalizado";
                string maderaFinal = !string.IsNullOrWhiteSpace(madera) ? madera.Trim() : "Pino Macizo";

                int cantidadFinal = 1;
                if (!string.IsNullOrWhiteSpace(cantidad) && int.TryParse(cantidad.Trim(), out int cantParsed) && cantParsed > 0)
                {
                    cantidadFinal = cantParsed;
                }

                decimal altoNum = ParseDecimalFlexible(alto, 80m);
                decimal anchoNum = ParseDecimalFlexible(ancho, 120m);
                decimal profNum = ParseDecimalFlexible(profundidad, 60m);
                decimal totalNum = ParseDecimalFlexible(total, 320000m);
                if (totalNum <= 0) totalNum = 320000m;

                // 1. Identificar o vincular cliente actual
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue && User.Identity?.IsAuthenticated == true)
                {
                    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                    if (int.TryParse(claimId, out int parsedId)) idUsuario = parsedId;
                }

                Usuario? usuario = null;
                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                string correoFinal = (!string.IsNullOrWhiteSpace(correo) ? correo : (usuario?.Correo ?? "")).Trim();
                string nombreFinal = (!string.IsNullOrWhiteSpace(nombreCliente) ? nombreCliente : (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : "Cliente")).Trim();
                string telFinal = (!string.IsNullOrWhiteSpace(telefono) ? telefono : "3001234567").Trim();

                Cliente? cliente = null;
                if (usuario != null && !string.IsNullOrEmpty(usuario.Correo))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(correoFinal))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoFinal.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreFinal))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreFinal.ToLower()) ||
                        (c.Contacto != null && c.Contacto.ToLower() == nombreFinal.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreFinal.ToLower())
                    );
                }

                if (cliente == null && string.IsNullOrEmpty(correoFinal) && string.IsNullOrEmpty(nombreFinal))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync();
                }

                // Si no existe el cliente en la BD, crearlo para persistir sus datos
                if (cliente == null)
                {
                    string correoClienteNuevo = !string.IsNullOrWhiteSpace(correoFinal) ? correoFinal : $"cliente_{DateTime.Now.Ticks}@carpintec.com";
                    int sufijo = 1;
                    while (await _context.Clientes.AnyAsync(c => c.Correo == correoClienteNuevo))
                    {
                        correoClienteNuevo = $"cliente_{DateTime.Now.Ticks}_{sufijo++}@carpintec.com";
                    }

                    string docNuevo = "100" + Random.Shared.Next(100000, 999999);
                    while (await _context.Clientes.AnyAsync(c => c.Documento == docNuevo))
                    {
                        docNuevo = "100" + Random.Shared.Next(100000, 999999);
                    }

                    cliente = new Cliente
                    {
                        Nombre = !string.IsNullOrWhiteSpace(nombreFinal) ? nombreFinal : (usuario?.Nombre ?? "Cliente"),
                        Apellido = usuario?.Apellido ?? "General",
                        Correo = correoClienteNuevo,
                        Telefono = !string.IsNullOrWhiteSpace(telFinal) ? telFinal : "3001234567",
                        TipoCliente = "Natural",
                        Contacto = !string.IsNullOrWhiteSpace(nombreFinal) ? nombreFinal : "Cliente General",
                        Estado = "Activo",
                        Documento = docNuevo,
                        FechaRegistro = DateTime.Now
                    };
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync();
                }

                int idClienteFinal = cliente.IdCliente;

                // 2. Asignar un empleado válido
                var empleadoDb = await _context.Empleados.FirstOrDefaultAsync();
                int idEmpleadoValido;
                if (empleadoDb != null)
                {
                    idEmpleadoValido = empleadoDb.IdEmpleado;
                }
                else
                {
                    string docEmp = "10" + Random.Shared.Next(100000, 999999);
                    var nuevoEmp = new Empleado
                    {
                        Nombre = "Asesor",
                        Apellido = "Carpintec",
                        Cargo = "Ventas",
                        TipoDocumento = "CC",
                        Documento = docEmp,
                        Correo = "ventas@carpintec.com",
                        Telefono = "3000000000",
                        FechaIngreso = DateOnly.FromDateTime(DateTime.Now),
                        Estado = "Activo"
                    };
                    _context.Empleados.Add(nuevoEmp);
                    await _context.SaveChangesAsync();
                    idEmpleadoValido = nuevoEmp.IdEmpleado;
                }

                // 3. Manejo opcional de foto subida
                if (fotoLugar != null && fotoLugar.Length > 0)
                {
                    try
                    {
                        string carpetaUploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(carpetaUploads))
                        {
                            Directory.CreateDirectory(carpetaUploads);
                        }

                        string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(fotoLugar.FileName);
                        string rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

                        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await fotoLugar.CopyToAsync(stream);
                        }
                    }
                    catch
                    {
                        // Si falla la subida de foto, no interrumpimos la cotización
                    }
                }

                bool esContado = string.Equals(tipoPago, "Contado", StringComparison.OrdinalIgnoreCase);
                decimal costoEnvioNum = ParseDecimalFlexible(costoEnvio, 0m);

                // Determinar el método de pago detallado
                string metodoPagoFinal;
                if (!esContado)
                {
                    metodoPagoFinal = $"Crédito a {numeroCuotas ?? 3} cuotas";
                }
                else if (!string.IsNullOrWhiteSpace(metodoPago))
                {
                    metodoPagoFinal = metodoPago.ToUpper() switch
                    {
                        "PSE" => $"PSE - {(!string.IsNullOrWhiteSpace(bancoPse) ? bancoPse : "Banco")} (Doc: {docPse ?? "N/A"})",
                        "NEQUI" => $"Nequi ({(!string.IsNullOrWhiteSpace(telefonoNequi) ? telefonoNequi : telFinal)})",
                        "DAVIPLATA" => $"Daviplata ({(!string.IsNullOrWhiteSpace(telefonoDaviplata) ? telefonoDaviplata : telFinal)})",
                        "TARJETA" => $"Tarjeta {(!string.IsNullOrWhiteSpace(titularTarjeta) ? titularTarjeta : "Titular")}",
                        _ => metodoPago
                    };
                }
                else
                {
                    metodoPagoFinal = "Tarjeta (Simulado)";
                }

                // Armar resumen completo de despacho al cliente
                string ciudadFinal = !string.IsNullOrWhiteSpace(ciudad) ? ciudad : (!string.IsNullOrWhiteSpace(municipio) ? municipio : "Bogotá D.C.");
                string detalleDespacho = $"Envío: {(costoEnvioNum > 0 ? costoEnvioNum.ToString("C0") : "Estándar")}. Destino: {direccion ?? "Dirección del cliente"}, Barrio/Localidad: {localidad ?? "N/A"}, {municipio ?? ciudadFinal}, CP: {codigoPostal ?? "N/A"}. Complemento: {pisoDepto ?? "N/A"}. Lugar/Empresa: {hotelEmpresa ?? tipoLugar ?? "Particular"}.";

                // Actualizar dirección del cliente si existe
                if (cliente != null)
                {
                    if (!string.IsNullOrWhiteSpace(direccion)) cliente.Direccion = direccion;
                    if (!string.IsNullOrWhiteSpace(ciudadFinal)) cliente.Ciudad = ciudadFinal;
                    if (!string.IsNullOrWhiteSpace(hotelEmpresa)) cliente.NombreEmpresa = hotelEmpresa;
                    _context.Clientes.Update(cliente);
                }

                // 4. Folio único
                string folioGen;
                do
                {
                    folioGen = $"COT-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
                } while (await _context.Cotizaciones.AnyAsync(c => c.Folio == folioGen));

                var nuevaCotizacion = new Cotizacion
                {
                    Folio = folioGen,
                    IdCliente = idClienteFinal,
                    IdEmpleado = idEmpleadoValido,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    Total = totalNum,
                    Estado = esContado ? "Aprobada" : "Pendiente",
                    NombreCliente = nombreFinal,
                    CorreoCliente = correoFinal,
                    TelefonoCliente = telFinal,
                    DetalleProducto = productoFinal,
                    TipoMadera = maderaFinal,
                    Cantidad = cantidadFinal,
                    Medidas = $"Alto: {altoNum}cm, Ancho: {anchoNum}cm, Prof: {profNum}cm",
                    TiempoEntrega = tiempoEntrega ?? "15 días hábiles",
                    ObservacionesAdicionales = $"{(observaciones ?? "")} | [Despacho: {detalleDespacho}] | Método: {metodoPagoFinal}".Trim(' ', '|'),
                    FechaRegistro = DateTime.Now
                };

                _context.Cotizaciones.Add(nuevaCotizacion);
                await _context.SaveChangesAsync();

                // Crear Pedido vinculado con código único
                string codigoPedidoGen;
                do
                {
                    codigoPedidoGen = $"PED-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
                } while (await _context.Pedidos.AnyAsync(p => p.CodigoPedido == codigoPedidoGen));

                var nuevoPedido = new Pedido
                {
                    CodigoPedido = codigoPedidoGen,
                    Producto = !string.IsNullOrWhiteSpace(nuevaCotizacion.DetalleProducto) ? nuevaCotizacion.DetalleProducto : productoFinal,
                    IdCotizacion = nuevaCotizacion.IdCotizacion,
                    IdCliente = idClienteFinal,
                    FechaSolicitud = DateOnly.FromDateTime(DateTime.Now),
                    FechaEntrega = DateOnly.FromDateTime(DateTime.Now.AddDays(15)),
                    Estado = esContado ? "En Preparación" : "Pendiente",
                    ValorTotal = nuevaCotizacion.Total,
                    Observaciones = $"Pedido registrado con pago ({metodoPagoFinal}). Madera: {nuevaCotizacion.TipoMadera}, Medidas: {nuevaCotizacion.Medidas}. {detalleDespacho}",
                    FechaRegistro = DateTime.Now
                };

                _context.Pedidos.Add(nuevoPedido);
                await _context.SaveChangesAsync();

                // Registrar Venta
                try
                {
                    string numFactura;
                    do
                    {
                        numFactura = $"FAC-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
                    } while (await _context.Ventas.AnyAsync(v => v.NumeroFactura == numFactura));

                    var nuevaVenta = new Venta
                    {
                        IdPedido = nuevoPedido.IdPedido,
                        IdCliente = idClienteFinal,
                        FechaVenta = DateTime.Now,
                        NumeroFactura = numFactura,
                        Subtotal = Math.Round(nuevaCotizacion.Total / 1.19m, 2),
                        IVA = Math.Round(nuevaCotizacion.Total - (nuevaCotizacion.Total / 1.19m), 2),
                        Total = nuevaCotizacion.Total,
                        MetodoPago = metodoPagoFinal,
                        Estado = esContado ? "Pagada" : "Pendiente",
                        Observaciones = $"Venta registrada mediante simulador de pago con despacho a {ciudadFinal}. {detalleDespacho}"
                    };
                    _context.Ventas.Add(nuevaVenta);
                    await _context.SaveChangesAsync();
                }
                catch { }

                return Json(new { 
                    success = true, 
                    idCotizacion = nuevaCotizacion.IdCotizacion,
                    idPedido = nuevoPedido.IdPedido,
                    codigoPedido = nuevoPedido.CodigoPedido,
                    folioCotizacion = nuevaCotizacion.Folio,
                    estadoPedido = nuevoPedido.Estado,
                    message = esContado
                        ? "¡Pago procesado exitosamente! Tu cotización y pedido ya están registrados y en preparación de fabricación."
                        : "¡Solicitud registrada con éxito! Tu plan de cuotas está pendiente de validación.",
                    redirectUrl = "/Mi_Pedido"
                });
            }
            catch (Exception ex)
            {
                string mensajeError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error al procesar el pago y guardar la cotización: " + mensajeError });
            }
        }

        // ==========================================
        // GET: /MiCotizacion/ObtenerDetalle/5
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ObtenerDetalle(int id)
        {
            try
            {
                var cotizacion = await _context.Cotizaciones
                    .Include(c => c.IdClienteNavigation)
                    .Include(c => c.DetalleCotizacions)
                        .ThenInclude(d => d.IdProductoNavigation)
                    .FirstOrDefaultAsync(c => c.IdCotizacion == id);

                if (cotizacion == null)
                {
                    return Json(new { success = false, message = "Cotización no encontrada." });
                }

                string nombreProd = !string.IsNullOrWhiteSpace(cotizacion.DetalleProducto)
                    ? cotizacion.DetalleProducto
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.IdProductoNavigation?.Nombre ?? "Mueble personalizado");

                string tipoMad = !string.IsNullOrWhiteSpace(cotizacion.TipoMadera)
                    ? cotizacion.TipoMadera
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.IdProductoNavigation?.Material ?? "Pino Macizo");

                int cant = cotizacion.Cantidad ?? cotizacion.DetalleCotizacions.FirstOrDefault()?.Cantidad ?? 1;
                string med = !string.IsNullOrWhiteSpace(cotizacion.Medidas)
                    ? cotizacion.Medidas
                    : (cotizacion.DetalleCotizacions.FirstOrDefault()?.Medidas ?? "N/A");

                string nomCliente = !string.IsNullOrWhiteSpace(cotizacion.NombreCliente)
                    ? cotizacion.NombreCliente
                    : ($"{cotizacion.IdClienteNavigation?.Nombre} {cotizacion.IdClienteNavigation?.Apellido}".Trim());

                string corCliente = !string.IsNullOrWhiteSpace(cotizacion.CorreoCliente)
                    ? cotizacion.CorreoCliente
                    : (cotizacion.IdClienteNavigation?.Correo ?? "");

                string telCliente = !string.IsNullOrWhiteSpace(cotizacion.TelefonoCliente)
                    ? cotizacion.TelefonoCliente
                    : (cotizacion.IdClienteNavigation?.Telefono ?? "");

                return Json(new
                {
                    success = true,
                    folio = cotizacion.Folio,
                    fecha = cotizacion.Fecha.ToString("yyyy-MM-dd"),
                    cliente = string.IsNullOrWhiteSpace(nomCliente) ? "Cliente General" : nomCliente,
                    correo = corCliente,
                    telefono = telCliente,
                    producto = nombreProd,
                    madera = tipoMad,
                    cantidad = cant,
                    medidas = med,
                    total = cotizacion.Total.ToString("C0", new System.Globalization.CultureInfo("es-CO")),
                    estado = cotizacion.Estado ?? "Pendiente",
                    observaciones = string.IsNullOrWhiteSpace(cotizacion.ObservacionesAdicionales) ? "Sin observaciones adicionales." : cotizacion.ObservacionesAdicionales,
                    tiempoEntrega = cotizacion.TiempoEntrega ?? "15 días hábiles"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}