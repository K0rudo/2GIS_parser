namespace _2GIS_parser.Models
{
    public class NetworkNodeHardware
    {
        public int Id { get; set; }
        public int NodeId { get; set; }
        public string HardwareName { get; set; } = "";
        public string HardwareType { get; set; } = "";

        public NetworkNode Node { get; set; } = null!;
    }
}