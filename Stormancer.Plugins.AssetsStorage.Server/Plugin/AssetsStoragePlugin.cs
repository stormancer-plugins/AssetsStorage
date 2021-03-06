﻿using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer;
using Server.Plugins.AdminApi;
using Stormancer.Server;
using System.Threading;
using System.Threading.Tasks;
using Server.Plugins.Configuration;
using System;
using Stormancer.Diagnostics;

namespace Stormancer.Server.AssetsStorage
{
    class AssetsStoragePlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.assetsstorage";

        private const string LOG_CATEGORY = "AssetsStoragePlugin";
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private IAssetsStorageRepository _assetStorageRepo = null;
        private Task _flushTask = null;
        private IConfiguration _config;
        private IHost _host;
        private int _cacheDuration = 60;

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

            ctx.HostStarted += (IHost host) =>
            {
                _host = host;
                _config = host.DependencyResolver.Resolve<IConfiguration>();
                _config.SettingsChanged += OnSettingsChange;
                OnSettingsChange(_config, _config.Settings);

                _assetStorageRepo = host.DependencyResolver.Resolve<IAssetsStorageRepository>();
                _flushTask = Task.Run(() => FlushSchedule(), _cts.Token);
            };

            ctx.HostShuttingDown += (IHost host) =>
            {
                _cts.Cancel();
            };
        }


        private void OnSettingsChange(object sender, dynamic settings)
        {
            _cacheDuration = (int?)settings.assetsStorage?.cacheDuration ?? 60;

            if (settings.assetsStorage?.cacheDuration == null)
            {
                _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Warn, LOG_CATEGORY, $"Failed to find settings in assetsStorage -> MaxCacheLogDuration ! Settings is with default value : {_cacheDuration}", new { cacheDuration = _cacheDuration });
            }
        }

        private async Task FlushSchedule()
        {
            try
            {
                var token = _cts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_cacheDuration * 1000, _cts.Token);
                        await _assetStorageRepo.Flush();
                    }
                    catch (Exception ex)
                    {
                        _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, LOG_CATEGORY, "Failed to flush assetsStorage", ex);
                    }
                }
                await _assetStorageRepo.Flush();
            }
            catch (Exception ex)
            {
                _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, LOG_CATEGORY, "Failed to flush assetsStorage when server shutting down", ex);
            }
        }
    }
}

