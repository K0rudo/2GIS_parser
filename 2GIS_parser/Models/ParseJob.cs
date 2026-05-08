namespace _2GIS_parser.Models
{
    public class ParseJob
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string City { get; set; } = "";
        public string SearchQuery { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public int TotalFound { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
