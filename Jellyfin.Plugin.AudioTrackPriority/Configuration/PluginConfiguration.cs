using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioTrackPriority.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin changes the default audio track.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a log line is written every time a track is overridden.
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule is picked from the viewing user's own
    /// "preferred audio language" playback setting. When disabled, <see cref="FallbackLanguage"/>
    /// is used for everyone.
    /// </summary>
    public bool UsePreferredAudioLanguage { get; set; } = true;

    /// <summary>
    /// Gets or sets the language used when the user has no preferred audio language set, or when
    /// the request carries no user context.
    /// </summary>
    public string FallbackLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether only tracks whose own language matches the rule's
    /// language are considered. Turning this off lets a rule match a track in any language.
    /// </summary>
    public bool RestrictToLanguage { get; set; } = true;

    /// <summary>
    /// Gets or sets the per-language rules.
    /// </summary>
    public LanguageRule[] Rules { get; set; } = Array.Empty<LanguageRule>();
}
