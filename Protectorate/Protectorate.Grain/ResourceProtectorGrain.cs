using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Protectorate.Interfaces;

namespace Protectorate.Grain
{

#nullable disable

    public class ProtectionNote
    {
        public Uri Source { get; set; }

        public string Comment { get; set; }
       
        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }
    }

#nullable restore

    public class ResourceProtectorState
    {
        public List<ProtectionNote> ProtectionNotes { get; set; } = new List<ProtectionNote>();
        
        public DateTime? SoftDeleteTimestamp { get; set; }

        public DateTime Created { get; set; }
        
        public DateTime Updated { get; set; }
    }

    public class ResourceProtectorGrain : Orleans.Grain, IResourceProtector
    {
        private readonly IPersistentState<ResourceProtectorState> _persistedState;
        private readonly ILogger<ResourceProtectorGrain> _logger;

        private ResourceProtectorState State => _persistedState.State;

        public ResourceProtectorGrain(
            [PersistentState("protected-resources", "protected-resources")] IPersistentState<ResourceProtectorState> persistedState, 
            ILogger<ResourceProtectorGrain> logger)
        {
            _persistedState = persistedState;
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            // ensure we always work with an initialized state to avoid NREs like n00bs
            if (_persistedState.State?.ProtectionNotes == null)
            {
                _logger.LogInformation("state doesn't exist, creating");

                var time = DateTime.UtcNow;

                _persistedState.State = new ResourceProtectorState
                {
                    ProtectionNotes = new List<ProtectionNote>(),
                    Created = time,
                    Updated = time
                };
            }
         
            return base.OnActivateAsync();
        }

        public ValueTask<ProtectedModel[]> GetResourceProtections() =>
            ValueTask.FromResult(_persistedState.State.ProtectionNotes.Select(x => new ProtectedModel
            {
                Uri = x.Source,
                Comment = x.Comment,
            }).ToArray());

        public async Task ProtectResource(ProtectedModel request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var slips = _persistedState.State.ProtectionNotes;

            var previousSlip = slips.SingleOrDefault(x => x.Source == request.Uri);

            var time = DateTime.UtcNow;
            if (previousSlip == null)
            {
                _persistedState.State.ProtectionNotes.Add(new ProtectionNote
                {
                    Source = request.Uri,
                    Comment = request.Comment,
                    Created = time,
                    Updated = time
                });

                _logger.LogInformation("adding another guardian slip", this.GetPrimaryKeyString());
            }
            else
            {
                _logger.LogInformation("updating existing protection request");
                previousSlip.Updated = time;
            }

            await WriteStateAsync();
        }

        public ValueTask<bool> IsInitialized()
        {
            return ValueTask.FromResult(!EtagExists());
        }

        private bool EtagExists()
            => string.IsNullOrEmpty(_persistedState.Etag);

        public async Task<bool> UnprotectResource(ProtectedModel request)
        {
            var slips = _persistedState.State.ProtectionNotes;

            var previousSlip = slips.SingleOrDefault(x => x.Source == request.Uri);

            if (previousSlip == null)
            {
                _logger.LogWarning("delete guardian slip operation attempted on a non existing slip", this.GetPrimaryKeyString());
                return false;
            }

            slips.Remove(previousSlip);

            if (!slips.Any())
            {
                _logger.LogInformation("Resource {0} is left unguarded and is now eligible for deletion", this.GetPrimaryKeyString());
            }

            await WriteStateAsync();
            
            return true;
        }

        public async Task<int> DeleteResource()
        {
            if (!EtagExists())
            {
                _logger.LogWarning("attempted resource deletion for a non existing resource");
                return -1;
            }

            if (_persistedState.State.SoftDeleteTimestamp == null)
            {
                _logger.LogWarning("resource marked for deletion");
                _persistedState.State.SoftDeleteTimestamp = DateTime.UtcNow;

                return 0;
            }

            if (_persistedState.State.SoftDeleteTimestamp != null)
            {
                _logger.LogWarning("resource deletion will be deleted");
                foreach (var slip in State.ProtectionNotes)
                {
                    _logger.LogWarning("resource force deleting slip {0}:{1}", slip.Source);
                }
            }

            var slips = State.ProtectionNotes.Count;

            await _persistedState.ClearStateAsync();
            DeactivateOnIdle();

            return slips;
        }

        private Task WriteStateAsync()
        {
            _persistedState.State.Updated = DateTime.UtcNow;

            return _persistedState.WriteStateAsync();
        }
    }
}
