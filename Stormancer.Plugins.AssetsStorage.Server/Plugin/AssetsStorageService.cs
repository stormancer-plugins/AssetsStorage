using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography;
using Stormancer.Diagnostics;
using Server.Database;
using Server.Plugins.Configuration;
using Server.Plugins.FileStorage;
using Server.Plugins.AssetsStorage.Exceptions;

namespace Stormancer.Server.AssetsStorage
{
    
    class AssetsStorageService : IAssetsStorageService
    {
        //Accessor
        public class Accessor
        {
            public Accessor(IAssetsStorageService service, IEnumerable<IAssetsStorageEventHandler> handlers)
            {
                ((AssetsStorageService)service).EventHandlers = handlers;
                Service = service;
            }
            public IAssetsStorageService Service { get; }
        }
                
        public IEnumerable<IAssetsStorageEventHandler> EventHandlers
        {
            set
            {
                _eventHandlers = value;
            }
        }

        //Class fields
        private readonly string _logCategory = "AssetsStorageService";
        private readonly ILogger _logger;
        private readonly IFileStorage _fileStorage;
        private readonly IAssetsStorageRepository _dataBaseWrapper;
        private readonly IConfiguration _conf;
        private IEnumerable<IAssetsStorageEventHandler> _eventHandlers;
        private bool _keepHistory; 
  
        public AssetsStorageService(
            ILogger logger,
            IESClientFactory clientFactory,
            IConfiguration configuration,
            IFileStorage fileStorage,
            IAssetsStorageRepository assetStorageCache,
            IConfiguration config)
        {
            _logger = logger;
            _fileStorage = fileStorage;         
            _dataBaseWrapper = assetStorageCache;
            _conf = config;

            _conf.SettingsChanged += OnSettingsChange;
            OnSettingsChange(_conf, _conf.Settings);
        }

        #region Utilities

        private void OnSettingsChange(object sender, dynamic settings)
        {
            if ((bool?)settings.assetsStorage?.keepHistory == null)
            {
                _keepHistory = false;
                _logger.Log(LogLevel.Info, _logCategory, $"Keep history assets is disable ", new { keepHistory = _keepHistory });
            }
            else
            {
                _keepHistory = settings.assetsStorage.keepHistory;
                _logger.Log(LogLevel.Info, _logCategory, $"Keep history assets is { _keepHistory } ", new { keepHistory = _keepHistory });
            }
        }

        /// <summary>
        /// Compute MD5 with string
        /// </summary>
        /// <param md5Object="md5Hash"> Md5 instance</param>
        /// <param stringToCheck="input"> string must be MD5 computed</param>
        /// <returns>Return true if input and hash are equal</returns>
        private string GetMD5Hash(MD5 md5Hash, string input)
        {
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("X2"));
            }
            return sBuilder.ToString();
        }

        /// <summary>
        /// Compute MD5 with stream
        /// </summary>
        /// <param md5Objec="md5Hash"> Md5 instance</param>
        /// <param streamToCheck="input"> Stream must be MD5 computed</param>
        /// <returns>Return true if input and hash are equal</returns>
        private string GetMD5Hash(MD5 md5Hash, Stream input)
        {
            byte[] data = md5Hash.ComputeHash(input);

            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("X2"));
            }
            return sBuilder.ToString();
        }

        /// <summary>
        /// Verify if the MD5 are equal
        /// </summary>
        /// <param md5Objec="md5Hash"> Md5 instance</param>
        /// <param contentToCheck="input"> String need to be MD5 computed </param>
        /// <param hash="hash"> Md5 string must be compare </param>
        /// <returns>Return true if input and hash are equal</returns>
        private bool VerifyMD5Hash(MD5 md5Hash, string input, string hash)
        {
            string hashOfInput = GetMD5Hash(md5Hash, input);

            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            if (0 == comparer.Compare(hashOfInput, hash))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region BranchAction
        /// <summary>
        /// Retrieve a specific branch
        /// </summary>
        /// <param branchName="branch">Branch name</param>
        /// <returns>Return a task of list files in selected branch</returns>
        public async Task<List<MetafileDto>> GetBranch(string branch)
        {
            var selectedBranch = await _dataBaseWrapper.GetBranch(branch);

            if (selectedBranch.MetafilesHead == null)
            {
                return null;
            }

            // 
            var resultDto = new List<MetafileDto>();
            foreach (MetafileHead mfh in selectedBranch.MetafilesHead)
            {
                if (mfh.IsActive)
                {
                    var fileUrl = await _fileStorage.GetDownloadUrl(mfh.Record.BranchId + "/" + mfh.Record.Id);
                    resultDto.Add(new MetafileDto
                    {
                        FileName = mfh.Record.Filename,
                        URL = fileUrl.AbsoluteUri,
                        Path = mfh.Record.Path,
                        MD5Hash = mfh.Record.MD5Hash,
                    });
                }                
            }
            return resultDto;
        }

        /// <summary>
        /// Retrieve all branch in bdd
        /// </summary>
        /// <returns>Return a task of all branch present in database</returns>     
        public async Task<List<BranchDto>> GetBranches()
        {
            var branches = await _dataBaseWrapper.GetBranches();
            if (branches == null)
            {
                return null;
            }
            else
            {
                List<BranchDto> branchDto = new List<BranchDto>();
                foreach(Branch b in branches)
                {
                    branchDto.Add(new BranchDto { BranchName = b.Id, BranchPath = b.Path });
                }
                return branchDto;
            }
        }

        /// <summary>
        /// Create branch specified in param
        /// </summary>
        /// <param branchObject="branch">Branch objec</param>
        public async Task CreateBranch(Branch branch)
        {
            var branchesResult = await _dataBaseWrapper.GetBranch(branch.Id);

            if (branchesResult != null)
            {
                throw new BranchException($"Can not create branch because branch {branch} already exist");
            }

            await _dataBaseWrapper.CreateBranch(branch);
        }

        /// <summary>
        /// Delete branch. A can be deleted if not referenced by other branch
        /// and if isn't empty.
        /// </summary>
        /// <param branchName="branchName">Branch to be delete</param>
        public async Task DeleteBranch(string branchName)
        {
            await _dataBaseWrapper.DeleteBranch(branchName);
        }

        #endregion

        #region FileAction
        /// <summary>
        /// Upload file in CDN defined in server configuration
        /// </summary>
        /// <param branchName="branch">Branch name where to upload file</param>
        /// <param path="path">Full path where to store file</param>
        /// <param MetafileObject="file">Object describe file and stored in data base</param>
        /// <param ContentRequest="data">Data to store</param>
        public async Task UploadFile(string branchName, string path, Metafile file, HttpRequestMessage data)
        {
            // manipulate data to store it in azure       
            MemoryStream content = new MemoryStream();
            await data.Content.CopyToAsync(content);
            content.Seek(0, SeekOrigin.Begin);

            var type = data.Content.Headers.ContentType.ToString();

            await _fileStorage.UploadFile(branchName + "/" + file.Id, content, type);

            content.Seek(0, SeekOrigin.Begin);

            MemoryStream metafileStream = new MemoryStream();
            StreamWriter writerData = new StreamWriter(metafileStream);
            writerData.Write(file.Id);
            writerData.Write(file.Path);
            writerData.Flush();
            metafileStream.Seek(0, SeekOrigin.Begin);

            List<Stream> streamList = new List<Stream>();
            streamList.Add(metafileStream);
            streamList.Add(content);
            ConcatenatedStream streams = new ConcatenatedStream(streamList);

            MD5 md5Hash = MD5.Create();
            var hash = GetMD5Hash(md5Hash, streams);

            file.MD5Hash = hash;
        }

        /// <summary>
        /// Create file in database
        /// </summary>
        /// <param MetafileObject="file">Object describe file and stored in elasticsearch</param>
        public async Task CreateFile(Metafile metafile)
        {
            await _dataBaseWrapper.CreateFile(metafile);
        }

        /// <summary>
        /// Delete file in data base and in CDN defined in server configuration
        /// </summary>
        /// <param branchName="branchName">Branch where is stored file</param>
        /// <param path="path">CDN file location</param>
        public async Task DeleteFile(string branchName, string path)
        {
            var fileToDelete = await _dataBaseWrapper.GetFile(branchName, path);
            if (!await _dataBaseWrapper.DeleteFile(branchName, path))
            {
                _logger.Log(LogLevel.Error, _logCategory, $"An error when you try to delete file", new { branchName = branchName, path = path });
                throw new FileException($"Specified file not found on Elasticsearch. Original file : path {path}  branch {branchName}");
            }
            
            if(!_keepHistory)
            {                
                await _fileStorage.DeleteFile(branchName + "/" + fileToDelete.Id);
            }
        }

        /// <summary>
        /// Get file from database perform hierarchical search
        /// </summary>
        /// <param branchName="branchName">Branch where is stored file</param>
        /// <param path="path">file location in branch</param>
        /// <returns>A task of all files present in selected branch</returns>    
        public async Task<MetafileDto> GetFile(string branchname, string path)
        {
            var file = await _dataBaseWrapper.GetFile(branchname, path);
            var fileUrl = await _fileStorage.GetDownloadUrl(file.BranchId + "/" + file.Id);
            var fildeDto = new MetafileDto { FileName = file.Id, Path = file.Path, URL = fileUrl.AbsoluteUri, MD5Hash = file.MD5Hash };
            return fildeDto;
        }
        #endregion
    }
}