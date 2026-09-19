using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CARPINTEC_App.Controllers
{
    public class ChatBotClienteController : Controller
    {
        // GET: ChatBotClienteController
        public ActionResult Index()
        {
            return View();
        }

        // GET: ChatBotClienteController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: ChatBotClienteController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ChatBotClienteController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ChatBotClienteController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: ChatBotClienteController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ChatBotClienteController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: ChatBotClienteController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
