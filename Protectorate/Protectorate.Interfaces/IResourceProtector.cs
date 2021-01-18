using System;
using System.Threading.Tasks;
using Orleans;

namespace Protectorate.Interfaces
{
    #nullable disable
    public class ProtectedModel
    {
        public Uri Uri { get; set; }
        
        public string Comment { get; set; }
    }
    #nullable restore
    
    public interface IResourceProtector : IGrainWithStringKey
    {
        ValueTask<ProtectedModel[]> GetResourceProtections();
        
        Task ProtectResource(ProtectedModel request);

        ValueTask<bool> IsInitialized();

        Task<bool> UnprotectResource(ProtectedModel request);

        Task<int> DeleteResource();
    }
}
