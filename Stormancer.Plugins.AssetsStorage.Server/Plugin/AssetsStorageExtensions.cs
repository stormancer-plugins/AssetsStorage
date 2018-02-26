using Server.Plugins.AssetsStorage;
using Stormancer.Core;

namespace Stormancer.Server.AssetsStorage
{
    public static class AssetsStorageExtensions
    {
        public static void AddAssetsStorage(this ISceneHost scene)
        {
            scene.Metadata[AssetsStoragePlugin.METADATA_KEY] = "enabled";
        }
    }
}
