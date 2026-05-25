namespace _2GIS_parser.Models
{
    public class Contact
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string ContactType { get; set; } = "";
        public string Value { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public Organization Organization { get; set; } = null!;
    }
}
