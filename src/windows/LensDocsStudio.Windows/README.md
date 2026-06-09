# Lens Docs Studio Windows Shell

This project is the first Windows desktop shell for Lens Docs Studio. It hosts the existing static browser app in WinUI 3 with WebView2 and does not replace the Markdown, Mermaid, import, export, or docs-site runtime.

## Prerequisites

- Windows 10 version 2004 or later.
- .NET 8 SDK or newer.
- Windows App SDK runtime matching the project package version.
- WebView2 Runtime, normally provided by Microsoft Edge on current Windows installations.

## Run Locally

From the repository root:

```powershell
dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj
```

The project copies `index.html`, `assets/`, `docs/`, `manifest.webmanifest`, `icon.svg`, `md-mmd-renderer-v5.html`, and `service-worker.js` into the output folder under `StaticApp/`. WebView2 loads the app through the local virtual host `https://lens-docs-studio.local/`.

## Build

```powershell
dotnet build src/windows/LensDocsStudio.Windows.sln
```

If build tooling is missing, install the WinUI 3/Windows App SDK development components through Visual Studio Installer and retry the command.

## Scope

This shell is intentionally thin. It creates the desktop window, initialises WebView2, and loads the packaged static app. Native file bridge behaviour, file associations, packaging, auto-update, folder watching, and native export flows are left for later phases.
