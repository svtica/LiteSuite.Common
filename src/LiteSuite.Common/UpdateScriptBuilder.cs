// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateScriptBuilder.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Writes the PowerShell script that replaces the files of a tool once
//          it has exited (generalized from LiteTask's UpdateManager): waits for
//          the process, stops the services of the tool, copies the new files
//          without deleting anything and without overwriting the files of the
//          user, restores the replaced files when a copy fails, starts the
//          services again and restarts the tool (without elevation when only
//          the copy needed it). The script is ASCII only: every value is
//          written as a PowerShell expression, non-ASCII characters as [char].
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LiteSuite
{
    public static class UpdateScriptBuilder
    {
        /// <summary>The script for these options. Throws ArgumentException when one is missing or invalid.</summary>
        public static string Build(UpdateScriptOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            Require(options.ApplicationDirectory, "ApplicationDirectory");
            Require(options.SourceDirectory, "SourceDirectory");
            Require(options.WorkDirectory, "WorkDirectory");
            Require(options.LogPath, "LogPath");
            Require(options.ExecutableName, "ExecutableName");
            if (options.ExecutableName != Path.GetFileName(options.ExecutableName))
                throw new ArgumentException("ExecutableName must be a file name.");
            if (options.ProcessId <= 0) throw new ArgumentException("ProcessId must be positive.");

            string name = string.IsNullOrWhiteSpace(options.ApplicationName)
                ? Path.GetFileNameWithoutExtension(options.ExecutableName) : options.ApplicationName;
            string script = Template
                .Replace("{{Name}}", AsciiComment(name))
                .Replace("{{ProcessId}}", options.ProcessId.ToString(CultureInfo.InvariantCulture))
                .Replace("{{AppDir}}", Literal(options.ApplicationDirectory))
                .Replace("{{SourceDir}}", Literal(options.SourceDirectory))
                .Replace("{{ExeName}}", Literal(options.ExecutableName))
                .Replace("{{RestartArguments}}", Literal(options.RestartArguments ?? ""))
                .Replace("{{Restart}}", options.Restart ? "$true" : "$false")
                .Replace("{{Elevate}}", options.Elevate ? "$true" : "$false")
                .Replace("{{Preserve}}", List((options.PreservePatterns ?? new List<string>()).Select(p => p.Replace('/', '\\'))))
                .Replace("{{Services}}", List(options.ServiceNames ?? new List<string>()))
                .Replace("{{WorkDir}}", Literal(options.WorkDirectory))
                .Replace("{{LogPath}}", Literal(options.LogPath));
            return script.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        /// <summary>Writes the script (ASCII) to <paramref name="path"/>.</summary>
        public static void Write(UpdateScriptOptions options, string path)
        {
            File.WriteAllText(path, Build(options), Encoding.ASCII);
        }

        /// <summary>
        /// A PowerShell expression of the string, ASCII only: a single-quoted string, with
        /// non-ASCII and control characters added as [char]0xNNNN ('' + [char]0x00E9 + 'x').
        /// </summary>
        public static string Literal(string value)
        {
            if (string.IsNullOrEmpty(value)) return "''";
            List<string> parts = new List<string>();
            StringBuilder plain = new StringBuilder();
            foreach (char c in value)
            {
                if (c >= 0x20 && c < 0x7F)
                {
                    plain.Append(c == '\'' ? "''" : c.ToString());
                    continue;
                }
                if (plain.Length > 0) { parts.Add("'" + plain + "'"); plain.Clear(); }
                parts.Add("[char]0x" + ((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            if (plain.Length > 0) parts.Add("'" + plain + "'");
            if (parts.Count == 1 && parts[0].StartsWith("'")) return parts[0];
            // Starts with a string so that + concatenates ([char] + [char] would add numbers)
            if (!parts[0].StartsWith("'")) parts.Insert(0, "''");
            return "(" + string.Join(" + ", parts) + ")";
        }

        static string List(IEnumerable<string> values)
        {
            return string.Join(", ", values.Where(v => !string.IsNullOrEmpty(v)).Select(Literal));
        }

        static string AsciiComment(string value)
        {
            return new string(value.Select(c => c >= 0x20 && c < 0x7F ? c : '?').ToArray());
        }

        static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
        }

        const string Template = @"# {{Name}} update script, generated by LiteSuite.Common (UpdateScriptBuilder). ASCII only.
# Waits for {{Name}} to exit, stops its services, copies the new files (the files of the user
# matching $preserve are kept when they exist), starts the services again and restarts
# {{Name}}. When a file cannot be copied, the files already replaced are restored.
param([switch]$ApplyOnly)

$ErrorActionPreference = 'Stop'
$processId = {{ProcessId}}
$appDir = {{AppDir}}
$sourceDir = {{SourceDir}}
$exeName = {{ExeName}}
$restartArguments = {{RestartArguments}}
$restart = {{Restart}}
$elevate = {{Elevate}}
$preserve = @({{Preserve}})
$services = @({{Services}})
$workDir = {{WorkDir}}
$logPath = {{LogPath}}
$exePath = Join-Path $appDir $exeName
$script:stoppedServices = @()
$script:updateOk = $false

function Write-Log([string]$message) {
    try { Add-Content -LiteralPath $logPath -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $message) -Encoding UTF8 } catch { }
}

function Wait-Application {
    try {
        $process = Get-Process -Id $processId -ErrorAction Stop
        if (-not $process.WaitForExit(60000)) {
            if ($process.Path -eq $exePath) {
                Write-Log ""Process $processId still running after 60 s: stopping it""
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
            }
        }
    } catch { }
}

function Stop-ApplicationServices {
    foreach ($name in $services) {
        $service = Get-Service -Name $name -ErrorAction SilentlyContinue
        if ($service -and $service.Status -ne 'Stopped') {
            Write-Log ""Stopping service $name""
            $script:stoppedServices += $name
            try {
                Stop-Service -Name $name -Force -ErrorAction Stop
                $service.WaitForStatus('Stopped', (New-TimeSpan -Seconds 60))
            } catch { Write-Log ('Cannot stop service ' + $name + ': ' + $_.Exception.Message) }
        }
    }
}

function Start-ApplicationServices {
    foreach ($name in $script:stoppedServices) {
        Write-Log ""Starting service $name""
        try { Start-Service -Name $name -ErrorAction Stop } catch { Write-Log ('Cannot start service ' + $name + ': ' + $_.Exception.Message) }
    }
}

function Wait-OtherInstances {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $others = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { try { $_.Path -and ($_.Path -eq $exePath) } catch { $false } })
        if ($others.Count -eq 0) { return }
        Start-Sleep -Milliseconds 500
    }
    Write-Log ""Other instances of $exeName are still running""
}

function Test-Preserved([string]$relative) {
    foreach ($pattern in $preserve) {
        if ($relative -like $pattern) { return $true }
    }
    return $false
}

function Copy-FileWithRetry([string]$source, [string]$target) {
    for ($i = 1; $i -le 10; $i++) {
        try { [System.IO.File]::Copy($source, $target, $true); return }
        catch { if ($i -eq 10) { throw }; Start-Sleep -Seconds 1 }
    }
}

function Invoke-Update {
    Wait-Application
    Stop-ApplicationServices
    Wait-OtherInstances
    $backupDir = Join-Path $workDir 'backup'
    $replaced = New-Object System.Collections.Generic.List[string]
    $created = New-Object System.Collections.Generic.List[string]
    try {
        $sourceRoot = [System.IO.Path]::GetFullPath($sourceDir).TrimEnd('\')
        $files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force | Where-Object { -not $_.PSIsContainer })
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($sourceRoot.Length + 1)
            $target = Join-Path $appDir $relative
            $exists = [System.IO.File]::Exists($target)
            if ($exists -and (Test-Preserved $relative)) { Write-Log ""Kept $relative""; continue }
            [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target))
            if ($exists) {
                $backup = Join-Path $backupDir $relative
                [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($backup))
                [System.IO.File]::Copy($target, $backup, $true)
                $replaced.Add($relative)
            } else {
                $created.Add($relative)
            }
            Copy-FileWithRetry $file.FullName $target
        }
        $script:updateOk = $true
        Write-Log ('Files replaced: ' + $replaced.Count + ', added: ' + $created.Count)
    } catch {
        Write-Log ('Update failed: ' + $_.Exception.Message + ' Restoring the previous files.')
        foreach ($relative in $replaced) {
            try { [System.IO.File]::Copy((Join-Path $backupDir $relative), (Join-Path $appDir $relative), $true) }
            catch { Write-Log ('Cannot restore ' + $relative + ': ' + $_.Exception.Message) }
        }
        foreach ($relative in $created) {
            try { [System.IO.File]::Delete((Join-Path $appDir $relative)) } catch { }
        }
    }
    Start-ApplicationServices
}

if ($ApplyOnly) {
    Write-Log 'Elevated copy started'
    Invoke-Update
    if ($script:updateOk) { exit 0 } else { exit 1 }
}

Write-Log ""Update of $exeName started""
if ($elevate) {
    Wait-Application
    try {
        $arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""' + $PSCommandPath + '"" -ApplyOnly'
        $elevated = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        Write-Log ('Elevated copy finished with code ' + $elevated.ExitCode)
    } catch {
        Write-Log ('Elevation refused or failed: ' + $_.Exception.Message)
    }
} else {
    Invoke-Update
}

if ($restart) {
    Write-Log ""Starting $exePath""
    try {
        if ($restartArguments) { Start-Process -FilePath $exePath -ArgumentList $restartArguments -WorkingDirectory $appDir }
        else { Start-Process -FilePath $exePath -WorkingDirectory $appDir }
    } catch { Write-Log ('Cannot restart: ' + $_.Exception.Message) }
}

Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Log 'Done'
";
    }
}
