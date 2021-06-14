using System;
using Newtonsoft.Json;

namespace vaccine_watcher
{
    public class User
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string CountryCode { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CreatedDateTimeUtc { get; set; }
    }
}