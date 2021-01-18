using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Events;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.CompilerServices;
using Orleans;
using Orleans.Runtime;
using ProtectorServices;
using SiloA.Interfaces;

namespace SiloA.Grains
{
    public class WorkspaceState
    {
#nullable disable
        public string Id { get; set; }
        public HashSet<string> BusinessUnits { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
#nullable restore
        public static implicit operator WorkspaceModel(WorkspaceState state)
            => new WorkspaceModel
        {
            Id = state.Id,
            BusinessUnits = state.BusinessUnits,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt
        };
    }

    public class WorkspaceGrain : Grain, IWorkspaceGrain
    {
        private readonly IPersistentState<WorkspaceState> _persistedState;
        private readonly Protector.ProtectorClient _protectorClient;
        private readonly ILogger<WorkspaceGrain> _logger;

        public WorkspaceGrain(
            [PersistentState("workspaces", "workspaces")] IPersistentState<WorkspaceState> persistedState,
            Protector.ProtectorClient protectorClient,
            ILogger<WorkspaceGrain> logger)
        {
            _persistedState = persistedState;
            _protectorClient = protectorClient;
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
            _persistedState.State = new WorkspaceState
            {
                Id = this.GetPrimaryKeyString(),
                CreatedAt = time,
                UpdatedAt = time,
                BusinessUnits = new HashSet<string>()
            };
        }

        public ValueTask<bool> IsInitialized() => ValueTask.FromResult(Initialized());

        public ValueTask<WorkspaceModel> GetWorkspace()
            => ValueTask.FromResult((WorkspaceModel) _persistedState.State);

        private bool Initialized()
            => !string.IsNullOrEmpty(_persistedState.Etag);

        public async Task<WorkspaceModel> AddBusinessUnits(IEnumerable<string> businessUnits)
        {
            if (businessUnits == null)
                throw new ArgumentNullException(nameof(businessUnits), "should probably provide a non-null non-empty array");

            if (!businessUnits.Any()) return _persistedState.State;

            var alreadyContained = AddBusinessUnitsToState(businessUnits);

            foreach (var businessUnit in alreadyContained)
            {
                _logger.LogWarning($"business unit {businessUnit} already part of workspace {_persistedState.State.Id}");
            }

            await WriteStateAsync();

            return _persistedState.State;
        }

        public async Task<WorkspaceDeleteResult> DeleteWorkspace()
        {
            var r = _protectorClient.IsChangeAllowedAsync(new CheckChange
            {
                Target = UriCreator.Create(("workspace", this.GetPrimaryKeyString())).ToString()
            });

            if (!r.Permitted)
            {
                return new WorkspaceDeleteResult
                {
                    Deleted = false,
                    Reasons = r.Antecedents.Select(x => new WorkspaceDeleteResult.Reason
                    {
                        Source = x.Source,
                        Comments = x.Comments.ToList()
                    }).ToList()
                };
            }

            await _persistedState.ClearStateAsync();
            DeactivateOnIdle();
            return new WorkspaceDeleteResult { Deleted = true, Reasons = new List<WorkspaceDeleteResult.Reason>() };
        }

        private IEnumerable<string> AddBusinessUnitsToState(IEnumerable<string> businessUnits)
            => from businessUnit in businessUnits 
                let added = _persistedState.State.BusinessUnits.Add(businessUnit) 
                where !added
                select businessUnit;

        public async Task<WorkspaceModel> Create()
        {
            if (Initialized())
                throw new AccessViolationException("Can't create an already initialized workspace");

            await _persistedState.WriteStateAsync();

            return _persistedState.State;
        }

        private Task WriteStateAsync()
        {
            _persistedState.State.UpdatedAt = DateTime.UtcNow;

            return _persistedState.WriteStateAsync();
        }
    }
}
