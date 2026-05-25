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

        public async Task<IActionResult> Index(
            string? analyticsType,
            string? filterCity,
            string? filterCategory,
            string? filterDistrict,
            bool applied = false)
        {
            ViewData["Title"] = "Аналитика";
            ViewData["Page"] = "analytics";

            // Списки для фильтров
            ViewBag.Cities = await _db.Organizations
                .Where(o => o.City != null)
                .Select(o => o.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Categories = await _db.Organizations
                .Where(o => o.CategoryName != null && o.CategoryName != "")
                .Select(o => o.CategoryName)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Districts = await _db.Organizations
                .Where(o => o.District != null && o.District != "")
                .Select(o => o.District)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.AnalyticsType = analyticsType ?? "overview";
            ViewBag.FilterCity = filterCity;
            ViewBag.FilterCategory = filterCategory;
            ViewBag.FilterDistrict = filterDistrict;

            // Базовый запрос с фильтрами
            var query = _db.Organizations
                .Include(o => o.Contacts)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterCity))
                query = query.Where(o => o.City == filterCity);
            if (!string.IsNullOrEmpty(filterCategory))
                query = query.Where(o => o.CategoryName == filterCategory);
            if (!string.IsNullOrEmpty(filterDistrict))
                query = query.Where(o => o.District == filterDistrict);

            var orgs = await query.ToListAsync();

            // Общие метрики
            ViewBag.TotalOrgs = orgs.Count;
            ViewBag.OrgsWithContacts = orgs.Count(o => o.Contacts.Any());
            ViewBag.AvgRating = orgs.Any(o => o.Rating.HasValue)
                ? Math.Round(orgs.Where(o => o.Rating.HasValue)
                    .Average(o => (double)o.Rating!.Value), 1)
                : 0.0;
            ViewBag.ContactCoverage = orgs.Count > 0
                ? (int)((double)orgs.Count(o => o.Contacts.Any()) / orgs.Count * 100)
                : 0;

            // Данные в зависимости от типа аналитики
            switch (analyticsType ?? "overview")
            {
                case "overview":
                    ViewBag.TopCategories = orgs
                        .Where(o => o.CategoryName != null)
                        .GroupBy(o => o.CategoryName)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(8)
                        .ToList();

                    ViewBag.ByDistrict = orgs
                        .Where(o => o.District != null)
                        .GroupBy(o => o.District)
                        .Select(g => new { District = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    break;

                case "contacts":
                    if (!string.IsNullOrEmpty(filterCategory) || !string.IsNullOrEmpty(filterDistrict))
                    {
                        ViewBag.OrgContacts = orgs
                            .Select(o => new {
                                Name = o.Name ?? "—",
                                Address = o.Address ?? "—",
                                HasPhone = o.Contacts.Any(c => c.ContactType == "phone"),
                                HasSite = o.Contacts.Any(c => c.ContactType == "website"),
                                HasEmail = o.Contacts.Any(c => c.ContactType == "email"),
                                HasTelegram = o.Contacts.Any(c => c.ContactType == "telegram"),
                                HasWhatsApp = o.Contacts.Any(c => c.ContactType == "whatsapp"),
                                Phone = o.Contacts.FirstOrDefault(c => c.ContactType == "phone")?.Value ?? "",
                                Site = o.Contacts.FirstOrDefault(c => c.ContactType == "website")?.Value ?? "",
                            })
                            .OrderBy(o => o.Name)
                            .ToList();
                    }
                    break;

                case "districts":
                    ViewBag.ByDistrict = orgs
                        .Where(o => o.District != null)
                        .GroupBy(o => o.District)
                        .Select(g => new {
                            District = g.Key,
                            Count = g.Count(),
                            WithContacts = g.Count(o => o.Contacts.Any())
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    break;

                case "rating":
                    ViewBag.RatingByDistrict = orgs
                        .Where(o => o.District != null && o.Rating.HasValue)
                        .GroupBy(o => o.District)
                        .Select(g => new {
                            District = g.Key,
                            AvgRating = Math.Round(g.Average(o => (double)o.Rating!.Value), 1),
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.AvgRating)
                        .ToList();
                    break;

                case "top":
                    ViewBag.TopOrgs = orgs
                        .Where(o => o.Rating.HasValue)
                        .OrderByDescending(o => o.Rating)
                        .Take(20)
                        .Select(o => new {
                            Name = o.Name ?? "—",
                            Address = o.Address ?? "—",
                            Category = o.CategoryName ?? "—",
                            District = o.District ?? "—",
                            Rating = (double)o.Rating!.Value,
                            Reviews = o.ReviewCount ?? 0,
                            HasContacts = o.Contacts.Any()
                        })
                        .ToList();
                    break;

                case "categories":
                    ViewBag.CategoriesStats = orgs
                        .Where(o => !string.IsNullOrEmpty(o.CategoryName))
                        .GroupBy(o => o.CategoryName)
                        .Select(g => new {
                            Category = g.Key,
                            Count = g.Count(),
                            AvgRating = g.Any(o => o.Rating.HasValue)
                                ? Math.Round(g.Where(o => o.Rating.HasValue).Average(o => (double)o.Rating!.Value), 1)
                                : 0.0,
                            WithContacts = g.Count(o => o.Contacts.Any()),
                            TotalReviews = g.Sum(o => o.ReviewCount ?? 0)
                        })
                        .OrderByDescending(x => x.AvgRating)
                        .ToList();
                    break;
            }
            ViewBag.Applied = applied;
            return View();
        }

        public async Task<IActionResult> Export(string? fields,string? filterCity,string? filterCategory)
        {
            var query = _db.Organizations
                .Include(o => o.Contacts)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterCity))
                query = query.Where(o => o.City == filterCity);
            if (!string.IsNullOrEmpty(filterCategory))
                query = query.Where(o => o.CategoryName == filterCategory);

            var orgs = await query.ToListAsync();

            var selectedFields = (fields ?? "name,city,address,category,district,rating,reviews,phones,website,email,telegram,whatsapp")
                .Split(',').ToHashSet();

            var sb = new System.Text.StringBuilder();

            // Заголовок
            var headers = new List<string>();
            if (selectedFields.Contains("name")) headers.Add("Название");
            if (selectedFields.Contains("city")) headers.Add("Город");
            if (selectedFields.Contains("address")) headers.Add("Адрес");
            if (selectedFields.Contains("category")) headers.Add("Категория");
            if (selectedFields.Contains("district")) headers.Add("Район");
            if (selectedFields.Contains("rating")) headers.Add("Рейтинг");
            if (selectedFields.Contains("reviews")) headers.Add("Отзывов");
            if (selectedFields.Contains("phones")) headers.Add("Телефоны");
            if (selectedFields.Contains("website")) headers.Add("Сайт");
            if (selectedFields.Contains("email")) headers.Add("Email");
            if (selectedFields.Contains("telegram")) headers.Add("Telegram");
            if (selectedFields.Contains("whatsapp")) headers.Add("WhatsApp");
            sb.AppendLine(string.Join(";", headers));

            foreach (var org in orgs)
            {
                var row = new List<string>();
                if (selectedFields.Contains("name")) row.Add(org.Name ?? "");
                if (selectedFields.Contains("city")) row.Add(org.City ?? "");
                if (selectedFields.Contains("address")) row.Add(org.Address ?? "");
                if (selectedFields.Contains("category")) row.Add(org.CategoryName ?? "");
                if (selectedFields.Contains("district")) row.Add(org.District ?? "");
                if (selectedFields.Contains("rating")) row.Add(org.Rating?.ToString("F1") ?? "");
                if (selectedFields.Contains("reviews")) row.Add(org.ReviewCount?.ToString() ?? "");
                if (selectedFields.Contains("phones")) row.Add(string.Join(", ", org.Contacts.Where(c => c.ContactType == "phone").Select(c => "'" + c.Value)));
                if (selectedFields.Contains("website")) row.Add(string.Join(", ", org.Contacts.Where(c => c.ContactType == "website").Select(c => c.Value)));
                if (selectedFields.Contains("email")) row.Add(string.Join(", ", org.Contacts.Where(c => c.ContactType == "email").Select(c => c.Value)));
                if (selectedFields.Contains("telegram")) row.Add(string.Join(", ", org.Contacts.Where(c => c.ContactType == "telegram").Select(c => c.Value)));
                if (selectedFields.Contains("whatsapp")) row.Add(string.Join(", ", org.Contacts.Where(c => c.ContactType == "whatsapp").Select(c => c.Value)));
                sb.AppendLine(string.Join(";", row));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv", $"organizations_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}