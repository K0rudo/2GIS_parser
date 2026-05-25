using _2GIS_parser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace _2GIS_parser.Controllers
{
    public class NetworkController : Controller
    {
        private readonly AppDbContext _db;

        public NetworkController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Маршрутизация";
            ViewData["Page"] = "network";

            var nodes = await _db.NetworkNodes
                .Include(n => n.Hardware)
                .ToListAsync();

            var links = await _db.NetworkLinks
                .Include(l => l.Waypoints.OrderBy(w => w.OrderIndex))
                .ToListAsync();

            ViewBag.Nodes = nodes;
            ViewBag.Links = links;

            return View();
        }
    }
}