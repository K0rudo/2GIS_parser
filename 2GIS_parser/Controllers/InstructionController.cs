using Microsoft.AspNetCore.Mvc;

namespace _2GIS_parser.Controllers
{
    public class InstructionController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Page"] = "instruction";
            ViewData["Title"] = "Инструкция";
            return View();
        }
    }
}