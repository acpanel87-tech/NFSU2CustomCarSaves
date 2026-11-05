# NFSU2 CCS

**Custom Car Saves** for *Need for Speed: Underground 2* — replace cars, tune dynos, manage liveries, and keep a save rack.

## Download (Discord beta)

1. Open **[Releases](https://github.com/acpanel87-tech/NFSU2CustomCarSaves/releases)**
2. Download the latest **Beta** zip (marked *Pre-release*)
3. Extract the folder
4. Run **`NFSU2CustomCarSaves.exe`**

Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

The beta ships with stock dyno / livery libraries and save-rack seed so you start with the same content as the public build.

## For testers

- Point the app at your NFSU2 profile / `GARAGE.DAT` as prompted
- Stock content is seeded on first run from `Presets/` → `Data/`
- Your personal clipboard and free-roam flag stay local (not in the zip)

## Build from source

```powershell
dotnet build .\NFSU2CustomCarSaves.csproj -c Release
```

Pack a Desktop zip only:

```powershell
.\pack-beta.ps1
```

Publish a GitHub Release (beta by default):

```powershell
.\release.ps1 -Version 0.1.0-beta.1
# stable later:
.\release.ps1 -Version 1.0.0 -Stable
```

## License / game files

This tool is a fan utility. Need for Speed Underground 2 and related trademarks belong to their respective owners. Do not redistribute copyrighted game binaries.
