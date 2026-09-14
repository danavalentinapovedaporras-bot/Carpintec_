using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CARPINTEC_App.Controllers
{
    [Authorize]
    public class ManoObraController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}