namespace _2GIS_parser.Models
{
    public class NetworkNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string NodeType { get; set; } = "";
        public string? Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int Connections { get; set; }
        public bool IsActive { get; set; }

        public ICollection<NetworkNodeHardware> Hardware { get; set; } = new List<NetworkNodeHardware>();
        public ICollection<NetworkLink> LinksFrom { get; set; } = new List<NetworkLink>();
        public ICollection<NetworkLink> LinksTo { get; set; } = new List<NetworkLink>();
    }
}