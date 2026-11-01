# Ninfer Launcher — v0.2.0-alpha

Self-contained Windows (x64) launcher for the **Ninfer** LLM server, tuned for the **RTX 4090 (24 GB)**.

> **Release:** `ninfer-launcher-v0.2.0-alpha` &nbsp;·&nbsp; **Tag:** [`v0.2.0-alpha`](../../releases/tag/v0.2.0-alpha) &nbsp;·&nbsp; **License:** MIT

## Highlights
- Graphical launcher for the Ninfer server (replaces the old `runnerbat`): one-click **Start / Stop / Restart**.
- Pre-tuned context profiles for a 24 GB card (measured safe ceilings, incl. MTP speculation + vision modes).
- Live server terminal with noise filtering and a **prefill / decode tok/s** readout.
- Settings (server directory, model file, port) persisted to `%AppData%\NinferLauncher\settings.json`.
- Fully translated UI.

## 📦 Downloads
| Asset | Platform | Notes |
| --- | --- | --- |
| [ninfer-launcher-v0.2.0-alpha-win-x64.zip](ninfer-launcher-v0.2.0-alpha-win-x64.zip) | Windows x64 | Self-contained — **no .NET install required** |

## 🚀 Install & run
1. Download and extract the zip above.
2. Install the Ninfer server `ninfer-rtx4090-windows-x64-v1.2.0` — see https://github.com/UDPSendToFailed/ninfer-4090 (e.g. to `F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0`).
3. Place the model `Qwen3.8-27B-Uncensored.ninfer` inside that server folder.
4. Run `NinferLauncher-v-2.exe`.
5. In the launcher set the **Ninfer server directory** and **model file**, choose a **profile** and **port**, then click **Start**.
6. Your server endpoint will be `http://127.0.0.1:<port>`.

## ✅ Requirements
- Windows 10/11, x64
- NVIDIA RTX 4090 (24 GB) with a recent NVIDIA driver
- Ninfer server `ninfer-rtx4090-windows-x64-v1.2.0` + the `Qwen3.8-27B-Uncensored.ninfer` model (**not bundled**)

## 📝 Notes
- **Alpha** build — Chat and History pages are placeholders.
- Contexts above ~240k tokens may not bind under Windows WDDM; use a lower profile if the port doesn't come up.

## 📄 License
Released under the [MIT License](LICENSE).