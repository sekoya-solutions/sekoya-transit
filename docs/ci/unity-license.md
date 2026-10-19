# Unity license for GitHub Actions

The Unity workflow requires these repository Actions secrets:

- `UNITY_LICENSE`: contents of the locally activated Unity Personal `.ulf` file.
- `UNITY_EMAIL`: email address for the Unity account that owns the license.
- `UNITY_PASSWORD`: password for that Unity account.

Add them under **Repository Settings → Secrets and variables → Actions**. Do not put license contents or account credentials in the repository, issues, pull requests, or chat.

To get the `.ulf` file, activate the Personal license in Unity Hub. On Windows, it is normally at `C:\ProgramData\Unity\Unity_lic.ulf`. GameCI's [activation guide](https://game.ci/docs/github/activation/) has the current activation steps. If the Unity account uses a paid license or license server instead, follow the matching GameCI activation instructions and update the workflow's credentials accordingly.

The Unity CI job checks all three secret values before invoking GameCI. Missing secrets therefore fail the Unity job explicitly rather than skipping validation or presenting a successful result. The regular .NET job continues to run independently. GitHub does not expose repository secrets to workflows triggered from fork pull requests, so those Unity jobs will fail their preflight until run from a trusted branch/repository context.
