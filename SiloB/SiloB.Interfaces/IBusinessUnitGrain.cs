using System;
using System.Threading.Tasks;

using Orleans;

namespace SiloB.Interfaces
{
#nullable disable
    public class BusinessUnitModel
    {
        public string Id { get; set; }
        
        public string WorkspaceId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
    }
#nullable restore

    public interface IBusinessUnitGrain : IGrainWithStringKey
    {
        Task<BusinessUnitModel> Create(string workspaceId);

        ValueTask<bool> IsInitialized();

        ValueTask<BusinessUnitModel> GetBusinessUnit();

        Task Delete();
    }
}