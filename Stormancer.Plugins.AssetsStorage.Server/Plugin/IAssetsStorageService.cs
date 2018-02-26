using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    public interface IAssetsStorageService
    {
        Task CreateBranch(Branch branch);
        Task<List<MetafileDto>> GetBranch(string branch);
        Task<List<BranchDto>> GetBranches();
        Task DeleteBranch(string branchName);

        Task CreateFile(Metafile metaFile);
        Task UploadFile(string branchName, string fileName, Metafile file, HttpRequestMessage data);
        Task<MetafileDto> GetFile(string branchname, string path);
        Task DeleteFile(string branchName, string path);
    }
}
