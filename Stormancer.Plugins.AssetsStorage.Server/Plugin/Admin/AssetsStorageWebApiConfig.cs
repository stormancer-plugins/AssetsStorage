using Server.Plugins.AdminApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Stormancer.Server.AssetsStorage
{
    class AssetsStorageWebApiConfig : IAdminWebApiConfig
    {
        public void Configure(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute("assetsbranch","_assetsstorage/{branchname}", new {Controller = "AssetsStorageAdmin", Action = "branch", branchname = RouteParameter.Optional});
            config.Routes.MapHttpRoute("assetsfile", "_assetsstorage/{branchname}/{*path}", new { Controller = "AssetsStorageAdmin", Action = "file" });            
        }
    }
}
