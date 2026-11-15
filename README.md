# Hunters' Accord — Seamless Co-op for Bloodborne

Hunters' Accord is a seamless co-op mod for Bloodborne (CUSA03173 v01.09) running in the
[shadPS4](https://github.com/shadps4-emu/shadPS4) emulator. This repository holds the source of its **launcher**
(`HuntersAccord.exe`, Avalonia, .NET 10) and the libraries the launcher is built from, under the MIT License.
The co-op mod itself (the library shadPS4 loads, in the `mod` folder of a release) is closed source and ships
as a binary only.

Version of this snapshot: 0.2.14.

## What you need

- **Your own copy of Bloodborne** 1.09 (CUSA03173), dumped from your own PS4. Hunters' Accord ships no game files,
  no emulator binaries and no decryption keys; unpacking PKG files needs your own keys.
- **shadPS4**: the launcher downloads the official build from github.com/shadps4-emu/shadPS4 **only with your
  consent** (it asks first, or you press "Download / update" in Settings › Emulator). It never downloads or updates it by itself.
- For online play: Steam, signed in, with a FromSoftware game on your account (the co-op session runs under its app id).
  Co-op goes through Valve's relays by default; a direct connection is used when both players choose it in the game
  (System › Network › Connection Mode).

## Network use

The launcher contacts the internet only for these, each listed here with when it happens:

| What | Where | When |
|---|---|---|
| shadPS4 release list and download | api.github.com, github.com (shadps4-emu/shadPS4) | only when you agree to the download dialog or press "Download / update" |
| Community patch files (GoldHEN, shadPS4) | raw.githubusercontent.com (illusionyy/PS-Game-Patch, shadps4-emu/ps4_cheats) | only when you press Update on the Patches page |
| New version check | api.github.com (`/repos/HappyEntity/HuntersAccord/releases/latest`): reads the latest release tag only | at most once a day; off in Settings › General › "Check for new versions" (then no request at all). Never downloads or installs anything |
| Links you click (release page, shadPS4, patches, debug font) | your browser | when you click them |

The launcher also serves a small co-op session directory on `localhost` (for two game instances on one PC) through
the mod library; it does not listen on the network. The co-op mod, inside the game, uses Steam's networking
(lobbies and relays) for online play.

## Building the launcher

Requirements: the .NET SDK 10 (see `global.json`).

```bash
dotnet build HuntersAccord.slnx
dotnet test tests/HuntersAccord.Core.Tests
dotnet run --project src/HuntersAccord.Launcher
```

A launcher built from this source alone has no co-op mod: it starts the game in plain shadPS4. To play with the mod,
copy the `mod` folder of an official release next to the built `HuntersAccord.exe`
(`src/HuntersAccord.Launcher/bin/<Configuration>/net10.0/mod`); the launcher finds and loads it at run time.

| Project | Purpose |
|---|---|
| `src/HuntersAccord.Launcher` | Avalonia launcher (`HuntersAccord.exe`) |
| `src/HuntersAccord.Core` | PKG installer, shadPS4 downloader, Steam library scan, settings, patches, update check, the bridge to the mod library (`ModLibrary`) |
| `src/HuntersAccord.Platform` | OS-specific services (Windows / Linux / macOS) |
| `tests/HuntersAccord.Core.Tests` | xUnit v3 tests |

## Translations

The UI is English by default. Translations are JSON files in `src/HuntersAccord.Launcher/Resources/Lang/` (copied to
`lang/` next to the exe): copy `en.json` to `<code>.json`, set `$language` and `$author`, translate the values (keep
keys and `{0}` placeholders). `TranslationFilesTests` checks submitted files.

## Releases

Releases (see the Releases page) are zips with the launcher, the translations, the licences and the closed co-op mod
binary in `mod/`. The mod binary is under its own licence, see [MOD-LICENSE.txt](MOD-LICENSE.txt): free to use with
your copy of the game, no reverse engineering, no modification, no redistribution for sale. The SHA-256 of each zip
is given in its release notes (and in the `.sha256` file next to it).

## License

The source in this repository: MIT, see [LICENSE](LICENSE). Third-party components:
`src/HuntersAccord.Launcher/THIRD-PARTY-NOTICES.txt`.

Bloodborne is a trademark of Sony Interactive Entertainment. Hunters' Accord is a fan project, not affiliated with
or endorsed by Sony Interactive Entertainment or FromSoftware. It contains no code from shadPS4 or other
GPL-licensed projects.
