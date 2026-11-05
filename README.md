# Personal_Medical_Tracking_Calender

A native Windows calendar app built with **C# / WPF on .NET 10 (LTS)**.

## Requirements (development)
- Windows 10 1809+ or Windows 11, x64
- .NET 10 SDK: `winget install Microsoft.DotNet.SDK.10`
- Editor: VS Code with the **C# Dev Kit** extension, or Visual Studio 2022/2026 (open `MedicalCalendar.sln`)

## Run / build
| Task | Command |
|---|---|
| Run (debug) | `run.cmd` or `dotnet run --project src/MedicalCalendar` |
| Release exe | `publish.cmd` writes `dist\MedicalCalendar.exe` |

The release exe is **self-contained**, so the target PC doesn't need .NET installed.

## Why Windows Security doesn't block it
- **No UAC prompt:** the manifest runs the app as `asInvoker`, never as administrator.
- **No SmartScreen warning when built locally:** SmartScreen only checks files that carry the "downloaded from internet" mark (Mark-of-the-Web). An exe you build yourself doesn't have that mark.
- **Fewer antivirus false positives:** single-file compression and self-extraction are turned off, and the exe has full version and publisher info.
- **Controlled Folder Access is not triggered:** data is saved to `%LOCALAPPDATA%\MedicalCalendar\`, not to Documents or Desktop.
- **PowerShell execution policy doesn't matter:** the helper scripts are `.cmd` files, not `.ps1`.

If you **send the exe to other people** (download, email, or USB from the internet), SmartScreen will show
"Windows protected your PC" until the exe is code-signed. Options:
1. Sign it with a code-signing certificate, e.g. Azure Trusted Signing at about $10/month, which is the cheapest trusted option.
2. Publish it through the Microsoft Store as MSIX. The Store signs it for you.
3. For personal use only: right-click the exe → Properties → check **Unblock**.

## Smooth performance
- ReadyToRun precompilation and Tiered PGO for fast startup
- WPF renders on the GPU (DirectX), with Per-Monitor-V2 DPI support so it stays sharp on 4K and mixed monitors
- Saves are debounced and atomic (temp file, then swap), so the UI doesn't stall and data isn't corrupted by a crash
