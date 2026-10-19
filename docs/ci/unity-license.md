# Unity license for GitHub Actions

## Unity Personal license

For a **Personal** license, you do not need to find or upload an `.alf` file. The `.alf` → `.ulf` browser workflow is the older manual activation route; Unity says manual activation is not supported for Personal licenses. Activate Personal through Unity Hub instead. See Unity's [license activation methods](https://docs.unity3d.com/6000.3/Documentation/Manual/LicenseActivationMethods.html) and GameCI's [GitHub activation guide](https://game.ci/docs/github/activation/).

1. Open Unity Hub and sign in to the Unity account you want to use for CI.
2. Open **Settings/Preferences → Licenses**. Select **Add** and complete **Get a free Personal license**. Even if Hub already displays a license, GameCI recommends completing the Add flow so the local license file is present.
3. On Windows, find the resulting `Unity_lic.ulf` at `C:\ProgramData\Unity\Unity_lic.ulf`. `C:\ProgramData` is hidden by default; in File Explorer, enter `%ProgramData%\Unity` in the address bar. If the file is absent, re-check that the Hub Add/activation flow completed successfully.
4. In GitHub, open the repository's **Settings → Secrets and variables → Actions → New repository secret**. Add these three secrets:

   | Secret name | Value |
   |---|---|
   | `UNITY_LICENSE` | The **entire contents** of `Unity_lic.ulf` (open the file in a text editor, copy all, paste as the secret value). |
   | `UNITY_EMAIL` | Email address of the Unity account used in Unity Hub. |
   | `UNITY_PASSWORD` | Password for that Unity account. |

Do not commit the `.ulf` file or put license contents or account credentials in the repository, issues, pull requests, or chat. GitHub does not show secret values after they have been saved. If the license changes or expires, update `UNITY_LICENSE` with the new file contents.

After all three secrets are saved, open the failed Unity CI run for PR #75 and choose **Re-run jobs**. The Unity job checks the secret values are present before invoking GameCI. The current workflow will fail this preflight clearly if any are missing; it will not silently skip Unity validation or report a successful Unity job.

The regular .NET job continues to run independently. GitHub does not expose repository secrets to workflows triggered from fork pull requests, so those Unity jobs fail their preflight until run from a trusted branch/repository context.

If your Unity license is not Personal (for example, a qualifying paid license or a floating license server), do not use the Personal steps above; follow the corresponding [GameCI activation instructions](https://game.ci/docs/github/activation/) and update the workflow to match.
