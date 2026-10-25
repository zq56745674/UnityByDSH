<#
.SYNOPSIS
  一键快速回归（roadmap A5 / C3）：静态哨兵 / 成就核心 / 无尽冒烟。

.DESCRIPTION
  Editor 已经在跑 → 逐条走 `unity command eval`，秒级返回；
  Editor 没开 或 传 -Batch → 退回 batchmode，重新打开工程再跑（分钟级）：
      unity run <Project> -- -executeMethod AutoBattle.EditorTools.QuickRegression.RunAllQuickBatch
      （-quit 由 CLI 管理，不要写在 -- 之后；RunAllQuickBatch 自己会 Exit）

  任何一条输出里出现 FAIL 或调用异常，脚本退出码为 1；全过为 0。
  这条路径不依赖 Play 模式，也不写任何工程文件，可以在提交前随手跑。

.EXAMPLE
  pwsh -File tools/quick-regression.ps1

.EXAMPLE
  pwsh -File tools/quick-regression.ps1 -Batch
#>
[CmdletBinding()]
param(
    [string]$Project = (Join-Path $PSScriptRoot '..\HelloWorldProject'),
    [switch]$Batch,
    [int]$EvalTimeoutMs = 180000
)

$ErrorActionPreference = 'Stop'

$checks = @(
    @{ Name = '静态哨兵'; Code = 'return AutoBattle.EditorTools.BalanceRegression.RunStaticOnly();' },
    @{ Name = '成就核心'; Code = 'return AutoBattle.EditorTools.AchievementRegression.RunCore();' },
    @{ Name = '无尽冒烟'; Code = 'return AutoBattle.EditorTools.EndlessPacing.Run(2, 40);' }
)

function Test-EditorReady {
    try {
        $json = unity status --format json 2>$null | Out-String | ConvertFrom-Json
        return @($json.data.instances | Where-Object { $_.state -eq 'ready' }).Count -gt 0
    }
    catch {
        return $false
    }
}

function Get-FirstLine {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return '(无输出)' }
    $line = ($Text -split "`n" | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -First 1)
    if ($null -eq $line) { return '(无输出)' }
    if ($line.Length -gt 160) { return $line.Substring(0, 160) + '…' }
    return $line
}

$Project = [System.IO.Path]::GetFullPath($Project)
$results = @()
$useBatch = $Batch -or -not (Test-EditorReady)

if ($useBatch) {
    Write-Host "[quick-regression] Editor 未运行（或指定了 -Batch），走 batchmode：$Project" -ForegroundColor Cyan
    & unity run $Project -- -executeMethod AutoBattle.EditorTools.QuickRegression.RunAllQuickBatch
    $code = $LASTEXITCODE
    Write-Host "[quick-regression] batchmode 退出码：$code"
    exit $code
}

Write-Host "[quick-regression] Editor 已连接，逐条 eval：$Project" -ForegroundColor Cyan

foreach ($check in $checks) {
    $raw = unity command --result-only eval -- --code $check.Code --timeout $EvalTimeoutMs 2>&1 | Out-String
    $ok = $false
    $detail = $raw.Trim()

    try {
        $obj = $raw | ConvertFrom-Json
        if ($null -ne $obj.result) {
            $detail = [string]$obj.result
            $ok = $detail -notmatch 'FAIL'
        }
    }
    catch {
        $ok = $false
        if ($detail.Length -gt 400) { $detail = $detail.Substring(0, 400) }
    }

    $results += [pscustomobject]@{
        Name   = $check.Name
        Passed = $ok
        Detail = Get-FirstLine $detail
    }
}

Write-Host ''
Write-Host ('{0,-6} {1,-10} {2}' -f '结果', '步骤', '摘要')
Write-Host ('{0,-6} {1,-10} {2}' -f '------', '----------', '----')
foreach ($r in $results) {
    $tag = 'FAIL'
    $color = 'Red'
    if ($r.Passed) {
        $tag = 'PASS'
        $color = 'Green'
    }
    Write-Host ('{0,-6} {1,-10} {2}' -f $tag, $r.Name, $r.Detail) -ForegroundColor $color
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ''
if ($failed -gt 0) {
    Write-Host "[quick-regression] $failed / $($results.Count) 条失败" -ForegroundColor Red
    exit 1
}

Write-Host "[quick-regression] $($results.Count) / $($results.Count) 条通过" -ForegroundColor Green
exit 0
