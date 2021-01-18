using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace SiloA.Interfaces
{
#nullable disable
    public class WorkspaceModel
    {
        public string Id { get; set; }
        public HashSet<string> BusinessUnits { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    public class WorkspaceDeleteResult
    {
        public string EntityUri { get; set; }
        public bool Deleted { get; set; }
        public List<Reason> Reasons { get; set; }
        public class Reason
        {
            public string Source { get; set; }
            public List<string> Comments { get; set; }
        }
    }
#nullable restore

    public interface IWorkspaceGrain : IGrainWithStringKey
    {
        Task<WorkspaceModel> Create();

        ValueTask<bool> IsInitialized();
        
        ValueTask<WorkspaceModel> GetWorkspace();
        
        Task<WorkspaceModel> AddBusinessUnits(IEnumerable<string> businessUnits);
        Task<WorkspaceDeleteResult> DeleteWorkspace();
    }
}
