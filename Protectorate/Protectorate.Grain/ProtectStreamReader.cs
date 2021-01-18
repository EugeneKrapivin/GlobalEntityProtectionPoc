using System;
using System.Linq;
using System.Threading.Tasks;
using Events;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Streams;
using Protectorate.Interfaces;

namespace Protectorate.Grain
{
    [StatelessWorker(1)]
    [ImplicitStreamSubscription("protection-requests")]
    [ImplicitStreamSubscription("unprotection-requests")]
    public class ProtectStreamReader : 
        Orleans.Grain, 
        IProtectStreamReader, 
        IAsyncObserver<ProtectRequest>,
        IAsyncObserver<UnprotectRequest>
    {
        private readonly ILogger<ProtectStreamReader> _logger;

        public async Task OnNextAsync(ProtectRequest item, StreamSequenceToken? token = null)
        {
            _logger.LogInformation($"Got message {item}");

            var grain = GrainFactory.GetGrain<IResourceProtector>(item.Target.ToString());

            await grain.ProtectResource(new ProtectedModel
            {
                Uri = item.Source,
                Comment = item.Comments.FirstOrDefault() ?? "keep the 5th"
            });
        }

        public async Task OnNextAsync(UnprotectRequest item, StreamSequenceToken? token = null)
        {
            _logger.LogInformation($"Got message {item}");
            var grainId = item.Target.ToString().Replace('/', '_');
            var grain = GrainFactory.GetGrain<IResourceProtector>(grainId);

            await grain.UnprotectResource(new ProtectedModel
            {
                Uri = item.Source
            });
        }

        Task IAsyncObserver<ProtectRequest>.OnCompletedAsync()
        {
            return Task.CompletedTask;
        }
        
        Task IAsyncObserver<ProtectRequest>.OnErrorAsync(Exception ex)
        {
            return Task.CompletedTask;
        }
        
        Task IAsyncObserver<UnprotectRequest>.OnCompletedAsync()
        {
            return Task.CompletedTask;
        }

        Task IAsyncObserver<UnprotectRequest>.OnErrorAsync(Exception ex)
        {
            return Task.CompletedTask;
        }

        public ProtectStreamReader(ILogger<ProtectStreamReader> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync()
        {
            var streamProvider = GetStreamProvider("stream-provider");
            
            var protectStream = streamProvider.GetStream<ProtectRequest>(this.GetPrimaryKey(), "protection-requests");
            await protectStream.SubscribeAsync(OnNextAsync);

            var unprotectStream = streamProvider.GetStream<UnprotectRequest>(this.GetPrimaryKey(), "unprotection-requests");
            await unprotectStream.SubscribeAsync(OnNextAsync);
        }
    }
}
