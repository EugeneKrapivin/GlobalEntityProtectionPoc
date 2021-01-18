using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Microsoft.Extensions.Logging;

using Orleans;
using Orleans.Runtime;

using SiloB.Interfaces;

namespace SiloB.Grains
{
    public class BusinessUnitState
    {
#nullable disable
        public string Id { get; set; }
        
        public string WorkspaceId { get; set; }
        
        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
#nullable restore
        public static implicit operator BusinessUnitModel(BusinessUnitState state)
            => new BusinessUnitModel
            {
                Id = state.Id,
                WorkspaceId = state.WorkspaceId,
                CreatedAt = state.CreatedAt,
                UpdatedAt = state.UpdatedAt
            };
    }

    public class BusinessUnitGrain : Grain, IBusinessUnitGrain
    {
        private readonly IPersistentState<BusinessUnitState> _persistedState;
        private readonly ILogger<BusinessUnitGrain> _logger;

        public BusinessUnitGrain(
            [PersistentState("business-units", "business-units")] IPersistentState<BusinessUnitState> persistedState,
            ILogger<BusinessUnitGrain> logger)
        {
            _persistedState = persistedState;
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            if (!Initialized())
            {
                InitializeState();
            }

            return Task.CompletedTask;
        }

        private void InitializeState()
        {
            var time = DateTime.UtcNow;
            _persistedState.State = new BusinessUnitState
            {
                Id = this.GetPrimaryKeyString(),
                CreatedAt = time,
                UpdatedAt = time
            };
        }

        public ValueTask<bool> IsInitialized() => ValueTask.FromResult(Initialized());

        public ValueTask<BusinessUnitModel> GetBusinessUnit()
            => ValueTask.FromResult((BusinessUnitModel)_persistedState.State);

        private bool Initialized()
            => !string.IsNullOrEmpty(_persistedState.Etag);

        public async Task<BusinessUnitModel> Create(string workspaceId)
        {
            if (Initialized())
                throw new AccessViolationException("Can't create an already initialized workspace");
            
            _persistedState.State.WorkspaceId = workspaceId;

            await _persistedState.WriteStateAsync();
            
            var stream = GetStreamProvider("stream-provider")
                .GetStream<ProtectRequest>(Guid.Empty, "protection-requests");

            await stream.OnNextAsync(new ProtectRequest(
                source: UriCreator.Create(("workspace", _persistedState.State.WorkspaceId), ("businessunit", this.GetPrimaryKeyString())),
                target: UriCreator.Create(("workspace", _persistedState.State.WorkspaceId))
            ));

            return _persistedState.State;
        }

        public async Task Delete()
        {
            var stream = GetStreamProvider("stream-provider")
                .GetStream<UnprotectRequest>(Guid.Empty, "unprotection-requests");

            await stream.OnNextAsync(new UnprotectRequest(
                source: UriCreator.Create(("workspace", _persistedState.State.WorkspaceId), ("businessunit", this.GetPrimaryKeyString())),
                target: UriCreator.Create(("workspace", _persistedState.State.WorkspaceId))
            ));

            await _persistedState.ClearStateAsync();
            DeactivateOnIdle();
        }
    }
}

