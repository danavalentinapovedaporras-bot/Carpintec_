using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers
{
    public class MisProductosController : Controller
    {
        private readonly CarpintecContext _context;

        public MisProductosController(CarpintecContext context)
        {
            _context = context;
        }

        // GET: MisProductosController
        public async Task<IActionResult> Index()
        {
            try
            {
                // Cargar datos del usuario/cliente en sesión para autocompletar el modal de cotización
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoUsuario = null;
                string? nombreUsuario = HttpContext.Session.GetString("NombreUsuario");
                Cliente? cliente = null;

                if (idUsuario.HasValue)
                {
                    var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                    if (usuario != null)
                    {
                        correoUsuario = usuario.Correo;
                        cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                            (c.Correo != null && usuario.Correo != null && c.Correo.ToLower() == usuario.Correo.ToLower()) ||
                            (c.Nombre != null && usuario.Nombre != null && c.Nombre.ToLower() == usuario.Nombre.ToLower())
                        );
                    }
                }

                if (cliente == null)
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync();
                }

                ViewBag.NombreCliente = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : (nombreUsuario ?? "");
                ViewBag.CorreoCliente = cliente?.Correo ?? correoUsuario ?? "";
                ViewBag.TelefonoCliente = cliente?.Telefono ?? "";
                ViewBag.IdCliente = cliente?.IdCliente ?? 1;

                return View();
            }
            catch
            {
                return View();
            }
        }

        // POST: MisProductosController/GuardarCotizacion
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
             int? numeroCuotas = null)
        {
            try
            {
                string productoFinal = !string.IsNullOrWhiteSpace(producto) ? producto.Trim() : "Producto de Catálogo Carpintec";
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

                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                Usuario? usuario = null;

                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                string correoFinal = (!string.IsNullOrWhiteSpace(correo) ? correo : (usuario?.Correo ?? "")).Trim();
                string nombreFinal = (!string.IsNullOrWhiteSpace(nombreCliente) ? nombreCliente : (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : "Cliente")).Trim();
                string telFinal = (!string.IsNullOrWhiteSpace(telefono) ? telefono : "3001234567").Trim();

                // 1. Obtener o crear Cliente de forma segura
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

                // 2. Empleado válido
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
                    catch { }
                }

                bool esContado = string.Equals(tipoPago, "Contado", StringComparison.OrdinalIgnoreCase);

                // 3. Folio único
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
                    NombreCliente = !string.IsNullOrWhiteSpace(nombreFinal) ? nombreFinal : (cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : "Cliente General"),
                    CorreoCliente = correoFinal,
                    TelefonoCliente = telFinal,
                    DetalleProducto = productoFinal,
                    TipoMadera = maderaFinal,
                    Cantidad = cantidadFinal,
                    Medidas = $"Alto: {altoNum}cm, Ancho: {anchoNum}cm, Prof: {profNum}cm",
                    TiempoEntrega = tiempoEntrega ?? "15 días hábiles",
                    ObservacionesAdicionales = observaciones ?? "",
                    FechaRegistro = DateTime.Now
                };

                _context.Cotizaciones.Add(nuevaCotizacion);
                await _context.SaveChangesAsync();

                // 4. Código Pedido único
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
                    Observaciones = $"Pedido generado con pago confirmado ({tipoPago}) desde Catálogo. Madera: {nuevaCotizacion.TipoMadera}, Medidas: {nuevaCotizacion.Medidas}.",
                    FechaRegistro = DateTime.Now
                };

                _context.Pedidos.Add(nuevoPedido);
                await _context.SaveChangesAsync();

                // 5. Registrar Venta
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
                        MetodoPago = esContado ? "Tarjeta (Simulado)" : $"Crédito a {numeroCuotas ?? 3} cuotas",
                        Estado = esContado ? "Pagada" : "Pendiente",
                        Observaciones = $"Venta registrada mediante simulador de pago en cotización {nuevaCotizacion.Folio}"
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
    }
}
