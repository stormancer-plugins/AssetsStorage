using Stormancer;

namespace Stormancer.Server.AssetsStorage
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AdminPlugin("assetsstorage").Name("Assets Storage");
            builder.AddPlugin(new AssetsStoragePlugin());
        }
    }
}
