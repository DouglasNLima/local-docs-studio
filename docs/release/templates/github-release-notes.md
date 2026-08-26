# {{PRODUCT_NAME}} {{TAG}}

Version: `{{VERSION}}`
Commit: `{{COMMIT_SHA}}`
Date: `{{DATE}}`
Package type: {{PACKAGE_TYPE}}

## Download

- `{{DOWNLOAD_ARTEFACT}}`
- SHA256: `{{SHA256}}`
- RC report: `{{RC_REPORT}}`

Choose the ZIP when you want a portable/fallback package for manual extraction, smoke validation, or an environment that avoids installers. The ZIP does not create a Start Menu entry or uninstall entry.

Choose the unsigned Inno Setup installer when you want the easier Windows install path. The installer creates a Start Menu shortcut, offers an optional Desktop shortcut, keeps file associations optional/default-safe, and supports uninstall. It still requires the runtime prerequisites below.

## Prerequisites

- {{WINDOWS_VERSION}}.
- .NET 8 Desktop Runtime.
- Windows App SDK Runtime matching `Microsoft.WindowsAppSDK` `{{WINDOWS_APP_SDK_VERSION}}`.
- Evergreen Microsoft Edge WebView2 Runtime compatible with `Microsoft.Web.WebView2` `{{WEBVIEW2_PACKAGE_VERSION}}`.

## Install And Run

### ZIP Package

1. Download `{{DOWNLOAD_ARTEFACT}}`.
2. Extract the ZIP into a local folder.
3. Run `LensDocsStudio.Windows.exe`.

### Inno Installer

1. Download the setup executable when it is included in the prerelease assets.
2. Verify the installer SHA256 against its `.sha256` file.
3. Run the unsigned installer and follow the setup prompts.
4. Keep file associations unchecked unless you want Lens Docs Studio registered for supported Markdown, Mermaid, and text files for your Windows user.

## Capabilities

- Offline Markdown and Mermaid editor.
- Native file open, save, and save-as.
- Native workspace open, save, and create.
- Native workspace watcher and external-change review UX.
- First-run setup wizard.
- Optional manual per-user file association scripts.

## Known Limitations

- ZIP and installer assets are unsigned prerelease assets.
- No MSIX package is included.
- No auto-update is included.
- No WebView2 bootstrapper is included.
- No machine-wide file associations are included.
- WebView2 user data may remain after installer uninstall under `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2`; remove it manually only if you want a fully clean uninstall and no longer need local browser state.

## Verification

From the extracted release folder or a folder containing the ZIP:

```powershell
{{VERIFICATION_COMMAND}}
```

Confirm the hash matches:

```text
{{SHA256}}
```

Review `{{RC_REPORT}}` for the release-candidate certification details.
