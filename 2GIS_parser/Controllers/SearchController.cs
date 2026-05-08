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
            if (System.IO.File.Exists(enrichPath))
            {
                var enrichProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"\"{enrichPath}\" \"{city}\" \"{query}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                enrichProcess.Start();
                // Намеренно не ждём — работает в фоне
            }
            //TempData["Debug"] = $"OUT: {output} | ERR: {errors}";

            return RedirectToAction("Results", new { jobId = job.Id });
        }

        public async Task<IActionResult> Results(int jobId, string? sortBy, string? filterCategory)
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

            if (!string.IsNullOrEmpty(filterCategory))
                query2 = query2.Where(o => o.CategoryName == filterCategory);

            query2 = sortBy switch
            {
                "name" => query2.OrderBy(o => o.Name),
                "rating" => query2.OrderByDescending(o => o.Rating),
                "reviews" => query2.OrderByDescending(o => o.ReviewCount),
                _ => query2.OrderBy(o => o.Name)
            };

            var organizations = await query2.ToListAsync();

            var categories = await _db.Organizations
                .Where(o => orgIds.Contains(o.Id))
                .Select(o => o.CategoryName)
                .Distinct()
                .ToListAsync();

            ViewBag.Job = job;
            ViewBag.Categories = categories;
            ViewBag.SortBy = sortBy;
            ViewBag.FilterCategory = filterCategory;

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
    }
}