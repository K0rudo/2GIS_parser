namespace _2GIS_parser.Models
{
    public class NetworkLinkWaypoint
    {
        public int Id { get; set; }
        public int LinkId { get; set; }
        public int OrderIndex { get; set; }
        public decimal Longitude { get; set; }
        public decimal Latitude { get; set; }

        public NetworkLink Link { get; set; } = null!;
    }
}