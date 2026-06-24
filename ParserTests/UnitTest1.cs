using _2GIS_parser.Controllers;
using _2GIS_parser.Models;
using Xunit;

namespace ParserTests
{
    public class DistanceTests
    {
        //расстояние между двумя известными точками
        [Fact]
        public void CalculateDistance_TwoKnownPoints_ReturnsCorrectDistance()
        {
            decimal lat1 = 52.2978m, lon1 = 104.2964m;
            decimal lat2 = 52.3170m, lon2 = 104.3011m;

            double result = SearchController.CalculateDistance(lat1, lon1, lat2, lon2);

            Assert.InRange(result, 2.0, 3.0);
        }

        //расстояние от точки до самой себя равно 0
        [Fact]
        public void CalculateDistance_SamePoint_ReturnsZero()
        {
            decimal lat = 52.2978m, lon = 104.2964m;

            double result = SearchController.CalculateDistance(lat, lon, lat, lon);

            Assert.Equal(0, result, 5);
        }

        //расстояние симметрично
        [Fact]
        public void CalculateDistance_IsSymmetric()
        {
            decimal lat1 = 52.2978m, lon1 = 104.2964m;
            decimal lat2 = 52.3170m, lon2 = 104.3011m;

            double ab = SearchController.CalculateDistance(lat1, lon1, lat2, lon2);
            double ba = SearchController.CalculateDistance(lat2, lon2, lat1, lon1);

            Assert.Equal(ab, ba, 5);
        }
    }

    public class CsvTests
    {
        //апостроф добавляется перед телефоном
        [Fact]
        public void BuildRow_PhoneContact_HasApostrophe()
        {
            var org = new Organization
            {
                Name = "Тест",
                Contacts = new List<Contact>
                {
                    new Contact { ContactType = "phone", Value = "+79991234567" }
                }
            };
            var fields = new HashSet<string> { "phones" };

            var row = AnalyticsController.BuildRow(org, fields);

            Assert.Single(row);
            Assert.StartsWith("'", row[0]);
        }

        //заголовки CSV формируются корректно
        [Fact]
        public void BuildHeaders_SelectedFields_ReturnsCorrectHeaders()
        {
            var fields = new HashSet<string> { "name", "city", "phones" };

            var headers = AnalyticsController.BuildHeaders(fields);

            Assert.Equal(3, headers.Count);
            Assert.Equal("Название", headers[0]);
            Assert.Equal("Город", headers[1]);
            Assert.Equal("Телефоны", headers[2]);
        }
    }
    public class ContactTypeTests
    {
        // URL с доменом t.me определяется как telegram
        [Fact]
        public void DetermineContactType_TelegramUrl_ReturnsTelegram()
        {
            string result = AnalyticsController.DetermineContactType("https://t.me/svyaztransit");
            Assert.Equal("telegram", result);
        }
        // URL с доменом wa.me определяется как whatsapp
        [Fact]
        public void DetermineContactType_WhatsappUrl_ReturnsWhatsapp()
        {
            string result = AnalyticsController.DetermineContactType("https://wa.me/79991234567");
            Assert.Equal("whatsapp", result);
        }
        // обычный сайт остаётся с типом website
        [Fact]
        public void DetermineContactType_RegularSite_ReturnsWebsite()
        {
            string result = AnalyticsController.DetermineContactType("https://example.ru");
            Assert.Equal("website", result);
        }
    }
    public class ParseJobTests
    {
        // новый ParseJob создаётся со статусом Pending
        [Fact]
        public void ParseJob_DefaultStatus_IsPending()
        {
            var job = new ParseJob();

            Assert.Equal("Pending", job.Status);
        }

        // город и запрос сохраняются в ParseJob корректно
        [Fact]
        public void ParseJob_CityAndQuery_SavedCorrectly()
        {
            var job = new ParseJob
            {
                City = "Иркутск",
                SearchQuery = "бизнес-центры"
            };

            Assert.Equal("Иркутск", job.City);
            Assert.Equal("бизнес-центры", job.SearchQuery);
        }
    }

    public class ModelTests
    {
        // ExternalId сохраняется корректно
        [Fact]
        public void Organization_ExternalId_SavedCorrectly()
        {
            var org = new Organization
            {
                ExternalId = "70000001064168398"
            };

            Assert.NotNull(org.ExternalId);
            Assert.Equal("70000001064168398", org.ExternalId);
        }

        // тип контакта сохраняется корректно
        [Fact]
        public void Contact_ContactType_SavedCorrectly()
        {
            var contact = new Contact
            {
                ContactType = "phone",
                Value = "+79991234567"
            };

            Assert.Equal("phone", contact.ContactType);
            Assert.Equal("+79991234567", contact.Value);
        }
    }
}