# WinGet manifests

The manifests submitted to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) for
`Mun1to.TaskbarGroupsFluent`, one folder per version.

For a new version, publish the GitHub release first (the installer URL has to exist), then copy the
latest folder and change `PackageVersion` in the four files, plus `InstallerUrl`,
`InstallerSha256`, `ReleaseDate` and `ReleaseNotesUrl`. Check it with
`winget validate --manifest <folder>` and open the pull request titled
`New version: Mun1to.TaskbarGroupsFluent version X.Y.Z`.

Keep `AppsAndFeaturesEntries.Publisher` as `Mun1to`: it is what the installer writes to the
registry, and winget uses it to recognise an existing install.
