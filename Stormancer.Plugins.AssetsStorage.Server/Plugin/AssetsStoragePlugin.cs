using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer;
using Server.Plugins.AdminApi;
using Stormancer.Server;

namespace Stormancer.Server.AssetsStorage
{
    class AssetsStoragePlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.assetsstorage";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<AssetsStorageService>().As<IAssetsStorageService>();
                  builder.Register<AssetsStorageController>().InstancePerRequest();
                  builder.Register<AssetsStorageWebApiConfig>().As<IAdminWebApiConfig>();
                  builder.Register<AssetsStorageAdminController>();
                  builder.Register<ESAssetsStorageRepository>().As<IAssetsStorageRepository>().SingleInstance();
                  builder.Register<AssetsStorageService.Accessor>();
              };
                        
            ctx.SceneCreated += (ISceneHost scene) =>
             {                 
                 if (scene.Metadata.ContainsKey(METADATA_KEY))
                 {
                     scene.AddController<AssetsStorageController>();                   
                 }
             };
        }
    }
}
