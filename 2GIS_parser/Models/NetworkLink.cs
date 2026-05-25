namespace _2GIS_parser.Models
{
    public class NetworkLink
    {
        public int Id { get; set; }
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public string LinkType { get; set; } = "";

        public NetworkNode FromNode { get; set; } = null!;
        public NetworkNode ToNode { get; set; } = null!;
        public ICollection<NetworkLinkWaypoint> Waypoints { get; set; } = new List<NetworkLinkWaypoint>();
    }
}