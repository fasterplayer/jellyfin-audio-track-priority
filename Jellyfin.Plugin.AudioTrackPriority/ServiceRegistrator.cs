using System;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTrackPriority;

/// <summary>
/// Replaces the registration of <see cref="IMediaSourceManager"/> with a decorator.
/// This works because Jellyfin registers plugin services after its own
/// (see ApplicationHost.Init), so the last registration wins.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        var existing = serviceCollection.LastOrDefault(d => d.ServiceType == typeof(IMediaSourceManager));

        if (existing is null || existing.ImplementationType is null)
        {
            return;
        }

        var innerType = existing.ImplementationType;
        serviceCollection.Remove(existing);

        serviceCollection.AddSingleton<IMediaSourceManager>(provider =>
        {
            var inner = (IMediaSourceManager)ActivatorUtilities.CreateInstance(provider, innerType);
            var localization = provider.GetRequiredService<ILocalizationManager>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AudioTrackPriority");
            logger.LogInformation("AudioTrackPriority: IMediaSourceManager decorated, track priority active");
            return new PriorityMediaSourceManager(inner, localization, logger);
        });
    }
}
