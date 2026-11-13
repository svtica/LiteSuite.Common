# LiteSuite.Common

**Components shared by the LiteSuite tools** (LiteDeploy, LiteTask): PsExec command lines,
Authenticode checks, credentials, Mark of the Web, process failure messages and parameter strings.

- Targets **.NET Framework 4.8** (LiteDeploy) and **.NET 10** (`net10.0-windows`, LiteTask), C# 7.3.
- No dependency on the application that uses it, no third-party package.
- `LiteSuite.Common.dll` ships next to the executable of each tool and is signed with it.

> **Status**: moved from the LiteDeploy repository (`src/LiteSuite.Common`), where it was created
> and tested. LiteDeploy and LiteTask switch to the NuGet package next.

## Contents

| Type | Purpose |
|---|---|
| `PsExecCommandLine` | PsExec arguments: quoting (CommandLineToArgvW rules), credentials, executable and batch commands, progress line recognition |
| `AuthenticodeVerifier`, `AuthenticodeResult`, `AuthenticodeStatus` | Authenticode check of a file (WinVerifyTrust) and signer thumbprint |
| `UpdateSignaturePolicy`, `UpdateSignatureVerdict` | Accept or refuse an update package or a script from its signature: pinned LiteSuite publisher, or the signer of the running tool |
| `CredentialStore`, `StoredCredential` | Accounts in the Windows Credential Manager of the user (target `LiteSuite:<name>`), shared by LiteDeploy and LiteTask; protected by Windows (DPAPI), no custom encryption |
| `MarkOfTheWeb` | Removes the `Zone.Identifier` stream of a downloaded file (like `Unblock-File`) |
| `ProcessFailureFormatter` | Failure message of a child process: exit code, duration, stderr, end of stdout |
| `ParameterParser` | `key=value` and `-Name Value` parameter strings |

## Publisher certificate

The LiteSuite releases are signed with a self-signed code signing certificate (`CN=LiteSuite`).
The tools pin its SHA-1 thumbprint:

```
D21B5D39EA4C7BDC69C94C86FC4602E8A1CA15B4
```

`AuthenticodeVerifier` reports a valid signature whose root is not trusted on the computer as
`ValidUntrustedRoot`: the content is verified, and the identity comes from the pinned thumbprint.
A file that was modified, or signed by anyone else, is always refused.

## Layout

| Path | Content |
|---|---|
| `src/LiteSuite.Common/` | The library (`LiteSuite.Common.csproj`, `net48;net10.0-windows`) |
| `tests/LiteSuite.Common.Tests/` | MSTest tests, run on both frameworks |
| `tools/ci/Sign-Binaries.ps1` | Signs and verifies the DLLs (CI) |
| `bump-version.ps1` | Raises the version, commits and tags it (Release workflow) |

## Build and test

Windows, .NET 10 SDK (it also builds the `net48` target):

```powershell
dotnet build LiteSuite.Common.sln -c Release
```

```powershell
dotnet test tests\LiteSuite.Common.Tests -c Release
```

## Using the NuGet package

The package `LiteSuite.Common` is published to GitHub Packages and attached to each
[release](https://github.com/svtica/LiteSuite.Common/releases) (`.nupkg`).

```xml
<PackageReference Include="LiteSuite.Common" Version="1.0.0" />
```

GitHub Packages asks for a token even for a public package. In the consumer repository, a
`nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="LiteSuite" value="https://nuget.pkg.github.com/svtica/index.json" />
  </packageSources>
</configuration>
```

- **CI** (GitHub Actions): `GITHUB_TOKEN` with `packages: read`, once the package settings
  (*Package settings > Manage Actions access*) give that repository read access.
- **Workstation**: a personal access token (classic) with `read:packages` only, added with
  `dotnet nuget update source LiteSuite -u <user> -p <token> --store-password-in-clear-text`,
  or from the `.nupkg` of a release placed in a local folder feed. Never commit the token.

`LiteSuite.Common.dll` must ship next to the executable that uses it.

## Releases

*Actions > Release > Run workflow* (`patch` by default), as in LitePM, LiteDeploy and LiteTask:
`bump-version.ps1` raises the version (1.0.0 -> 1.0.1), commits and tags it on `main`, then the
build tests, signs the DLLs, packs and publishes the package. Locally:
`.\bump-version.ps1 -Bump patch`.

## Conventions

- Namespace `LiteSuite`, one public type per file, standard file header (see the LiteDeploy
  `docs/conventions.md`).
- Behavior shared by several tools only: code that depends on the settings, logger or user
  interface of one tool stays in that tool.
- Never a secret, a password or site-specific data (computer names, servers, paths) in the code,
  the tests or the logs: this repository is public.

## License

This software is released under [The Unlicense](LICENSE) - public domain.


## 🌟 Part of LiteSuite

This library is part of **LiteSuite** - a comprehensive collection of lightweight Windows administration tools.

### Tools in the Suite:
- **[LiteTask](https://github.com/svtica/LiteTask)** - Advanced Task Scheduler Alternative
- **[LitePM](https://github.com/svtica/LitePM)** - Process Manager with System Monitoring
- **[LiteDeploy](https://github.com/svtica/LiteDeploy)** - Network Deployment and Management
- **[LiteRun](https://github.com/svtica/LiteRun)** - Remote Command Execution Utility (archived)
- **[LiteSrv](https://github.com/svtica/LiteSrv)** - Windows Service Wrapper (archived)
