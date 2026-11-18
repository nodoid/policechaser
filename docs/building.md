# Building, signing and packaging

Everything is built with the .NET SDK; there is no MonoGame content pipeline and no `.mgcb`
step, so a clean clone builds without installing the MonoGame templates.

## Prerequisites

| Tool | Needed for |
| --- | --- |
| .NET 10 SDK | Everything |
| Xcode 26 or later | iOS and macOS targets |
| Android SDK 36 + JDK 17 | Android (`minSdk` 32, `targetSdk` 36) |
| Pillow (`python3 -m pip install pillow`) | The asset and screenshot generators only |

`Directory.Build.props` pins `MonoGameVersion` (3.8.6.1), so all hosts use the same framework
build.

## Build and test

```bash
dotnet build PoliceChaser.slnx
dotnet test tests/PoliceChaser.Tests
```

The test project targets plain `net10.0` and covers only the parts of Core that have no MonoGame
dependency (`Race`, `DifficultyRules`, `Mission`, `HighScores`, `GameSettings`, `TouchMapping`),
which is why the suite runs without a graphics device.

Run the desktop build directly:

```bash
dotnet run --project src/PoliceChaser.Desktop
```

## Desktop (macOS, Windows, Linux)

`src/PoliceChaser.Desktop` is a MonoGame DesktopGL host. On Windows, `tools/publish-windows.sh`
publishes a `WinExe` (no console window) and stages the MSIX manifest and tile art next to it:

```bash
SELF_CONTAINED=1 tools/publish-windows.sh                # win-x64, self-contained
RID=win-arm64 SELF_CONTAINED=1 tools/publish-windows.sh  # Parallels on Apple silicon
```

| Variable | Default | Meaning |
| --- | --- | --- |
| `RID` | `win-x64` | Runtime identifier; output goes to `artifacts/$RID` |
| `SELF_CONTAINED` | `0` | `1` bundles the .NET runtime and compresses to a single file |

The MSIX identity (`49556nodoid.PoliceChaser`) and the splash screen reference live in
`windows/Package.appxmanifest`; the publisher identity is reserved and should be replaced if you
package under your own store account.

For macOS, `tools/make-macos-app.sh` publishes the desktop build and wraps it in a `.app`:

```bash
tools/make-macos-app.sh                                  # ad-hoc signed, runs locally
tools/make-macos-app.sh --sign                           # signed with an installed identity
IDENTITY="Apple Development: ..." tools/make-macos-app.sh --sign
SELF_CONTAINED=1 tools/make-macos-app.sh --sign           # bundles the .NET runtime
RID=osx-x64 SELF_CONTAINED=1 tools/make-macos-app.sh

# Mac App Store build: embed the distribution profile and sign with it
PROFILE=~/Downloads/rel-police-mac.provisionprofile SELF_CONTAINED=1 \
  IDENTITY="Apple Distribution: ..." tools/make-macos-app.sh --sign
```

`--sign` uses `src/PoliceChaser.macOS/entitlements.plist` (sandbox, no network) and signs with
the `IDENTITY` you provide or the string `Apple Development`. `SELF_CONTAINED=1` bundles the
.NET runtime so the `.app` runs on a Mac without the SDK installed; the default build is
framework-dependent and needs the .NET 10 runtime.

The App Store build needs `PROFILE` (`rel-police-mac`): it is copied to
`Contents/embedded.provisionprofile`, and its App ID and team identifier are merged into the
signing entitlements, so the signed app matches the profile without a team ID in the repo.

Release installers are built from the signed `.app` with `productbuild`:

```bash
productbuild --component "artifacts/macos/osx-arm64/Police Chaser.app" /Applications \
  --sign "3rd Party Mac Developer Installer: <your name> (<TEAMID>)" \
  releases/PoliceChaser-<version>-macos-arm64.pkg
```

## Android

```bash
dotnet publish src/PoliceChaser.Android -c Release -f net10.0-android36.0 -o artifacts/android
```

Key project properties in `src/PoliceChaser.Android/PoliceChaser.Android.csproj`:

| Property | Value |
| --- | --- |
| `TargetFramework` | `net10.0-android36.0` |
| `SupportedOSPlatformVersion` | 32.0 |
| `TargetSdkVersion` | 36 |
| `ApplicationId` | `uk.co.allthejohnsons.policechaser` |
| `ApplicationVersion` | version code (2) |
| `ApplicationDisplayVersion` | version name (1.0.1) |
| `AndroidPackageFormat` | `apk` (switch to `aab` for Play submission) |

Version codes live in **two** places and must agree: the csproj properties above and
`versionCode` / `versionName` in `AndroidManifest.xml`.

Signing: keep the upload keystore outside the repository. `keys/` is gitignored for exactly this
reason. Pass it at publish time rather than putting secrets in the csproj:

```bash
dotnet publish src/PoliceChaser.Android -c Release -f net10.0-android36.0 -o artifacts/android \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=keys/your-upload.jks \
  -p:AndroidSigningKeyAlias=your-alias \
  -p:AndroidSigningStorePass=... -p:AndroidSigningKeyPass=...
```

The signed result is the `*-Signed.apk` in the output folder. Without a keystore the publish
still produces an unsigned APK for local testing.

The activity is `SensorLandscape`, hides the system bars (re-hiding them in
`OnWindowFocusChanged`, since Android shows them again after dialogs), and sets
`LayoutInDisplayCutoutMode.Always` so the widened canvas can fill the screen around a camera
hole.

## iOS

Simulator build (no signing needed):

```bash
dotnet build src/PoliceChaser.iOS -c Release -f net10.0-ios -r iossimulator-arm64
xcrun simctl boot <UDID>
xcrun simctl install <UDID> src/PoliceChaser.iOS/bin/Release/net10.0-ios/iossimulator-arm64/PoliceChaser.app
xcrun simctl launch <UDID> uk.co.allthejohnsons.policechaser
```

Device / distribution build:

```bash
dotnet publish src/PoliceChaser.iOS -c Release -f net10.0-ios -r ios-arm64 -o artifacts/ios \
  -p:CodesignKey="Apple Distribution: <your name> (<TEAMID>)" \
  -p:CodesignProvision=<your provisioning profile>
```

The publish emits `PoliceChaser.ipa` (plus a `.dSYM`) directly; there is no loose `.app` in the
output folder. Useful details:

* iOS 18+ is the minimum (`SupportedOSPlatformVersion` 18.0, `MinimumOSVersion` in `Info.plist`).
* Since iOS 27 a scene manifest is mandatory. `Info.plist` declares the scene configuration and
  `Program.cs` attaches MonoGame's `UIWindow` to the `UIWindowScene`; without that the window is
  never resized for landscape and the race renders blank.
* Versions live in the csproj (`ApplicationVersion` = `CFBundleVersion`,
  `ApplicationDisplayVersion` = `CFBundleShortVersionString`) and in `Info.plist`.
* `Entitlements.plist` intentionally ships without a team identifier. Add your own if your
  signing setup needs one, or pass the identity/profile on the command line as above.
* Backgrounding pauses the race audio (`PauseAudio`/`ResumeAudio` from the app delegate) so iOS
  does not kill the audio session.

## macOS

The desktop build is the macOS build. Signing entitlements are in
`src/PoliceChaser.macOS/entitlements.plist`; `tools/make-macos-app.sh --sign` applies them. The
`--sign` path needs an *Apple Development* identity and, for distribution, notarisation with
`notarytool` (not automated here).

## Artefacts and versions

| Platform | Output | Ignored by git |
| --- | --- | --- |
| Android | `artifacts/android/*.apk` (`*-Signed.apk` when signed) | yes |
| iOS | `artifacts/ios/PoliceChaser.ipa`, `.dSYM` | yes |
| Windows | `artifacts/win-x64` (or `artifacts/win-arm64`) | yes |
| macOS | `artifacts/macos/<rid>/Police Chaser.app` | yes |
| Screenshots | `artifacts/screenshots/raw`, `artifacts/screenshots/store` | yes |
| Store art | `Stores/` | yes |

`artifacts/`, `Stores/` and `keys/` are all in `.gitignore`; the release builds in this
repository are not committed.

Bump the version in every place that carries one: `Directory.Build.props` (assembly version),
the Android csproj + `AndroidManifest.xml`, and the iOS csproj + `Info.plist`.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Portrait window shows a rotate prompt | Expected: the game is landscape only |
| Black bars or a squashed picture on a phone | The host did not call `SetBackBuffer` before `Run`, so MonoGame kept the 4:3 buffer |
| iOS race renders blank after launch | Scene lifecycle: check `Info.plist` scene configuration and `Program.StartInScene` |
| `InstancePlayLimitException` on iOS | Too many concurrent voices; race loops must be stopped when a run ends |
| `codesign` fails with "no identity found" | The certificate/profile named in `CodesignKey`/`CodesignProvision` is not installed |
| Android package rejected for a reused version code | Bump `ApplicationVersion` and `versionCode` together |
| `dotnet publish` warns `CA1422` for `SetDecorFitsSystemWindows` | Platform-compatibility analyser noise for Android 35+; the runtime check is intentional |
