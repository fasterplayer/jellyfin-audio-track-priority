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
