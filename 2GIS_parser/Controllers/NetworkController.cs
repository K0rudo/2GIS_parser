using Microsoft.AspNetCore.Mvc;

namespace _2GIS_parser.Controllers
{
    public class NetworkController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Маршрутизация";
            ViewData["Page"] = "network";
            return View();
        }
    }
}