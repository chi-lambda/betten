using Newtonsoft.Json;

namespace betten.WebsocketHandler.Messages
{
    public class CreateBedsParameter
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}