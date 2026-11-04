# Desktop host spike — findings (A04 / A05 partial / A06 untested)

Disposable prototype. Not part of the product, not in `DbMapper.slnx`, not a basis for the approved host decision (ADR-002 remains provisional, Q1/Q2 open).

## A04 — Windows x64 native host viability (observed pass, with manual items outstanding)

Packages (all from nuspec, Apache-2.0 unless noted):
- Photino.Blazor 4.0.13 (Apache-2.0). Targets net8/net9 only; net10 consumes the net9 lib (no NU1701 on restore).
- Photino.NET 4.0.16 (Apache-2.0).
- Photino.Native 4.0.22 (Apache-2.0), bundles WebView2Loader.
- Microsoft.AspNetCore.Components.WebView 9.0.1 (MIT).

Runtime prerequisite: WebView2 Evergreen runtime (154.0.4258.62 present on test machine, HKLM WOW6432Node EdgeUpdate). Behaviour when absent is **not tested**.

Blocker found and fixed: without `[STAThread]` on `Main`, the window stays black and no `msedgewebview2` child spawns (reproduced on net8 and net10). The spike uses `[STAThread]`.

Observed on Windows 11 Pro 10.0.26200 (x64):
- `MainWindowTitle` = "DbMapper host spike"; `Responding` = True; screenshot shows bundled UI.
- `msedgewebview2` process tree sits under the host PID.
- `LISTEN` count in host process tree = 0.
- No chrome/msedge/firefox process with the host as parent.
- Programmatic `CloseMainWindow`: host exited within ~1 s, no leftover WebView2 children. Relaunch restored window and WebView2 child.
- Build: 0 warnings. `TreatWarningsAsErrors=false` set in the spike csproj only (untested without it).

Not verified by a human (requires user manual check):
- Directory picker (Photino `ShowOpenFolder` in `Pages/Home.razor`) — never clicked.
- Close/reopen via real window chrome.

## A05 — WebView network isolation (partial, online only, NOT a zero-egress claim)

Outbound TCP observed from the host process tree while online (unattributed owner processes):
- 52.98.145.66:443 — owner WebView2 browser process 7632, at ~10 s and ~50 s.
- 150.171.27.11:443 — owner process 27424, at ~50 s.

The app page has no external references. A plain control probe (`ProbePlain`) showed no connections at ~15 s, but that sampling was not time-matched, so it is not a valid control comparison.

DNS cache holds `msedge…tlu.dl.delivery.mp.microsoft.com` entries that may be Edge/WebView update traffic. Per-process DNS not captured (DNS operational log disabled).

Not done: egress-blocked run (requires firewall rule or network monitor with admin approval).

## A06 — Remaining host targets: untested

`wsl -l -v`: Ubuntu and docker-desktop, both Stopped. No Linux, macOS, Debian or Fedora run. Cross-compiling is not a pass.

## Commands

```powershell
cd F:\Projects\dbmapper\experiments\desktop-host\DesktopHostSpike
dotnet publish -r win-x64 -c Release --self-contained false -p:PublishDir=publish/win-x64/
& .\publish\win-x64\DesktopHostSpike.exe
```

Isolation control: `experiments\desktop-host\ProbePlain` (plain Photino.NET).

## User decisions (approved)

1. Egress-blocked A05 run: **approved**. Not performed. This session is not elevated. `scripts/Run-EgressBlocked.ps1` is an **untested draft**; review it, then run it from an elevated PowerShell. Note: its WebView2 block rule affects every WebView2 app on the machine during the run.
2. Manual directory picker and close/reopen checks: user to perform.
3. WebView2 Evergreen accepted as a Windows prerequisite.
4. Photino.Blazor net9-computed path on net10 accepted for the spike. Product approval still open under Q1.
