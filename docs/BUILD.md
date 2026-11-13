# Local build layout

The repository is intended to be cloned to `D:\WindowsProjects\Nutriora` on Windows. A self-hosted Windows x64 runner builds from the GitHub Actions checkout and writes outputs to:

- `D:\WindowsProjects\Nutriora\LatestBuild` — latest publish output
- `D:\WindowsProjects\Nutriora\LatestBuild-Ready` — ready-to-run copy
- `D:\WindowsProjects\Nutriora\LatestInstaller` — reserved for future installer

The runner must have .NET 10 SDK, write access to drive D:, and labels `self-hosted`, `Windows`, `X64`. No GitHub build artifact is uploaded. An installer is not yet created. The build output directories are ignored by Git.
