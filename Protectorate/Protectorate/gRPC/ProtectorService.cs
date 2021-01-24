using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Orleans;
using Protectorate.Grain;
using Protectorate.Interfaces;
using ProtectorServices;

namespace Protectorate.gRPC
{
    public class ProtectorService : ProtectorServices.Protector.ProtectorBase
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<ProtectorService> _logger;

        public ProtectorService(IClusterClient clusterClient, ILogger<ProtectorService> logger)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public override async Task<CheckChangeResponse> IsChangeAllowedAsync(CheckChange request, ServerCallContext context)
        {
            _logger.LogInformation("received and RPC request");

            var resource = _clusterClient.GetGrain<IResourceProtector>(request.Target);

            if (await resource.IsInitialized() == false)
            {
                return new CheckChangeResponse
                {
                    Target = request.Target,
                    Permitted = true
                };
            }

            var antecedents = await resource.GetResourceProtections();
            var response = new CheckChangeResponse
            {
                Target = request.Target,
                Permitted = true
            };

            foreach (var protector in antecedents)
            {
                var notice = new DependencyNotice
                {
                    Source = protector.Uri.ToString()
                };

                notice.Comments.Add(protector.Comment);

                response.Antecedents.Add(notice);
                
                response.Permitted = false;
            }

            return response;
        }
    }
}
