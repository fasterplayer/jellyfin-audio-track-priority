using System;
using System.Collections.Generic;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.AudioTrackPriority.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTrackPriority;

public static class TrackSelector
{
    public static void Apply(MediaSourceInfo? source, User? user, ILocalizationManager? localization, ILogger? logger)
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config is null || !config.Enabled || source is null)
            {
                return;
            }

            var streams = source.MediaStreams;
            if (streams is null || streams.Count == 0)
            {
                return;
            }

            var audio = new List<MediaStream>();
            var subtitles = new List<MediaStream>();
            foreach (var stream in streams)
            {
                if (stream is null)
                {
                    continue;
                }

                if (stream.Type == MediaStreamType.Audio)
                {
                    audio.Add(stream);
                }
                else if (stream.Type == MediaStreamType.Subtitle)
                {
                    subtitles.Add(stream);
                }
            }

            var rules = config.Rules;
            if (rules is null || rules.Length == 0)
            {
                return;
            }

            // With a single audio track and no subtitles there is nothing to arbitrate.
            if (audio.Count < 2 && subtitles.Count == 0)
            {
                return;
            }

            var target = config.UsePreferredAudioLanguage ? user?.AudioLanguagePreference : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                target = config.FallbackLanguage;
            }

            var targetCodes = ExpandLanguage(target, localization);

            LanguageRule? rule = null;
            if (targetCodes.Count > 0)
            {
                foreach (var candidate in rules)
                {
                    if (candidate is null || string.IsNullOrWhiteSpace(candidate.Language))
                    {
                        continue;
                    }

                    if (Intersects(ExpandLanguage(candidate.Language, localization), targetCodes))
                    {
                        rule = candidate;
                        break;
                    }
                }
            }

            if (rule is null)
            {
                foreach (var candidate in rules)
                {
                    if (candidate is not null && string.IsNullOrWhiteSpace(candidate.Language))
                    {
                        rule = candidate;
                        break;
                    }
                }
            }

            if (rule is null)
            {
                return;
            }

            if (audio.Count >= 2)
            {
                ApplyAudio(source, config, rule, targetCodes, audio, localization, logger);
            }

            if (subtitles.Count > 0)
            {
                ApplySubtitles(source, config, rule, targetCodes, subtitles, localization, logger);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "AudioTrackPriority: selection failed, keeping the original tracks");
        }
    }

    public static void ApplyAll(IReadOnlyList<MediaSourceInfo>? sources, User? user, ILocalizationManager? localization, ILogger? logger)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources)
        {
            Apply(source, user, localization, logger);
        }
    }

    private static void ApplyAudio(
        MediaSourceInfo source,
        PluginConfiguration config,
        LanguageRule rule,
        List<string> targetCodes,
        List<MediaStream> audio,
        ILocalizationManager? localization,
        ILogger? logger)
    {
        var patterns = rule.GetPatterns();
        if (patterns.Count == 0)
        {
            return;
        }

        var restrict = config.RestrictToLanguage
            && targetCodes.Count > 0
            && !string.IsNullOrWhiteSpace(rule.Language);

        MediaStream? best = null;
        var bestRank = int.MaxValue;

        foreach (var stream in audio)
        {
            if (restrict && !Intersects(ExpandLanguage(stream.Language, localization), targetCodes))
            {
                continue;
            }

            var title = stream.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var limit = Math.Min(patterns.Count, bestRank);
            for (var i = 0; i < limit; i++)
            {
                if (title.Contains(patterns[i], StringComparison.OrdinalIgnoreCase))
                {
                    best = stream;
                    bestRank = i;
                    break;
                }
            }
        }

        if (best is null || source.DefaultAudioStreamIndex == best.Index)
        {
            return;
        }

        if (config.VerboseLogging)
        {
            logger?.LogInformation(
                "AudioTrackPriority: default audio track set to #{Index} {Title} (pattern {Pattern}, was #{Previous})",
                best.Index,
                best.Title,
                patterns[bestRank],
                source.DefaultAudioStreamIndex);
        }

        source.DefaultAudioStreamIndex = best.Index;
    }

    /// <summary>
    /// Picks the default subtitle track for <paramref name="rule"/>, following its
    /// <see cref="LanguageRule.SubtitleMode"/>: <c>Off</c> leaves subtitle selection to Jellyfin,
    /// <c>Same</c> reuses the audio rule's language and patterns, and <c>Custom</c> uses the
    /// rule's own <see cref="LanguageRule.SubtitleLanguage"/> and
    /// <see cref="LanguageRule.SubtitlePatterns"/> instead (for example, always defaulting to
    /// English subtitles regardless of the audio language rule). Within the eligible tracks,
    /// title patterns are ranked first (most preferred first); when no patterns are configured,
    /// or none of them match a track's title, a "Forced" track is preferred over a non-forced one
    /// when <see cref="LanguageRule.PreferForcedSubtitle"/> is enabled, since a forced
    /// track usually only translates foreign-language dialogue instead of subtitling the whole
    /// film.
    /// </summary>
    private static void ApplySubtitles(
        MediaSourceInfo source,
        PluginConfiguration config,
        LanguageRule rule,
        List<string> targetCodes,
        List<MediaStream> subtitles,
        ILocalizationManager? localization,
        ILogger? logger)
    {
        var mode = string.IsNullOrWhiteSpace(rule.SubtitleMode) ? "Same" : rule.SubtitleMode.Trim();
        if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<string> patterns;
        List<string> codes;
        bool restrict;

        if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            patterns = rule.GetSubtitlePatterns();
            codes = ExpandLanguage(rule.SubtitleLanguage, localization);
            restrict = codes.Count > 0;
        }
        else
        {
            patterns = rule.GetPatterns();
            codes = targetCodes;
            restrict = config.RestrictToLanguage
                && codes.Count > 0
                && !string.IsNullOrWhiteSpace(rule.Language);
        }

        var preferForced = rule.PreferForcedSubtitle;
        if (patterns.Count == 0 && !preferForced)
        {
            return;
        }

        MediaStream? best = null;
        var bestPatternRank = int.MaxValue;
        var bestForcedRank = int.MaxValue;

        foreach (var stream in subtitles)
        {
            if (restrict && !Intersects(ExpandLanguage(stream.Language, localization), codes))
            {
                continue;
            }

            var patternRank = patterns.Count > 0 ? int.MaxValue : 0;
            var title = stream.Title;
            if (patterns.Count > 0 && !string.IsNullOrWhiteSpace(title))
            {
                for (var i = 0; i < patterns.Count; i++)
                {
                    if (title.Contains(patterns[i], StringComparison.OrdinalIgnoreCase))
                    {
                        patternRank = i;
                        break;
                    }
                }
            }

            if (patterns.Count > 0 && patternRank == int.MaxValue)
            {
                // Configured, but this track's title matched none of the patterns; still keep it
                // in play behind every actual match, so the forced tiebreak below has a fallback.
                patternRank = patterns.Count;
            }

            var forcedRank = preferForced && stream.IsForced ? 0 : 1;

            if (patternRank < bestPatternRank || (patternRank == bestPatternRank && forcedRank < bestForcedRank))
            {
                best = stream;
                bestPatternRank = patternRank;
                bestForcedRank = forcedRank;
            }
        }

        if (best is null || source.DefaultSubtitleStreamIndex == best.Index)
        {
            return;
        }

        if (config.VerboseLogging)
        {
            logger?.LogInformation(
                "AudioTrackPriority: default subtitle track set to #{Index} {Title} (forced: {Forced}, was #{Previous})",
                best.Index,
                best.Title,
                best.IsForced,
                source.DefaultSubtitleStreamIndex);
        }

        source.DefaultSubtitleStreamIndex = best.Index;
    }

    /// <summary>
    /// Expands a language name or code into every equivalent code Jellyfin knows about,
    /// so that "fra", "fre", "fr" and "French" all resolve to the same set.
    /// </summary>
    private static List<string> ExpandLanguage(string? language, ILocalizationManager? localization)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(language))
        {
            return result;
        }

        var trimmed = language.Trim();
        result.Add(trimmed);

        AddWithBarePrefix(result, trimmed);

        var culture = localization?.FindLanguageInfo(trimmed);
        if (culture is null)
        {
            return result;
        }

        AddWithBarePrefix(result, culture.TwoLetterISOLanguageName);

        if (culture.ThreeLetterISOLanguageNames is not null)
        {
            foreach (var code in culture.ThreeLetterISOLanguageNames)
            {
                AddWithBarePrefix(result, code);
            }
        }

        return result;
    }

    /// <summary>
    /// Some of Jellyfin's own regional cultures (e.g. "fr-ca" / "frc" for French Canada) are
    /// stored as siloed entries unrelated to their parent language ("fra" / "French") in
    /// Jellyfin's own culture data. Adding the bare prefix before a hyphen lets a rule for the
    /// parent language ("fra") still match a viewer whose preference is the regional variant,
    /// and vice versa.
    /// </summary>
    private static void AddWithBarePrefix(List<string> result, string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        result.Add(code);

        var separator = code.IndexOf('-', StringComparison.Ordinal);
        if (separator > 0)
        {
            result.Add(code.Substring(0, separator));
        }
    }

    private static bool Intersects(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        foreach (var a in left)
        {
            foreach (var b in right)
            {
                if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
