using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    interface IAssetsStorageRepository
    {
        Task<List<Branch>> GetBranches();
        Task<Branch> GetBranch(string branchName);
        Task<Branch> CreateBranch(Branch branch);
        Task DeleteBranch(string branchName);
        Task CreateFile(Metafile metafile);
        Task<bool> DeleteFile(string branchName, string path);
        Task<Metafile> GetFile(string branchname, string path);
    }
}
