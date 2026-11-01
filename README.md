# NInfer Launcher v2

Desktop launcher for the **NInfer** local inference server, tuned for **NVIDIA RTX 4090 (24 GB)**.
It is a graphical front-end (replacement for `runnerbat` / `RunNinfer.bat`) that starts
`ninfer-serve.exe` / `ninfer_bench.exe` with pre-tuned profile flags and shows a live
terminal with the throughput (prefill / decode tok/s).

Built with **Avalonia UI 12** + **CommunityToolkit.Mvvm**, targeting **.NET 10** (Windows).

## Preview

![NInfer Launcher v2 preview](images/screenshot.png)

## Features

- One-click start / stop / restart of the NInfer server.
- Pre-tuned profiles with verified context ceilings for a 24 GB card
- Live terminal with noisy-log filtering and a prefill/decode tok/s readout.
- Live configuration (server dir, model file, port) persisted to
  `%AppData%\NinferLauncher\settings.json`.

## Requirements

| Dependency | Details |
| --- | --- |
| **NInfer server** | `ninfer-rtx4090-windows-x64-v1.2.0` from <https://github.com/UDPSendToFailed/ninfer-4090> |
| **Model** | `Qwen3.8-27B-Uncensored.ninfer` |
| **GPU** | NVIDIA RTX 4090 (24 GB) with the latest driver / CUDA runtime the server requires |
| **OS** | Windows 10 / 11 |

> The launcher is only the **launcher** – it does **not** bundle the NInfer server or the
> model. You have to install both separately (see below) and point the launcher at them.

## Installation

### 1. Get the NInfer server

Download and extract
[`ninfer-rtx4090-windows-x64-v1.2.0`](https://github.com/UDPSendToFailed/ninfer-4090) to a
folder on your machine, for example:

```
F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0\
    ├── ninfer-serve.exe
    ├── ninfer_bench.exe
    └── ...
```

### 2. Get the model

Download `Qwen3.8-27B-Uncensored.ninfer` and place it inside the same folder, e.g.:

```
F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0\Qwen3.8-27B-Uncensored.ninfer
```

### 3. Build & run the launcher

Prerequisites: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the project targets
`net10.0`).

```powershell
# from the project folder (the one containing the .sln / .csproj)
dotnet restore
dotnet run -c Release
```

Or build a standalone executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o out
```

### 4. Point the launcher at your setup

In the app's **Settings** screen set:

| Field | Value |
| --- | --- |
| **Server directory** | `F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0` |
| **Model file** | `Qwen3.8-27B-Uncensored.ninfer` |
| **Port** | `11434` (default) |

The defaults above are already baked in as defaults
(`Models/ServerSettings.cs`), so if you installed to that path nothing else to configure.

## Profiles

Profiles are defined in `Models/ServerSettings.cs` (`ServerProfiles`) and documented in
[`CustomProfiles.txt`](CustomProfiles.txt). Each profile is a combination of:

- context length (`--max-context` / `--kv-capacity`),
- MTP speculation (`--spec mtp --draft-tokens N --lm-head-draft`),
- KV cache dtype (`rk2v4-e8`, `rk4v4-e8`, `rk4v4`, `rk8v4`, `int8`),
- vision mode (none / 4k / 8k), and a fixed set of runtime flags
  (`--wddm-evictable-budget`, `--preserve-thinking`, `--max-concurrency 1`, ...).

**Notes for Windows (WDDM):**

- Contexts above ~240k tokens may not work under WDDM; the launcher prints a warning for
  profiles over 240k.

## License

Released under the [MIT License](LICENSE) – free to use, modify and distribute.

## Project layout

```
├── App.axaml / App.axaml.cs      # Avalonia app entry + global styles
├── Program.cs                    # Main / BuildAvaloniaApp
├── ViewLocator.cs                # ViewModel → View mapping
├── Models/
│   └── ServerSettings.cs         # Settings + profiles + argument builder
├── Services/
│   ├── ServerService.cs          # process start/stop, stdout capture
│   ├── LogFilter.cs              # terminal log filtering / tok/s parsing
│   └── SettingsStore.cs          # %AppData%\NinferLauncher\settings.json
├── ViewModels/                   # MVVM view models
├── Views/                        # .axaml views
├── Icons/  and  Assets/          # embedded icons / assets
```

## Troubleshooting

- **"Adresář serveru neexistuje" / "Model nenalezen"** – fix the server directory / model
  path in Settings to the real location of the NInfer folder and model file.
- **Port in use** – change the port in Settings or stop other processes bound to it.
- **Server starts but no response** – for high-context (>240k) profiles fall back to a
  smaller context profile; on WDDM very large contexts can fail to bind.