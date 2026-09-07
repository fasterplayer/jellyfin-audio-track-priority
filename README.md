# Audio Track Priority (Jellyfin plugin)

Jellyfin plugin that fixes default audio/subtitle track selection when several tracks share the same ISO language code and are only distinguishable by their `Title` field — the classic case being a Québécois French (VFQ) dub next to a France French (VFF) dub, both tagged `fra`.

GUID: `6b8790b2-e367-4076-8e07-9fc092702b32`

Repo layout follows the [official Jellyfin plugin template](https://github.com/jellyfin/jellyfin-plugin-template).

## How it works

A dependency-injection decorator on `IMediaSourceManager`, registered through `IPluginServiceRegistrator`, re-evaluates the default audio and subtitle track on every media source Jellyfin returns. No media file is modified — the decision is purely at serve time.

For each per-language rule (`LanguageRule`) you configure:

- an ordered list of case-insensitive substring patterns to look for in the audio track title (for example `Québec`, `Canad`), most preferred first;
- a subtitle mode of `Same` (reuse the audio language/patterns), `Custom` (its own language and patterns — e.g. always default to English subtitles), or `Off` (leave subtitle selection to Jellyfin);
- an optional "prefer a Forced subtitle track" setting, used as a tiebreaker per rule when the patterns don't produce a clear winner.

Rules are matched against the viewer's own "preferred audio language" setting (or a configured fallback), with language codes expanded through Jellyfin's culture tables so that regional variants (e.g. `fr-CA` / `frc`) still match a rule written for the parent language (`fra`).

## Finding the title to use for a pattern

The config page's preset dropdown (VFQ, VFF, Brasil, Latino, etc.) covers the common cases, but a pattern only works if it matches (even partially, case-insensitively) the actual `Title` tag stored on the track. When a file doesn't fit a preset, read that tag directly instead of guessing:

- **In Jellyfin itself** (no tools needed): open the item, click the "..." menu (or the ⓘ icon in the player) → **Media info**. Every audio and subtitle track is listed there with its `Title` exactly as stored in the file.
- **With `ffprobe`**, run against the file from inside the Jellyfin container (it ships with `jellyfin-ffmpeg`) or from any machine with ffmpeg installed:

```
docker exec jellyfin ffprobe -v quiet -print_format json -show_streams "/path/inside/container/Movie.mkv" | jq '.streams[] | select(.codec_type=="audio" or .codec_type=="subtitle") | {index, codec_type, language: .tags.language, title: .tags.title}'
```

- **With `mkvmerge`** (from the `mkvtoolnix` package, or the `jlesage/mkvtoolnix` Docker image) if you'd rather not touch the Jellyfin container:

```
docker run --rm -v "/volume1/Docker/media:/media" jlesage/mkvtoolnix mkvmerge -J "/media/Movie/Movie.mkv" | jq '.tracks[] | {id, type, language: .properties.language, title: .properties.track_name}'
```

Adjust the container name, mounted path and file path to match your setup. Whatever the `title` field comes back as (or `""`/`null` if the track has no title at all — in which case a title-based rule can never match it, and only the language/Forced-flag logic applies) is exactly what your rule's pattern needs to match a substring of.

## Project layout

- `Jellyfin.Plugin.AudioTrackPriority.slnx` — solution file
- `Directory.Build.props` — shared version properties (assembly/file/package version)
- `build.yaml` — plugin manifest (name, GUID, version, target ABI, description, changelog)
- `Jellyfin.Plugin.AudioTrackPriority/`
  - `Plugin.cs` — plugin entry point
  - `ServiceRegistrator.cs` — DI registration of the decorator
  - `PriorityMediaSourceManager.cs` — the `IMediaSourceManager` decorator
  - `TrackSelector.cs` — audio/subtitle selection logic
  - `Configuration/PluginConfiguration.cs` — global plugin configuration
  - `Configuration/LanguageRule.cs` — per-language rule (audio + subtitle)
  - `Configuration/configPage.html` — admin configuration page

## Building

```
dotnet build Jellyfin.Plugin.AudioTrackPriority.slnx -c Release
```

Copy the resulting `Jellyfin.Plugin.AudioTrackPriority.dll` (and a `meta.json` derived from `build.yaml`) into your Jellyfin server's plugin directory (e.g. `plugins/AudioTrackPriority/`) and restart Jellyfin.

## CI

`.github/workflows/build.yaml` and `.github/workflows/test.yaml` call the shared, public `jellyfin/jellyfin-meta-plugins` reusable workflows to build and test the plugin on every push and pull request — no secrets required. The template's org-specific workflows (release publishing to the official Jellyfin repository, auto-rebase, label sync, changelog bot) were left out since they depend on Jellyfin-organization bot accounts and secrets that don't apply to a personal repository.

## Current version

**1.2.0.0** — targets Jellyfin 10.11 / net9.0. Per-rule "prefer a Forced subtitle track" setting (previously a single global checkbox).
