# {{PRODUCT_NAME}} {{TAG}}

Version: `{{VERSION}}`
Commit: `{{COMMIT_SHA}}`
Date: `{{DATE}}`
Package type: {{PACKAGE_TYPE}}

## Download

- `{{DOWNLOAD_ARTEFACT}}`
- SHA256: `{{SHA256}}`
- RC report: `{{RC_REPORT}}`

## Prerequisites

- {{WINDOWS_VERSION}}.
- .NET 8 Desktop Runtime.
- Windows App SDK Runtime matching `Microsoft.WindowsAppSDK` `{{WINDOWS_APP_SDK_VERSION}}`.
- Evergreen Microsoft Edge WebView2 Runtime compatible with `Microsoft.Web.WebView2` `{{WEBVIEW2_PACKAGE_VERSION}}`.

## Install And Run

1. Download `{{DOWNLOAD_ARTEFACT}}`.
2. Extract the ZIP into a local folder.
3. Run `LensDocsStudio.Windows.exe`.

## Capabilities

- Offline Markdown and Mermaid editor.
- Native file open, save, and save-as.
- Native workspace open, save, and create.
- Native workspace watcher and external-change review UX.
- First-run setup wizard.
- Optional manual per-user file association scripts.

## Known Limitations

- The ZIP is unsigned.
- No MSIX package is included.
- No classic installer is included.
- No auto-update is included.
- No WebView2 bootstrapper is included.
- No machine-wide file associations are included.

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
