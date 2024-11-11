using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace TraceRouting.Location
{
    public class LocationResolver : ILocationResolver
    {
        private readonly HttpClient client;

        public LocationResolver()
        {
            client = new HttpClient { BaseAddress = new Uri("https://iplocation.com") };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<LocationInformation> ResolveLocationAsync(IPAddress ip)
        {
            if (ip.IsPrivate()) return null;
            return await ResolveLocationAsync(ip.ToString());
        }

        protected async Task<LocationInformation> ResolveLocationAsync(string ip)
        {
            var requestContent = new StringContent($"ip={ip}", Encoding.UTF8, 
                "application/x-www-form-urlencoded");
            var response = await client.PostAsync("/", requestContent);

            // Ensure we get a good response before parsing
            if (response.StatusCode != HttpStatusCode.OK) return null;
            
            var obj = JObject.Parse(await response.Content.ReadAsStringAsync());

            var location = new LocationInformation()
            {
                City = obj["city"].Value<string>(),
                Organisation = obj["company"].Value<string>(),
                Country = obj["country_name"].Value<string>(),
                Region = obj["region_name"].Value<string>(),
                Longitude = obj["lng"].Value<double>(),
                Latitude = obj["lat"].Value<double>()
            };

            return location;
        }
    }
}
