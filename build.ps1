param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path $compiler)) { throw '.NET Framework 4.x compiler not found. Install the .NET Framework 4.8 developer tools.' }
    New-Item -ItemType Directory -Force bin,release,assets | Out-Null
    & $compiler /nologo /target:exe /out:bin\IconBuilder.exe /r:System.Drawing.dll tools\IconBuilder.cs
    if ($LASTEXITCODE) { throw 'Icon build failed.' }
    & .\bin\IconBuilder.exe assets
    $references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
    & $compiler /nologo /optimize+ /target:winexe /platform:anycpu /out:release\NLNS.exe /win32icon:assets\nlns.ico /win32manifest:src\app.manifest $references src\*.cs
    if ($LASTEXITCODE) { throw 'Application build failed.' }
    if ($Test) {
        & $compiler /nologo /target:exe /platform:anycpu /win32icon:assets\nlns.ico /out:bin\NLNS.Tests.exe /main:NLNS.Tests $references src\*.cs tests\*.cs
        if ($LASTEXITCODE) { throw 'Test build failed.' }
        & .\bin\NLNS.Tests.exe
        if ($LASTEXITCODE) { throw 'Tests failed.' }
    }
    Write-Output ("NLNS.exe: " + (Get-Item release\NLNS.exe).Length + " bytes")
    Write-Output ("SHA-256: " + (Get-FileHash release\NLNS.exe -Algorithm SHA256).Hash)
} finally { Pop-Location }
