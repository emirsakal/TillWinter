# Till Winter — Cutting a Release

Step by step, for the developer. Version 1.0.0, Android min API 25 / target API 35 / IL2CPP
ARM64, iOS 15+, iPhone only.

## 1. Keystore, once

Create the Android keystore a single time and reuse it for every release — Play App Signing
uploads it once and Play re-signs from there.

Either from Unity: **Player Settings → Publishing Settings → Keystore Manager**, or from a
terminal:

```
keytool -genkeypair -v -keystore tillwinter.keystore -alias tillwinter -keyalg RSA -keysize 2048 -validity 10000
```

Store the resulting `.keystore` file outside the repo, plus one backup copy somewhere separate
(a password manager attachment or an encrypted drive). Never commit it, and never write its
password anywhere in the repo.

## 2. Environment

Before building, set these four variables for one shell only — they're read for the duration of
a single build and nowhere else:

```
TW_KEYSTORE_PATH
TW_KEYSTORE_PASS
TW_KEY_ALIAS
TW_KEY_PASS
```

## 3. Gates before a build

Run, in this order, through the `test-runner` subagent where applicable:

1. `run-tests-summary.bat`
2. `smoke-test-summary.bat`
3. `balance-sim.bat`
4. `release-compile-check.bat`
5. `ui-tour.bat <folder>` (eyeball the screenshots for layout regressions)

Don't build until all five are clean.

## 4. Android build

```
build-android.bat
```

Output: `Builds/Android/<version>-<code>/` containing the signed `.aab` and a sideloadable `.apk`.
The version code is bumped only by a real release build — a `-dev` build reuses the current code,
so it never burns a version code you'd want for the store.

## 5. iOS build

```
build-ios.bat
```

This writes an Xcode project. From there, on a Mac:

1. Open the project in Xcode, select the signing team.
2. **Product → Archive**.
3. **Distribute App** through Organizer.

`Info.plist` and `PrivacyInfo.xcprivacy` are written by the build's post-process step — don't hand
-edit them in Xcode.

## 6. Version bump

Version and build numbers move only through `BuildPipeline`, never by hand-editing Player
Settings.

## 7. Rollback and hotfix notes

Save compatibility runs one direction: an older build can only read saves up to its own schema
version. If a hotfix needs to go out on an older build, its players' saves are safe as long as no
newer build has bumped the schema past what that build understands. Never ship a schema bump
without its migration step and its hand-written JSON fixture of the previous version (see skill
`save-versioning`) — that fixture is what proves an old save still loads.

## 8. After release

- Tag the release in git: `v1.0.0`.
- Confirm the keystore backup is still in place and reachable.
- Watch crash reports. Upload the `symbols.zip` Unity writes next to the `.aab` so Play can
  symbolicate native crashes.
