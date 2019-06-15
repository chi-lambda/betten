using Newtonsoft.Json;

namespace betten.WebsocketHandler.Messages
{
    public class DischargePatientParameters
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("dischargedBy")]
        public string DischargedBy { get; set; }
    }
}