using _2GIS_parser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace _2GIS_parser.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly AppDbContext _db;

        public AnalyticsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Аналитика";
            ViewData["Page"] = "analytics";

            // Всего организаций
            ViewBag.TotalOrgs = await _db.Organizations.CountAsync();

            // Всего поисков
            ViewBag.TotalJobs = await _db.ParseJobs.CountAsync();

            // Организаций с контактами
            ViewBag.OrgsWithContacts = await _db.Organizations
                .Where(o => o.Contacts.Any())
                .CountAsync();

            // Средний рейтинг
            ViewBag.AvgRating = await _db.Organizations
                .Where(o => o.Rating.HasValue)
                .AverageAsync(o => (double?)o.Rating) ?? 0;

            // Топ категорий
            ViewBag.TopCategories = await _db.Organizations
                .Where(o => o.CategoryName != null)
                .GroupBy(o => o.CategoryName)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            // Распределение по районам
            ViewBag.ByDistrict = await _db.Organizations
                .Where(o => o.District != null)
                .GroupBy(o => o.District)
                .Select(g => new { District = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Export()
        {
            var orgs = await _db.Organizations
                .Include(o => o.Contacts)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();

            // Заголовок
            sb.AppendLine("Название;Город;Адрес;Категория;Район;Рейтинг;Отзывов;Телефоны;Сайт;Email;Telegram;WhatsApp");

            foreach (var org in orgs)
            {
                var phones = string.Join(", ", org.Contacts.Where(c => c.ContactType == "phone").Select(c => "'" + c.Value));
                var websites = string.Join(", ", org.Contacts.Where(c => c.ContactType == "website").Select(c => c.Value));
                var emails = string.Join(", ", org.Contacts.Where(c => c.ContactType == "email").Select(c => c.Value));
                var telegrams = string.Join(", ", org.Contacts.Where(c => c.ContactType == "telegram").Select(c => c.Value));
                var whatsapps = string.Join(", ", org.Contacts.Where(c => c.ContactType == "whatsapp").Select(c => c.Value));

                sb.AppendLine(string.Join(";", new[]
                {
                    org.Name ?? "",
                    org.City ?? "",
                    org.Address ?? "",
                    org.CategoryName ?? "",
                    org.District ?? "",
                    org.Rating?.ToString("F1") ?? "",
                    org.ReviewCount?.ToString() ?? "",
                    phones,
                    websites,
                    emails,
                    telegrams,
                    whatsapps
                }));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv", $"organizations_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}