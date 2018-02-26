using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Stormancer.Diagnostics;
using Server.Database;
using Server.Plugins.Configuration;
using Server.Plugins.AssetsStorage.Exceptions;

namespace Stormancer.Server.AssetsStorage
{
    class ESAssetsStorageRepository : IAssetsStorageRepository
    {
        private string _logCategory = "AssetStorageCacheDataBase";
        private const string _databaseName = "assetsstorage";
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private IESClientFactory _elasticClient;
        private ConcurrentDictionary<string, Task<Branch>> _branches = new ConcurrentDictionary<string, Task<Branch>>();

        public ESAssetsStorageRepository(
            ILogger logger,
            IESClientFactory clientFactory,
            IConfiguration configuration)
        {
            _elasticClient = clientFactory;

            _configuration = configuration;
            _logger = logger;

        }

        #region BddUtilities

        /// <summary>
        /// Tools function where i create/get current elastic search client 
        /// </summary>
        /// <param name="index">Index name</param>
        /// <returns></returns>
        private async Task<Nest.IElasticClient> CreateClient<T>(string assetType = "")
        {
            var result = await _elasticClient.CreateClient<T>(_databaseName, assetType);
            return result;
        }

        /// <summary>
        /// Try to find branch in elastic search data base.
        /// </summary>        
        /// <param BranchName="branch">Branch name to find</param>        
        /// <returns>Return selected branch</returns>
        private async Task<Branch> SearchBranch(String branch)
        {
            var esClient = await this.CreateClient<Branch>();

            var result = await esClient.GetAsync<Branch>(branch);

            if (!result.IsValid)
            {
                if (!result.Found)
                {
                    return null;
                }

                if (result.OriginalException != null)
                {
                    throw new ElasticsearchException($"An error occurred when trying to get branch {branch}", result.OriginalException);
                }
                else
                {
                    throw new ElasticsearchException($"The Elasticsearch server answered with an exception when trying to get branch {branch}. Original message: {result.ServerError.Error.Reason}");
                }
            }


            return result.Source;
        }

        /// <summary>
        /// Search recursively the parent branch parent from leaf to root.
        /// </summary>        
        /// <param ParentBranch="parent">Curent node parent branch</param>
        /// <param BranchHierarchy="path">Current branch hierarchy</param>
        /// <returns>Return curent computed branch</returns>
        private async Task<String> SearchBranchHierarchy(string parent, string path)
        {
            string completePath = parent + "/" + path;
            if (parent != "")
            {
                var branch = await this.GetBranch(parent);
                return await SearchBranchHierarchy(branch.ParentBranchId, completePath);
            }
            return await Task.FromResult<string>(completePath);
        }

        /// <summary>
        ///  Add or update branch in database.This function also update cache
        /// </summary>        
        /// <param BranchObject="branch">Name of branch</param>
        /// <returns>Return branch newly added</returns>
        private async Task<Branch> AddOrUpdateBranch(Branch branch)
        {
            var esClient = await this.CreateClient<Branch>();
            var branchCreation = await esClient.IndexAsync<Branch>(branch, _ => _);

            if (!branchCreation.IsValid)
            {
                _logger.Log(LogLevel.Error, _logCategory, "An error occurred when trying to index file.", new { Error = branchCreation.OriginalException });
                throw new ElasticsearchException($"An error occured during creation process in Elasticsearh. Original message: {branchCreation.ServerError.Error.Reason}");
            }
            else
            {
                return await this.GetBranch(branch.Id);
            }
        }


        /// <summary>
        ///  Add metafile in BDD
        /// </summary>        
        /// <param MetafileObject="metafile">Name of branch</param>
        /// <returns>Return branch newly added</returns>
        private async Task AddMetafile(Metafile metafile)
        {
            var esClient = await this.CreateClient<Metafile>();
            var metafileCreation = await esClient.IndexAsync<Metafile>(metafile, _ => _);

            if (!metafileCreation.IsValid)
            {
                _logger.Log(LogLevel.Error, _logCategory, "An error occurred when trying to index file.", new { Error = metafileCreation.OriginalException });
                throw new ElasticsearchException($"An error occured during creation process in Elasticsearh. Original message: {metafileCreation.ServerError.Error.Reason}");
            }
        }


        /// <summary>
        ///  Retrieve recursively file in database
        /// </summary>        
        /// <param BranchName="branchName">Name of branch</param>
        /// <param Path="path">Path in branch.</param>
        /// <returns>Return metafile if is found in database</returns>
        private async Task<Metafile> SearchFile(string branchname, string filePath, bool isActiveFile = true)
        {
            var selectBranch = await this.GetBranch(branchname);
            if (selectBranch == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"BranchNotFound : {branchname} ", new { Environment.StackTrace });
                throw new BranchException($"BranchNotFound : {branchname}");
            }

            // Todo asset chercher dans le head directement à la place du fichier lui même            
            // Faire un recherche BranchName_path_head
            var file = selectBranch.MetafilesHead.FirstOrDefault(mf => (mf.Id == branchname + "_" + filePath + "_HEAD") && (mf.IsActive == isActiveFile));
            if (file != null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"Branch file found in branch {selectBranch.Id}", "");
                return file.Record;
            }
            else
            {
                if (selectBranch.ParentBranchId != "")
                {
                    var nextBranch = await this.GetBranch(selectBranch.ParentBranchId);
                    return await SearchFile(nextBranch.Id, filePath);
                }
                else
                {
                    return null;
                }
            }
        }
        #endregion

        #region BranchAction       
        /// <summary>
        /// Retrieve all branch in elasticsearch. Function don't use cache. 
        /// </summary>        
        /// <returns></returns>
        public async Task<List<Branch>> GetBranches()
        {
            var esClient = await this.CreateClient<Branch>();
            var result = await esClient.SearchAsync<Branch>(sr => sr.Scroll("1s").Size(30));

            if (!result.IsValid)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"An error occurred when trying to get branches", new { result.DebugInformation });
                return null;
            }

            // Get first scroll page
            var list = result.Documents.ToList();
            while (list.Count < result.Total)
            {
                // Get next scroll page
                result = await esClient.ScrollAsync<Branch>("1s", result.ScrollId);
                if (!result.IsValid)
                {
                    _logger.Log(LogLevel.Warn, _logCategory, $"An error occurred when trying to get branches", new { result.DebugInformation });
                    return null;
                }
                list.AddRange(result.Documents);
            }

            //clear scroll because the process can be run until timeout.
            var _ = esClient.ClearScrollAsync(d => d.ScrollId(result.ScrollId)).ContinueWith(t => { });

            if (list.Count == 0)
            {
                return null;
            }
            else
            {
                return list.ToList();
            }
        }

        /// <summary>
        ///  Retrieve branch in database if it found branch add in cache otherwise return null
        /// </summary>        
        /// <param BranchName="branchName">Name of branch</param>
        /// <returns>Return selected branch</returns>
        public async Task<Branch> GetBranch(string branchName)
        {
            Branch branchResult = await _branches.GetOrAdd(branchName, SearchBranch);

            if (branchResult == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"Selected branch not found in elastic search. branch={branchName}", new { Environment.StackTrace });
                Task<Branch> removedTask;
                _branches.TryRemove(branchName, out removedTask);
            }

            return branchResult;
        }

        /// <summary>
        ///  Create branch in database and update cache.
        /// </summary>        
        /// <param BranchObject="branchName">Object represent a branch</param>
        /// <returns>Return branch newly added.</returns>
        public async Task<Branch> CreateBranch(Branch branch)
        {
            branch.Path = await SearchBranchHierarchy(branch.ParentBranchId, branch.Id);
            return await AddOrUpdateBranch(branch);
        }

        /// <summary>
        ///  Remove branch from database. Delete conditon are:
        ///  1. A branch must  be empty
        ///  2. Don't referenced by other branch
        /// </summary>        
        /// <param BranchName="branchName">Name of branch</param>
        /// <returns>Return branch newly added.</returns>
        public async Task DeleteBranch(string branchName)
        {
            var esClient = await this.CreateClient<Branch>();
            var branch = await this.SearchBranch(branchName);

            var result = await esClient.SearchAsync<Branch>(s => s
                .From(0)
                .Size(2000)
                .IgnoreUnavailable()
                .Query(q => q
                    .Bool(bq => bq.
                        Must(mq => mq.
                            Term("parentBranchId.keyword", branch.Id)
                        )
                    )
                )
            );

            if (result.Documents.Count > 0)
            {
                string childBranches = "";
                result.Documents.ToList().ForEach(x => { childBranches += " " + x.Id; });
                throw new BranchException($"Can no delete selected branch brecause is parent of {childBranches}");
            }

            // Todo assets Check if all element in branch is inactive
            if (branch.MetafilesHead.Where<MetafileHead>(mfh => mfh.IsActive == true).ToList().Count != 0)
            {
                throw new BranchException($"Can not delete branch which there isn't empty");
            }
            
            var deleteResult = await esClient.DeleteAsync<Branch>(branch);
            if (!deleteResult.IsValid)
            {
                _logger.Log(LogLevel.Warn, _logCategory, "Delete branch ", deleteResult.DebugInformation);
                throw new ElasticsearchException($"An error occured during deletion process in Elasticsearh. Original message: {deleteResult.ServerError.Error.Reason}");
            }

            Task<Branch> removedTask;
            _branches.TryRemove(branchName, out removedTask);
        }
        #endregion

        #region FileAction
        /// <summary>
        ///  Create file file and add it in branch
        /// </summary>        
        /// <param Metafile="metafile">Object represent a file</param>
        public async Task CreateFile(Metafile metafile)
        {
            // Search branch
            var branchToUpdate = await this.GetBranch(metafile.BranchId);
            if (branchToUpdate == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"BranchNotFound : {metafile.BranchId} ", new { Environment.StackTrace });
                throw new BranchException($"BranchNotFound: {metafile.BranchId}");
            }

            // Todo asset je check avec l'id si il existe un recorde head.
            // Si oui alors je change le guid du head avec le nouveau
            // Si non alors je créer le nouveau head.
            // Check if the file allready exist
            // Update head 

            var headFile = branchToUpdate.MetafilesHead.FirstOrDefault<MetafileHead>(mf => mf.Id == metafile.BranchId + "_" + metafile.Path + "_HEAD");
            if (headFile != null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"File {metafile.Path} already exist on server ", new { });
                headFile.Record = metafile;
                headFile.HeadFile = metafile.Id;
                headFile.IsActive = true;            
            }
            else
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"File {metafile.Path} add on server ", new { });
                //Add new head in branch and index file index flatten.
                var metaFileHead = new MetafileHead();
                metaFileHead.Id = metafile.BranchId + "_" + metafile.Path + "_HEAD";
                metaFileHead.HeadFile = metafile.Id;
                metaFileHead.IsActive = true;
                metaFileHead.Record = metafile;
                branchToUpdate.MetafilesHead.Add(metaFileHead);
            }            

            // Remove branch in cache
            Task<Branch> removedTask;
            _branches.TryRemove(metafile.BranchId, out removedTask);

            //Update branch
            await this.AddOrUpdateBranch(branchToUpdate);

            //Update file
            await this.AddMetafile(metafile);
        }

        /// <summary>
        ///  Remove branch.
        /// </summary>        
        /// <param BranchName="branchName">Name of branch</param>
        /// <param Path="path">Path in branch.</param>
        /// <returns>Return true if the file delete in database</returns>
        public async Task<bool> DeleteFile(string branchName, string path)
        {
            var branch = await this.GetBranch(branchName);

            if (branch == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"BranchNotFound : {branchName} ", new { Environment.StackTrace });
                throw new BranchException($"BranchNotFound : {branchName}");
            }

            // Todo asset check si le head existe 
            MetafileHead fileToDelete = branch.MetafilesHead.FirstOrDefault(mf => mf.Id == branchName + "_" + path + "_HEAD");
            if (fileToDelete == null)
            {
                _logger.Log(LogLevel.Error, _logCategory, $"Specified file not found on Elasticsearch", new { path });
                throw new FileException($"Specified file not found on Elasticsearch. Original file : { path }");
            }

            // Remove branch in cache
            Task<Branch> removedTask;
            _branches.TryRemove(branchName, out removedTask);

            // Todo asset mettre la branche en désactiver à la place de la supprimer
            // Try to update branch without file
            foreach (var mfh in branch.MetafilesHead)
            {
                if (mfh.Id == fileToDelete.Id)
                {
                    mfh.IsActive = false;
                }
            }

            var updatedBranch = await this.AddOrUpdateBranch(branch);
            if (updatedBranch == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///  Retrieve recursively file
        /// </summary>        
        /// <param BranchName="branchName">Name of branch</param>
        /// <param Path="path">Path in branch.</param>
        /// <returns>Return metafile if is found in database</returns>
        public async Task<Metafile> GetFile(string branchname, string path)
        {
            var file = await this.SearchFile(branchname, path);
            if (file == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"File {path} doesn't exist on server ", new { Environment.StackTrace });
                throw new FileException($"File {path} doesn't exist on server");
            }
            return file;
        }
        #endregion
    }
}
