using System;
using System.Threading.Tasks;
using Events;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using Protectorate.Grain;

namespace Protectorate.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly IClusterClient _client;

        public ValuesController(IClusterClient client)
        {
            _client = client;
        }

        [HttpGet]
        public async Task PutSomeMessages()
        {
            var streamProvider = _client.GetStreamProvider("stream-provider");

            var protectStream = streamProvider.GetStream<ProtectRequest>(Guid.Empty, "protection-requests");

            await protectStream.OnNextAsync(new ProtectRequest(source: new Uri("cdp://demo/1"), target: new Uri("cdp://demo/1/2")));

        }
    }
}
