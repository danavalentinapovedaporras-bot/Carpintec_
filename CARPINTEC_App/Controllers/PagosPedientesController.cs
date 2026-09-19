using Microsoft.AspNetCore.Mvc;

namespace CARPINTEC_App.Controllers
{
    public class PagosPendientesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}