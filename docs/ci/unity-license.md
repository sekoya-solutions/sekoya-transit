# Unity license for GitHub Actions

## Unity Personal license

For a **Personal** license, you do not need to find or upload an `.alf` file. The `.alf` → `.ulf` browser workflow is the older manual activation route; Unity says manual activation is not supported for Personal licenses. Activate Personal through Unity Hub instead. See Unity's [license activation methods](https://docs.unity3d.com/6000.3/Documentation/Manual/LicenseActivationMethods.html) and GameCI's [GitHub activation guide](https://game.ci/docs/github/activation/).

1. Open Unity Hub and sign in to the Unity account you want to use for CI.
2. Open **Settings/Preferences → Licenses**. Select **Add** and complete **Get a free Personal license**. Even if Hub already displays a license, GameCI recommends completing the Add flow so the local license file is present.
3. On Windows, find the resulting `Unity_lic.ulf` at `C:\ProgramData\Unity\Unity_lic.ulf`. `C:\ProgramData` is hidden by default; in File Explorer, enter `%ProgramData%\Unity` in the address bar. If the file is absent, re-check that the Hub Add/activation flow completed successfully.
4. In GitHub, open the repository's **Settings → Secrets and variables → Actions → New repository secret**. Add these three secrets:

   | Secret name | Value |
   | --- | --- |
   | `UNITY_LICENSE` | The **entire contents** of `Unity_lic.ulf` (open the file in a text editor, copy all, paste as the secret value). |
   | `UNITY_EMAIL` | Email address of the Unity account used in Unity Hub. |
   | `UNITY_PASSWORD` | Password for that Unity account. |

Do not commit the `.ulf` file or put license contents or account credentials in the repository, issues, pull requests, or chat. GitHub does not show secret values after they have been saved. If the license changes or expires, update `UNITY_LICENSE` with the new file contents.

### If you sign in to Unity with Google

Do **not** put your Google password in `UNITY_PASSWORD`. GameCI's documented Personal-license setup expects a Unity account email and password; it does not take a Google OAuth login as the password.

GameCI's troubleshooting guide says Google sign-in users can sign in to the Unity website, open **Settings → Security**, and set/change a Unity account password there. Set a Unity-specific password (not your Google password), then use that value for `UNITY_PASSWORD`. See [GameCI's Google-login guidance](https://game.ci/docs/troubleshooting/common-issues/#i-use-my-google-account-to-login-to-unity-how-do-i-get-my-username-and-password). If that option is unavailable or doesn't work for your account, contact [Unity Support](https://support.unity.com/) rather than sharing or storing Google credentials.

After all three secrets are saved, open the failed Unity CI run for PR #75 and choose **Re-run jobs**. The Unity job checks the secret values are present before invoking GameCI. The current workflow will fail this preflight clearly if any are missing; it will not silently skip Unity validation or report a successful Unity job.

The regular .NET job continues to run independently. GitHub does not expose repository secrets to workflows triggered from fork pull requests, so those Unity jobs fail their preflight until run from a trusted branch/repository context.

The Unity workflow frees unused space on each Ubuntu runner before downloading GameCI's large Unity Docker images. This is needed because the default GitHub-hosted runner disk can be too small for the editor image.

Unity EditMode tests run on pull requests that change `TransportTycoon2/**`, and on manual runs. They do not run on pushes to `main`; documentation- and workflow-only pull requests skip Unity CI. Revisit these path filters when the Unity project starts consuming code from `dotnet/` (ADR 0016 does not yet define that). Windows and WebGL builds are available only through **Actions → Unity CI → Run workflow**; they never run automatically on pull requests or pushes to `main`. This avoids consuming the repository's limited GitHub Actions minutes on post-merge builds.

Before merging a pull request that changes the Unity project, run the EditMode tests and both platform builds locally as the final validation step, using the Unity version in `TransportTycoon2/ProjectSettings/ProjectVersion.txt`. Close the Unity Editor before running batch-mode commands. From the repository root in PowerShell:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$Project = (Resolve-Path ".\TransportTycoon2").Path
$Output = Join-Path $Project "Builds\PreMerge"
New-Item -ItemType Directory -Force -Path $Output, (Join-Path $Output "Windows"), (Join-Path $Output "WebGL") | Out-Null

& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testResults (Join-Path $Output "EditMode-results.xml") -logFile (Join-Path $Output "EditMode.log") -quit
if ($LASTEXITCODE -ne 0) { throw "Unity EditMode tests failed; see $Output\EditMode.log" }

& $Unity -batchmode -nographics -projectPath $Project -buildTarget StandaloneWindows64 -buildWindowsPlayer (Join-Path $Output "Windows\TransportTycoon2.exe") -logFile (Join-Path $Output "Windows-build.log") -quit
if ($LASTEXITCODE -ne 0) { throw "Windows build failed; see $Output\Windows-build.log" }

& $Unity -batchmode -nographics -projectPath $Project -buildTarget WebGL -buildWebGLPlayer (Join-Path $Output "WebGL") -logFile (Join-Path $Output "WebGL-build.log") -quit
if ($LASTEXITCODE -ne 0) { throw "WebGL build failed; see $Output\WebGL-build.log" }
```

The build output is under Unity's ignored `Builds/` directory. Skip these local builds for documentation- or workflow-only changes; they do not affect Unity project output.

If your Unity license is not Personal (for example, a qualifying paid license or a floating license server), do not use the Personal steps above; follow the corresponding [GameCI activation instructions](https://game.ci/docs/github/activation/) and update the workflow to match.
