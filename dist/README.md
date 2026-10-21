# dist/

Build outputs are **not** committed (`dist/*` is git-ignored).

The playable package is published as a GitHub **Release** asset instead:

- `VampireHuntersVR-0.9.6.zip` — firmware-free, self-contained installer package
- `VampireHuntersVR-0.9.6.sha256.txt` — SHA256 of the ZIP plus the three mod DLL hashes

To rebuild locally, see `README.md` ("Building"), then run `scripts\pack-release.ps1 -Version 0.9.6`. The ZIP layout is:

```
VampireHuntersVR-0.9.6/
  install.bat, install.ps1, uninstall.bat, uninstall.ps1
  VhvrSettings.exe (+ dll/deps/runtimeconfig)
  payload/            <- copied 1:1 into the game folder (BepInEx, dotnet, winhttp.dll, doorstop)
  licenses/           <- BepInEx, Doorstop, HarmonyX, OpenXR loader texts
  README-KO.md, SETTINGS-README-KO.md, ModNotes.txt, THIRD-PARTY-NOTICES.txt, LICENSE.txt
```
