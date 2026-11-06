# NFSU2 CCS

**Custom Car Saves** for *Need for Speed: Underground 2* — replace cars, tune dynos, manage liveries, and keep a save rack.

## Download

1. Open **[Releases](https://github.com/acpanel87-tech/NFSU2CustomCarSaves/releases)**
2. Download the latest **Beta** zip (marked *Pre-release*, `*-win-x64.zip`)
3. Extract the **`NFSU2CustomCarSaves`** folder
4. Run **`NFSU2CustomCarSaves.exe`**

Self-contained **Windows x64** — no separate .NET install. Keep the whole folder together (do not move only the `.exe`).

```
NFSU2CustomCarSaves/
  NFSU2CustomCarSaves.exe
  README.txt
  Assets/     UI + flags
  Data/       your libraries (writable)
  Stock/      Presets, DynoSaves, Templates, Stats
  Tools/      Wipe-All.bat
```

## Preview

![Main + Performance](Release%20Images/image3.png)

![Replace car](Release%20Images/image.png)

![Dyno](Release%20Images/image4.png)

![Livery](Release%20Images/image2.png)

## Usage

- Open your NFSU2 profile / `GARAGE.DAT` when prompted
- Stock dyno / livery libraries and save-rack seed load on first run
- **Add / Replace car** places a stock body and resets dyno to midpoint so old ECU/turbo curves do not stick

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
.\release.ps1 -Version 0.1.0-beta.2
# stable later:
.\release.ps1 -Version 1.0.0 -Stable
```

## License / game files

This tool is a fan utility. Need for Speed Underground 2 and related trademarks belong to their respective owners. Do not redistribute copyrighted game binaries.
