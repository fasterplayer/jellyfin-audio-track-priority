using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AudioTrackPriority.Configuration;

/// <summary>
/// One ordered set of title patterns, optionally scoped to a language.
/// </summary>
public class LanguageRule
{
    /// <summary>
    /// Gets or sets the language this rule applies to, as a two or three letter ISO code
    /// (for example <c>fra</c>, <c>por</c>, <c>spa</c>). An empty value makes the rule apply
    /// to every language.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the track title patterns, one per line, most preferred first.
    /// Matching is a case insensitive substring search against the audio track title.
    /// </summary>
    public string Patterns { get; set; } = string.Empty;

    /// <summary>
    /// Splits <see cref="Patterns"/> into a trimmed, ordered list.
    /// </summary>
    /// <returns>The patterns, most preferred first.</returns>
    public List<string> GetPatterns()
    {
        return SplitPatterns(Patterns);
    }

    /// <summary>
    /// Gets or sets how this rule picks the default subtitle track. One of
    /// <c>Same</c> (reuse <see cref="Language"/> and <see cref="Patterns"/>), <c>Custom</c>
    /// (use <see cref="SubtitleLanguage"/> and <see cref="SubtitlePatterns"/> instead), or
    /// <c>Off</c> (leave subtitle selection to Jellyfin).
    /// </summary>
    public string SubtitleMode { get; set; } = "Same";

    /// <summary>
    /// Gets or sets the language subtitle patterns are matched against when
    /// <see cref="SubtitleMode"/> is <c>Custom</c>. An empty value matches subtitles in any
    /// language (useful for always preferring English subtitles regardless of audio language).
    /// </summary>
    public string SubtitleLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subtitle title patterns used when <see cref="SubtitleMode"/> is
    /// <c>Custom</c>, one per line, most preferred first.
    /// </summary>
    public string SubtitlePatterns { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether, when this rule's subtitle patterns don't pick a
    /// clear winner (or no subtitle patterns are configured), a "Forced" subtitle track is
    /// preferred over a non-forced one. Recommended: forced tracks usually only translate
    /// foreign-language dialogue instead of subtitling the whole film.
    /// </summary>
    public bool PreferForcedSubtitle { get; set; } = true;

    /// <summary>
    /// Splits <see cref="SubtitlePatterns"/> into a trimmed, ordered list.
    /// </summary>
    /// <returns>The patterns, most preferred first.</returns>
    public List<string> GetSubtitlePatterns()
    {
        return SplitPatterns(SubtitlePatterns);
    }

    private static List<string> SplitPatterns(string? patterns)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(patterns))
        {
            return result;
        }

        foreach (var line in patterns.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
