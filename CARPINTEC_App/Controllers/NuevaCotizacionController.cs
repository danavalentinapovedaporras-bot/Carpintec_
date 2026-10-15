using CARPINTEC_App.Data;
using CARPINTEC_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CARPINTEC_App.Controllers
{
    public class NuevaCotizacionController : Controller
    {
        private readonly CarpintecContext _context;

        public NuevaCotizacionController(CarpintecContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /NuevaCotizacion/Index
        // ==========================================
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Obtener usuario en sesión
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                string? correoSession = HttpContext.Session.GetString("CorreoUsuario");
                string? nombreSession = HttpContext.Session.GetString("NombreUsuario");
                string? primerNombreSession = HttpContext.Session.GetString("PrimerNombre");

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

                // 2. Buscar o crear Cliente
                Cliente? cliente = null;
                if (!string.IsNullOrEmpty(correoUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Correo != null && c.Correo.ToLower() == correoUsuario.ToLower());
                }

                if (cliente == null && !string.IsNullOrEmpty(nombreUsuario))
                {
                    cliente = await _context.Clientes.FirstOrDefaultAsync(c =>
                        (c.Nombre != null && c.Nombre.ToLower() == nombreUsuario.ToLower()) ||
                        ((c.Nombre + " " + (c.Apellido ?? "")).Trim().ToLower() == nombreUsuario.ToLower())
                    );
                }

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

                string nombreCompleto = cliente != null ? $"{cliente.Nombre} {cliente.Apellido}".Trim() : (!string.IsNullOrWhiteSpace(nombreUsuario) ? nombreUsuario : "Cliente Carpintec");
                string primerNombre = !string.IsNullOrWhiteSpace(usuario?.Nombre) ? usuario.Nombre : (primerNombreSession ?? "Cliente");
                string iniciales = !string.IsNullOrWhiteSpace(primerNombre) ? primerNombre.Substring(0, 1).ToUpper() : "CL";

                ViewBag.NombreCliente = nombreCompleto;
                ViewBag.CorreoCliente = !string.IsNullOrWhiteSpace(cliente?.Correo) ? cliente.Correo : correoUsuario;
                ViewBag.TelefonoCliente = !string.IsNullOrWhiteSpace(cliente?.Telefono) ? cliente.Telefono : "300 123 4567";
                ViewBag.Iniciales = iniciales;
                ViewBag.IdCliente = cliente?.IdCliente ?? 0;

                return View();
            }
            catch
            {
                ViewBag.NombreCliente = "Cliente Carpintec";
                ViewBag.CorreoCliente = "";
                ViewBag.TelefonoCliente = "";
                ViewBag.Iniciales = "CL";
                return View();
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
        // POST: /NuevaCotizacion/GuardarCotizacion
        // Solo se llama cuando el usuario confirma el pago en el Simulador de Pago
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
                decimal totalNum = ParseDecimalFlexible(total, 418000m);
                if (totalNum <= 0) totalNum = 418000m;

                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                Usuario? usuario = null;
                if (idUsuario.HasValue)
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);
                }

                string correoFinal = (!string.IsNullOrWhiteSpace(correo) ? correo : (usuario?.Correo ?? "")).Trim();
                string nombreFinal = (!string.IsNullOrWhiteSpace(nombreCliente) ? nombreCliente : (usuario != null ? $"{usuario.Nombre} {usuario.Apellido}".Trim() : "Cliente")).Trim();
                string telFinal = (!string.IsNullOrWhiteSpace(telefono) ? telefono : "3001234567").Trim();

                // 1. Obtener o crear Cliente de forma segura sin violar llaves únicas
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

                // 2. Garantizar que exista un Empleado para la FK
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
                        if (!Directory.Exists(carpetaUploads)) Directory.CreateDirectory(carpetaUploads);

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

                // 3. Generar Folio único
                string folioGen;
                do
                {
                    folioGen = $"COT-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
                } while (await _context.Cotizaciones.AnyAsync(c => c.Folio == folioGen));

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

                // Guardar la Cotización
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

                // 4. Crear el Pedido correspondiente con Código único
                string codigoPedidoGen;
                do
                {
                    codigoPedidoGen = $"PED-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
                } while (await _context.Pedidos.AnyAsync(p => p.CodigoPedido == codigoPedidoGen));

                var nuevoPedido = new Pedido
                {
                    CodigoPedido = codigoPedidoGen,
                    Producto = nuevaCotizacion.DetalleProducto,
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

                // 5. Registrar la Venta asociada
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

                return Json(new
                {
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
