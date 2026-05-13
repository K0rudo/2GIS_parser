namespace _2GIS_parser.Models
{
    public class CompanyOffice
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string? Floor { get; set; }
        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }
        public string? Website { get; set; }
        public string? WorkingHours { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}