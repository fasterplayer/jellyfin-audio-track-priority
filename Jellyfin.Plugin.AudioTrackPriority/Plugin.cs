using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AudioTrackPriority.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AudioTrackPriority;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Audio Track Priority";

    public override Guid Id => Guid.Parse("6b8790b2-e367-4076-8e07-9fc092702b32");

    public override string Description =>
        "Picks the default audio track by track title when several tracks share the same language code, " +
        "such as a Quebec French dub next to a France French dub. No media file is modified.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}
