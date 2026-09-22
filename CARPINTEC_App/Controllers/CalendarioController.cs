using CARPINTEC_App.Models;
using CARPINTEC_App.Data;
using CARPINTEC_App.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Controllers;

[Authorize]
[PermisoCargo("Producción")]
public class CalendarioController : Controller
{
    private static readonly HashSet<string> CategoriasValidas =
    [
        "corte", "lijado", "ensamblado", "acabado", "entrega", "reunion", "otro"
    ];

    private readonly CarpintecContext _context;

    public CalendarioController(CarpintecContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerAnio(int anio)
    {
        var inicio = new DateOnly(anio, 1, 1);
        var fin = new DateOnly(anio, 12, 31);

        var actividades = await _context.ActividadTallers
            .AsNoTracking()
            .Where(a => a.Fecha >= inicio && a.Fecha <= fin)
            .OrderBy(a => a.Fecha)
            .ThenBy(a => a.IdActividad)
            .Select(a => new
            {
                a.IdActividad,
                a.Fecha,
                a.Categoria,
                a.Texto
            })
            .ToListAsync();

        var resultado = actividades
            .GroupBy(a => a.Fecha.ToString("yyyy-MM-dd"))
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new
                {
                    id = a.IdActividad.ToString(),
                    cat = a.Categoria,
                    text = a.Texto
                }).ToList()
            );

        return Json(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Agregar([FromBody] AgregarActividadRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Fecha) ||
            string.IsNullOrWhiteSpace(request.Texto) ||
            string.IsNullOrWhiteSpace(request.Cat))
        {
            return BadRequest(new { error = "Datos incompletos." });
        }

        if (!DateOnly.TryParse(request.Fecha, out var fecha))
        {
            return BadRequest(new { error = "Fecha inválida." });
        }

        var categoria = request.Cat.Trim().ToLowerInvariant();
        if (!CategoriasValidas.Contains(categoria))
        {
            return BadRequest(new { error = "Categoría inválida." });
        }

        var texto = request.Texto.Trim();
        if (texto.Length > 140)
        {
            return BadRequest(new { error = "La actividad no puede superar 140 caracteres." });
        }

        var actividad = new ActividadTaller
        {
            Fecha = fecha,
            Categoria = categoria,
            Texto = texto,
            IdUsuario = HttpContext.Session.GetInt32("IdUsuario"),
            FechaRegistro = DateTime.Now
        };

        _context.ActividadTallers.Add(actividad);
        await _context.SaveChangesAsync();

        return Json(new
        {
            id = actividad.IdActividad.ToString(),
            cat = actividad.Categoria,
            text = actividad.Texto
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Eliminar(int id)
    {
        var actividad = await _context.ActividadTallers.FindAsync(id);
        if (actividad == null)
        {
            return NotFound(new { error = "Actividad no encontrada." });
        }

        _context.ActividadTallers.Remove(actividad);
        await _context.SaveChangesAsync();

        return Json(new { ok = true });
    }

    public class AgregarActividadRequest
    {
        public string Fecha { get; set; } = "";
        public string Cat { get; set; } = "";
        public string Texto { get; set; } = "";
    }
}
