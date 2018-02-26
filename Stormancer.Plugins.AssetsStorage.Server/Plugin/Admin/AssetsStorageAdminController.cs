using Newtonsoft.Json.Linq;
using Server.Plugins.AssetsStorage;
using Server.Plugins.FileStorage;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Stormancer.Server.AssetsStorage
{
    public class AssetsStorageAdminController : ApiController
    {
        //private const string _debugLogCategoryName = "AssetsStorage";
        private readonly IAssetsStorageService _assetsStorage;

        public AssetsStorageAdminController(IAssetsStorageService assetsStorage)
        {
            _assetsStorage = assetsStorage;
        }

        /// <summary>
        /// Return all branch name
        /// </summary>
        /// <returns>List of branches</returns>
        [ActionName("branch")]
        [HttpGet]       
        public async Task<List<BranchDto>> GetBranches()
        {
            var result = await _assetsStorage.GetBranches();

            if (result != null)
            {
                return result;
            }
            else
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"Database maybe empty or there are errors when app try to access database"),
                    ReasonPhrase = $"An error occurred when trying to get branches"
                };
                throw new HttpResponseException(resp);
            }          
        }

        /// <summary>
        /// Return files in specific branch
        /// </summary>
        /// <param name="branchName">Desired branch</param>
        /// <returns>List of file contained in branch</returns>
        [ActionName("branch")]
        [HttpGet]     
        public async Task<List<MetafileDto>> GetBranch(string branchName)
        {
            var result = await _assetsStorage.GetBranch(branchName);

            if (result != null)
            {
                return result;
            }
            else
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"Branch content not found= {branchName}"),
                    ReasonPhrase = "Branch content not found"
                };
                throw new HttpResponseException(resp);
            }
        }

        /// <summary>
        /// Create a new branch
        /// </summary>
        /// <param name="branchName">Branch name to be created</param>
        /// <param name="parentBranch">Set the inheritance parent name</param>
        /// <returns></returns>
        [ActionName("branch")]
        [HttpPut]        
        public async Task CreateBranch(string branchName, string parentBranch = "")
        {
            var branch = new Branch
            {
                Id = branchName,
                ParentBranchId = parentBranch,
                Path = "",
                MetafilesHead = new List<MetafileHead>(),
            };

            try
            {
                await _assetsStorage.CreateBranch(branch);
            }
            catch (Exceptions.BranchException ex)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent(ex.Message),
                    ReasonPhrase = ex.Message
                };
                throw new HttpResponseException(resp);
            }
            catch(Exceptions.ElasticsearchException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Internal data base error please check server logs"),
                    ReasonPhrase = $"Internal data base error please check server logs"
                };
                throw new HttpResponseException(resp);
            }
        }

        /// <summary>
        /// Delete a specific branch if she was empty and no inherited
        /// </summary>
        /// <param name="branchName">Branch to delete</param>
        /// <returns></returns>
        [ActionName("branch")]
        [HttpDelete]
        public async Task RemoveBranch(string branchName)
        {
            try
            {
                await _assetsStorage.DeleteBranch(branchName);
            }
            catch (Exceptions.BranchException ex)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent(ex.Message),
                    ReasonPhrase = ex.Message
                };
                throw new HttpResponseException(resp);
            }
            catch (Exceptions.ElasticsearchException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Internal data base error please check server logs"),
                    ReasonPhrase = $"Internal data base error please check server logs"
                };
                throw new HttpResponseException(resp);
            }
        }

        /// <summary>
        /// Create file in branch
        /// </summary>
        /// <param name="branchName">File location</param>
        /// <param name="path">path in branch</param>
        /// <param name="data">Data of file</param>
        /// <returns></returns>
        [ActionName("file")]
        [HttpPut]
        public async Task CreateFile(string branchName, string path, HttpRequestMessage data)
        {
            var fileName = Path.GetFileName(path);
            
            //Create meta to store it in elasticsearch
            var id = Guid.NewGuid().ToString("N");
            var file = new Metafile
            {
                Id = id,
                Filename = fileName,                                
                Path = path  == "" ? "" : path,
                BranchId = branchName,
                MD5Hash = ""
            };

            await _assetsStorage.UploadFile(branchName, path, file, data);

            // Check if possible to create file before upload
            try
            {
                await _assetsStorage.CreateFile(file);
            }
            catch (Exceptions.BranchException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent($"{branchName} already exist !"),
                    ReasonPhrase = $"{branchName} already exist !"
                };
                throw new HttpResponseException(resp);
            }
            catch (Exceptions.FileException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent($"File : {path} already exist !"),
                    ReasonPhrase = $"File : {path} already exist !"
                };
                throw new HttpResponseException(resp);
            }
            catch (Exceptions.ElasticsearchException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Internal data base error please check server logs"),
                    ReasonPhrase = $"Internal data base error please check server logs"
                };
                throw new HttpResponseException(resp);
            }
        }

        /// <summary>
        /// Delete file matching selected branch and path.
        /// </summary>
        /// <param name="branchName">Branch where the file is located</param>
        /// <param name="path">Path where the file is stored</param>
        /// <returns></returns>
        [ActionName("file")]
        [HttpDelete]
        public async Task RemoveFile(string branchName, string path)
        {
            try
            {
                await _assetsStorage.DeleteFile(branchName, path);
            }
            catch (Exceptions.BranchException ex)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent(ex.Message),
                    ReasonPhrase = $"{branchName} not found !"
                };
                throw new HttpResponseException(resp);
            }
            catch (Exceptions.FileException ex)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent(ex.Message),
                    ReasonPhrase = ex.Message
                };
                throw new HttpResponseException(resp);
            }
            catch (Exceptions.ElasticsearchException)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Internal data base error please check server logs"),
                    ReasonPhrase = $"Internal data base error please check server logs"
                };
                throw new HttpResponseException(resp);
            }
        }

    }
}
