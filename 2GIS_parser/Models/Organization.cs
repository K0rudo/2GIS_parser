namespace _2GIS_parser.Models
{
    public class Organization
    {
        public int Id { get; set; }
        public string? ExternalId { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? CategoryName { get; set; }
        public decimal? Rating { get; set; }
        public int? ReviewCount { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? WorkingHours { get; set; }
        public string Source { get; set; } = "2GIS";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    }
}
