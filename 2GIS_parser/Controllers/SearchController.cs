using _2GIS_parser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace _2GIS_parser.Controllers
{
    public class SearchController : Controller
    {
        private readonly AppDbContext _db;
        private readonly string _pythonPath;
        public SearchController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _pythonPath = config["Parser:PythonPath"] ?? "py";
        }

        public IActionResult Index()
        {
            ViewData["Page"] = "search"; // ← добавить
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Search(string city, string query)
        {
            var job = new ParseJob
            {
                City = city,
                SearchQuery = query,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };
            _db.ParseJobs.Add(job);
            await _db.SaveChangesAsync();

            var parserPath = Path.Combine(Directory.GetCurrentDirectory(), "Parser", "parser.py");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{parserPath}\" {job.Id} \"{city}\" \"{query}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            //string output = await process.StandardOutput.ReadToEndAsync();
            //string errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Запускаем обогащение контактами в фоне (не ждём)
            var enrichPath = Path.Combine(Directory.GetCurrentDirectory(), "Parser", "enrich_contacts.py");
            var enrichProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{enrichPath}\" {job.Id}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Parser")
                }
            };
            enrichProcess.Start();
            //TempData["Debug"] = $"OUT: {output} | ERR: {errors}";

            return RedirectToAction("Results", new { jobId = job.Id });
        }

        public async Task<IActionResult> Results(int jobId, string? sortBy, string? filterCategory, string? filterDistrict, int? officeId)
        {
            var job = await _db.ParseJobs.FindAsync(jobId);
            if (job == null) return NotFound();

            var orgIds = await _db.ParseJobOrganizations
                .Where(x => x.ParseJobId == jobId)
                .Select(x => x.OrganizationId)
                .ToListAsync();

            var query2 = _db.Organizations
                .Include(o => o.Contacts)
                .Where(o => orgIds.Contains(o.Id));

            if (!string.IsNullOrEmpty(filterDistrict))
                query2 = query2.Where(o => o.District == filterDistrict);

            query2 = sortBy switch
            {
                "name" => query2.OrderBy(o => o.Name),
                "rating" => query2.OrderByDescending(o => o.Rating),
                "reviews" => query2.OrderByDescending(o => o.ReviewCount),
                _ => query2.OrderBy(o => o.Name)
            };

            // Сортировка по удалённости — после основного запроса
            var organizations = await query2.ToListAsync();

            if (sortBy == "distance" && officeId.HasValue)
            {
                var office = await _db.CompanyOffices.FindAsync(officeId.Value);
                if (office != null)
                {
                    organizations = organizations
                        .OrderBy(o => CalculateDistance(
                            o.Latitude ?? 0, o.Longitude ?? 0,
                            office.Latitude, office.Longitude))
                        .ToList();
                }
            }
            var districts = await _db.Organizations
                .Where(o => orgIds.Contains(o.Id) && o.District != null)
                .Select(o => o.District)
                .Distinct()
                .ToListAsync();

            ViewBag.Districts = districts;
            ViewBag.FilterDistrict = filterDistrict;

            ViewBag.Job = job;
            ViewBag.SortBy = sortBy;
            ViewBag.OfficeId = officeId;

            var offices = await _db.CompanyOffices.ToListAsync();
            ViewBag.Offices = offices;

            return View(organizations);
        }

        [HttpGet]
        public async Task<IActionResult> Contacts(int jobId)
        {
            var orgIds = await _db.ParseJobOrganizations
                .Where(x => x.ParseJobId == jobId)
                .Select(x => x.OrganizationId)
                .ToListAsync();

            var contacts = await _db.Contacts
                .Where(c => orgIds.Contains(c.OrganizationId))
                .Select(c => new {
                    orgId = c.OrganizationId,
                    type = c.ContactType,
                    value = c.Value
                })
                .ToListAsync();

            return Json(contacts);
        }

        private static double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            const double R = 6371;
            var dLat = ((double)(lat2 - lat1)) * Math.PI / 180;
            var dLon = ((double)(lon2 - lon1)) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos((double)lat1 * Math.PI / 180) * Math.Cos((double)lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}