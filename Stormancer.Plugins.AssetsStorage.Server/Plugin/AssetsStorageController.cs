using Server.Plugins.API;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Diagnostics;

namespace Stormancer.Server.AssetsStorage
{
    class AssetsStorageController : ControllerBase
    {
        private readonly IAssetsStorageService _assetsStorage;
        private readonly ILogger _logger;
        public AssetsStorageController(AssetsStorageService.Accessor assetsStorage, ILogger logger)
        {
            _assetsStorage = assetsStorage.Service;
            _logger = logger;            
        }
     
        public async Task GetFile(RequestContext<IScenePeerClient> ctx)
        {           
            var branchName = ctx.ReadObject<string>();
            var path = ctx.ReadObject<string>();
            MetafileDto metafileDto = null;
            try
            {
                metafileDto = await _assetsStorage.GetFile(branchName, path);
            }
            catch(BranchException ex)
            {
                _logger.Log(LogLevel.Warn, "AssetsStorageController", $"Branch not found: { branchName } ", ex.Message);
                throw new ClientException($"Branch Not Found:{ branchName }");
            }
            catch (FileException ex)
            {
                _logger.Log(LogLevel.Warn, "AssetsStorageController", $"File not found:  { path } ", ex.Message);
                throw new ClientException($"File not found :{ path }");
            }
        
            ctx.SendValue(metafileDto);
        }        
    }
}
