<#
.SYNOPSIS
    Authenticode-signs the LiteSuite.Common DLLs of the build output and verifies them.

.DESCRIPTION
    Signs every LiteSuite.Common.dll under -Dir (one per target framework) with signtool from
    the latest Windows 10/11 SDK, SHA-256 file digest and an RFC 3161 timestamp, then checks each
    signature with "signtool verify /pa". The certificate password is read from the environment,
    never passed on the command line of the caller nor written to the log.

    The LiteSuite publisher certificate is self-signed: its public part is trusted on the runner
    only while verifying, then removed.

    Used by .github/workflows/build.yml before "dotnet pack --no-build".
#>
param(
    [Parameter(Mandatory = $true)] [string] $Dir,
    [Parameter(Mandatory = $true)] [string] $CertPath,
    [string] $PasswordEnvVar = 'CERTIFICATE_PASSWORD',
    [string] $TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'

$signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
if (-not $signtool) { throw "signtool.exe not found in Windows SDK" }
Write-Host "Using signtool: $($signtool.FullName)"

$password = [Environment]::GetEnvironmentVariable($PasswordEnvVar)

$files = @(Get-ChildItem -Path $Dir -Recurse -File -Filter 'LiteSuite.Common.dll' | ForEach-Object FullName)
if ($files.Count -eq 0) { throw "LiteSuite.Common.dll not found under $Dir" }

$pfxCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertPath, [string]$password)
$trustedHere = $false
if ($pfxCert.Subject -eq $pfxCert.Issuer -and -not (Test-Path "Cert:\LocalMachine\Root\$($pfxCert.Thumbprint)")) {
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
    $rootStore.Open('ReadWrite')
    $rootStore.Add((New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(, $pfxCert.RawData)))
    $rootStore.Close()
    $trustedHere = $true
    Write-Host "Self-signed certificate $($pfxCert.Thumbprint): trusted on this runner for the verification only"
}
try {
    foreach ($file in $files) {
        Write-Host "Signing: $file"
        $signArgs = @('sign', '/f', $CertPath)
        if ($password) { $signArgs += @('/p', $password) }
        $signArgs += @('/tr', $TimestampUrl, '/td', 'sha256', '/fd', 'sha256', $file)
        & $signtool.FullName @signArgs
        if ($LASTEXITCODE -ne 0) { throw "Failed to sign $file" }

        & $signtool.FullName verify /pa $file
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file" }
    }
    Write-Host "Signed and verified $($files.Count) file(s)."
} finally {
    if ($trustedHere) {
        Remove-Item "Cert:\LocalMachine\Root\$($pfxCert.Thumbprint)" -ErrorAction SilentlyContinue
    }
    $pfxCert.Dispose()
}
