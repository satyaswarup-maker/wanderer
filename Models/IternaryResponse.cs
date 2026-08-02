namespace wanderer_api.Models
{
    public class ItineraryResponse
    {
        public string Overview { get; set; } = string.Empty;
        public List<ItineraryStop> Stops { get; set; } = new();
    }

    public class ItineraryStop
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Tip { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}