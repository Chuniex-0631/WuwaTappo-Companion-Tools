param([switch]$Test)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
foreach ($assembly in @('UIAutomationClient', 'UIAutomationTypes', 'WindowsBase')) {
    $assemblyPath = Get-ChildItem (Join-Path $framework $assembly) -Filter "$assembly.dll" -Recurse |
        Where-Object { $_.FullName -match 'v4.0_' } | Select-Object -First 1 -ExpandProperty FullName
    if (!$assemblyPath) { throw "Missing .NET Framework assembly: $assembly" }
    $references += $assemblyPath
}
$referenceArgs = @($references | ForEach-Object { '/reference:' + $_ })
$shared = @(Get-ChildItem (Join-Path $repoRoot 'src\Shared') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$output = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($appName in @('WuwaTappoToolkit', 'WuwaTappoAutoPilot')) {
    $source = Join-Path $repoRoot "src\$appName"
    & $compiler /nologo /target:winexe /optimize+ /warnaserror+ /platform:anycpu `
        "/out:$output\$appName.exe" "/win32icon:$source\app-icon.ico" `
        "/win32manifest:$source\app.manifest" @referenceArgs @shared "$source\$appName.cs"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $appName" }
    if ($Test) {
        $testOutput = Join-Path $repoRoot "tests\bin\$appName"
        New-Item -ItemType Directory -Force $testOutput | Out-Null
        $testNames = @('AutoPilotControllerTests', 'DesktopLifecycleTests', 'InputDeliveryTests', 'ButtonInteractionTests')
        if ($appName -eq 'WuwaTappoAutoPilot') { $testNames += 'AutoPilotWorkerTests' }
        foreach ($testName in $testNames) {
            & $compiler /nologo /target:exe /optimize+ /warnaserror+ "/main:$testName" `
                "/out:$testOutput\$testName.exe" @referenceArgs @shared "$source\$appName.cs" `
                "$repoRoot\tests\$testName.cs"
            if ($LASTEXITCODE -ne 0) { throw "Test build failed: $testName / $appName" }
            & "$testOutput\$testName.exe"
            if ($LASTEXITCODE -ne 0) { throw "Tests failed: $testName / $appName" }
        }
    }
}
