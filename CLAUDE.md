# CLAUDE.md

Guidance for Claude Code working on the `ninfer-launcher-4090` repository.

## Project & Stack Overview

- **App:** Ninfer Launcher v2 (`NinferLauncher-v-2`), a Windows desktop GUI front-end for the **Ninfer** local LLM server (`ninfer-serve.exe` / `ninfer_bench.exe`) tuned for **NVIDIA RTX 4090 (24 GB)**.
- **Framework:** .NET 10 (`net10.0` / `net10.0-windows`), C# 14.
- **Desktop UI & MVVM:** Avalonia UI 12 (`.axaml` + `.axaml.cs`) + `CommunityToolkit.Mvvm` (source generators).
- **Chat message rendering:** Simplecto `AvRichTextBox` (read-only, drag-select + copy). Built from the **vendored source** at `../.api-test/avrsrc/AvRichTextBox` via `ProjectReference` (NOT the `Simplecto.Avalonia.RichTextBox` NuGet package – the local tree is a newer superset of 1.11.6 and contains local fixes, e.g. the drag-selection scroll-offset fix in `RichTextBox_PointerEvents.cs`). The app wraps the control in `Controls/ChatRichTextBox.cs` (public `Doc` property mirror, because the library's `FlowDocumentProperty` field is internal). The library ships its own test suite: `dotnet test` in `../.api-test/avrsrc` (`AvRichTextBox.Tests`, ~239 tests). A Release build of the library also packs a NuGet to `D:\PROG\LocalNuget\AvRichTextBox` (its `GeneratePackageOnBuild`).

## Build & Publish Commands

Run from the root directory containing `NinferLauncher-v-2.csproj`:

```bash
dotnet restore && dotnet build
dotnet run -c Release
dotnet publish -c Release -r win-x64 --self-contained false -o out
```

## Complete Repository Map

### Entry & Navigation
- `Program.cs` / `App.axaml` / `App.axaml.cs` — Avalonia entry point and global styles.
- `ViewLocator.cs` — Maps `*ViewModel` types to `*View` types.

### Views & ViewModels (`x:DataType` 1:1 Mapping)
- `Views/MainWindow.axaml` ↔ `ViewModels/MainViewModel.cs` (root shell & navigation)
- `Views/ServerView.axaml` ↔ `ViewModels/ServerViewModel.cs` (start/stop/restart, profile dropdown, `--wddm-evictable-budget`, live terminal)
- `Views/ProfileEditorView.axaml` ↔ `ViewModels/ProfileEditorViewModel.cs` (`profiles.json` editor & validation)
- `Views/ChatView.axaml` ↔ `ViewModels/ChatViewModel.cs` (local LLM chat UI)
- `Views/HistoryView.axaml` ↔ `ViewModels/HistoryViewModel.cs` (session/benchmark history)
- `Views/SettingsView.axaml` ↔ `ViewModels/SettingsViewModel.cs` (server dir, model filename, port config)
- `ViewModels/ViewModelBase.cs` — Base class inheriting `ObservableObject`.

### Models, Services & Controls
- `Models/ServerSettings.cs` — Settings model, built-in fallback `ServerProfiles`, and case-insensitive `profiles.json` loader.
- `Models/ChatModels.cs` — Chat message/session data structures.
- `Services/ServerService.cs` — External process runner (`ninfer-serve.exe` / `ninfer_bench.exe`) and stdout/stderr streaming.
- `Services/LogFilter.cs` — Noisy-log filtering and prefill/decode `tok/s` metrics parser.
- `Services/SettingsStore.cs` — Persists `settings.json` next to the `.exe` (with `%AppData%\NinferLauncher\settings.json` fallback).
- `Services/ChatMarkdown.cs` — Turns a chat message into the `FlowDocument` the chat bubble renders (AI replies parsed as Markdown via Markdig, user lines as plain text; falls back to raw text on parse failure). **Gotcha:** the library's `Block` defaults (`FontFamily "Meiryo"`, `FontSize 16`) and the `EditableRun` constructor default (`FontSize 16`) silently override the bubble's XAML `FontFamily="Inter" FontSize="15"`, so every `Paragraph` **and every `EditableRun`** created here must set `FontFamily`/`FontSize` explicitly (body = Inter/15, code = Cascadia Mono/12, inline code 13.5, caption 10.5). Runs without an explicit size render at 16.
- `Controls/IconButton.cs` — Custom reusable icon button control.
- `Controls/ChatRichTextBox.cs` — Wrapper around the vendored `AvRichTextBox.RichTextBox` for the read-only chat message bubbles (public `Doc` binding mirror; caret hidden until the bubble is focused, since read-only non-tab-stop bubbles never get logical focus).

## Avalonia 12 Strict Rules

- **Compiled Bindings Are Mandatory:** Avalonia 12 enables `AvaloniaUseCompiledBindingsByDefault`. Every root view (`Window`, `UserControl`) and every `DataTemplate` using `{Binding ...}` MUST declare `x:DataType="..."`. Only use `{ReflectionBinding ...}` when untyped/dynamic binding is strictly required.
- **Avalonia 12 APIs Only (No v11/WPF Hallucinations):**
  - Never reference removed v12 APIs/packages: `Avalonia.Diagnostics` (use `AvaloniaUI.DiagnosticsSupport` + `AttachDeveloperTools()`), `Avalonia.Direct2D1`, `Avalonia.Browser.Blazor`, or `BinaryFormatter`.
  - Never instantiate `Screen` directly (it is abstract in v12; use `Screens.All`, `Screens.Primary`, or `Screens.ScreenFromWindow(...)`).
  - `DataValidationErrors` sits on the base `Control` class in Avalonia 12.
  - Prefer Avalonia 12's `TableView` over `DataGrid` for read-only tabular data unless inline cell editing is required.
- **XAML & Styling:**
  - Use `avares://NinferLauncher-v-2/Path/Asset.png` for embedded assets (never `pack://`).
  - Use `FluentTheme`, CSS-like `Style` selectors, and `ControlTheme` rather than WPF `Style TargetType` overrides.
  - Keep `.axaml.cs` minimal; put state and commands in ViewModels.

## CommunityToolkit.Mvvm Rules

- ViewModels MUST be `public partial class` inheriting from `ViewModelBase` (or `ObservableValidator` / `ObservableRecipient`).
- Use `[ObservableProperty]` on private camelCase fields (`_userName` → `UserName`), `[NotifyPropertyChangedFor(nameof(...))]` for dependent properties, and `partial void OnXxxChanged(...)` hooks.
- Use `[RelayCommand]` on methods (`SaveAsync` → `SaveCommand`) with `[NotifyCanExecuteChangedFor(nameof(...))]` instead of manual `ICommand` boilerplate.
- Replace or batch-update `ObservableCollection<T>` properties rather than firing hundreds of individual `.Add()` notifications on the UI thread.

## Ninfer Domain & Hardware Rules

- **CLI Flag Mapping:** Profiles map `maxContext` (`--max-context` / `--kv-capacity`), `kvDtype` (`bf16`, `int8`, `rk2v4-e8`, `rk4v4-e8`, `rk4v4`, `rk8v4`), `mtpSpec`/`draftTokens` (`--spec mtp --draft-tokens N --lm-head-draft`), `vision` (`none` / `tokens4096` / `tokens8192`), `isBench` (`ninfer_bench.exe` vs `ninfer-serve.exe`), and optional `--wddm-evictable-budget`.
- **Config Resiliency:** `profiles.json` parsing must skip malformed entries and fall back to built-in profiles without crashing.
- **RTX 4090 / WDDM Limits:** Keep UI warnings for context lengths > 240k tokens; experimental (`red`) profiles target a ~3 GB KV-cache budget.

## Verification & Editing Workflow

- Read the target `.axaml` and its paired `*ViewModel.cs` together before editing so `x:DataType` and property names match 100%.
- Keep edits minimal and localized; avoid reformatting unrelated XAML blocks.
- Run `dotnet build` immediately after editing `.axaml` or `.cs` files so `AvaloniaXamlIl` and CommunityToolkit source generators validate all bindings and generated members.

## After finished workflow
- Update CAULDE.md information about the project
- update README.md