<#
.SYNOPSIS
  跑 HelloWorldProject 的 EditMode NUnit 测试（roadmap A5）。

.DESCRIPTION
  等价于：
      unity test <Project> --mode EditMode --output _art/tmp-verify/test-results.xml

  测试程序集是 Assets/Tests/AutoBattle.Tests.asmdef（Unity Test Framework 1.8.0），
  所以这里必须让 Unity 以 batchmode 独占工程：Editor 正开着时直接退出，避免
  两个进程抢 ProjectSettings 锁。想边开 Editor 边跑，用 Window > General > Test Runner。

  结果以 NUnit XML 为准（unity test 的退出码在部分版本上不可靠）：
  只有 test-run/@result = Passed 才 exit 0；没有产出报告也按失败处理。

.EXAMPLE
  pwsh tools/run-editmode-tests.ps1

.EXAMPLE
  pwsh tools/run-editmode-tests.ps1 -Filter AutoBattle.Tests.ConfigAndFormulaTests
#>
[CmdletBinding()]
param(
    [string]$Project = (Join-Path $PSScriptRoot '..\HelloWorldProject'),
    [string]$Output = '_art/tmp-verify/test-results.xml',
    [string]$Filter = '',
    [int]$TimeoutSeconds = 480
)

$ErrorActionPreference = 'Stop'

$Project = [System.IO.Path]::GetFullPath($Project)
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($Output)) {
    $Output = Join-Path $repoRoot $Output
}
$Output = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null

function Test-EditorReady {
    try {
        $json = unity status --format json 2>$null | Out-String | ConvertFrom-Json
        return @($json.data.instances | Where-Object { $_.state -eq 'ready' }).Count -gt 0
    }
    catch {
        return $false
    }
}

if (Test-EditorReady) {
    Write-Host '[editmode-tests] Editor 正开着，batchmode 测试需要独占工程。' -ForegroundColor Yellow
    Write-Host '  请先关闭 Unity，或改用 Window > General > Test Runner 直接跑；quick-regression 的 eval 路径不受影响。'
    exit 2
}

Remove-Item $Output -ErrorAction SilentlyContinue
$unityArgs = @('test', $Project, '--mode', 'EditMode', '--output', $Output, '--timeout', $TimeoutSeconds)
if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $unityArgs += @('--filter', $Filter)
}

Write-Host "[editmode-tests] unity test $Project（EditMode）" -ForegroundColor Cyan
& unity @unityArgs
$code = $LASTEXITCODE
Write-Host "[editmode-tests] unity test 退出码：$code"

if (-not (Test-Path $Output)) {
    Write-Host "[editmode-tests] 没有生成测试报告：$Output" -ForegroundColor Red
    exit 1
}

[xml]$xml = Get-Content -Path $Output -Raw
$run = $xml.'test-run'
if ($null -eq $run) {
    Write-Host '[editmode-tests] 报告里没有 test-run 节点，无法判定结果' -ForegroundColor Red
    exit 1
}

$passed = [int]$run.passed
$failed = [int]$run.failed
$skipped = [int]$run.skipped
$total = [int]$run.total
$duration = 0.0
[void][double]::TryParse([string]$run.duration, [System.Globalization.NumberStyles]::Float,
    [System.Globalization.CultureInfo]::InvariantCulture, [ref]$duration)

Write-Host ''
Write-Host ("{0,-6} {1}" -f '结果', 'NUnit EditMode')
Write-Host ("{0,-6} {1}" -f '------', '--------------')
$color = if ($run.result -eq 'Passed') { 'Green' } else { 'Red' }
$summary = "{0}/{1} 通过，失败 {2}，跳过 {3}，用时 {4:0.00}s" -f $passed, $total, $failed, $skipped, $duration
Write-Host ("{0,-6} {1}" -f $run.result, $summary) -ForegroundColor $color
Write-Host "[editmode-tests] 报告：$Output"

if ($run.result -ne 'Passed' -or $failed -gt 0) {
    exit 1
}
exit 0
