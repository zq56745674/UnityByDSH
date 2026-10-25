#Requires -Version 7.0
<#
.SYNOPSIS
    用 Unity CLI 构建 Windows x64 播放器。

.DESCRIPTION
    全新 clone 后的一条命令：
        pwsh tools/build-windows.ps1
    产物默认写到 HelloWorldProject/Build/Windows/AutoBattle.exe（该目录已 gitignore）。
    走 Unity CLI 原生 build，不需要自定义 BuildPipeline / executeMethod；
    场景列表取自 ProjectSettings/EditorBuildSettings.asset。

    前置条件：unity CLI 与 Editor 6000.6.5f1 已安装（见 README「构建与 CI」）。

.PARAMETER OutputPath
    输出路径。相对路径按 Unity 工程目录（HelloWorldProject/）解析，默认
    Build/Windows/AutoBattle.exe。

.PARAMETER RequireClean
    默认允许工作区有未提交改动（本地开发方便）；加上此开关后要求 git 工作区干净，
    由 Unity 自带的脏工作区保护来决定是否构建。

.PARAMETER TimeoutSeconds
    Unity 构建超时，默认 1800 秒。
#>
[CmdletBinding()]
param(
    [string]$OutputPath = "Build/Windows/AutoBattle.exe",
    [switch]$RequireClean,
    [int]$TimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "HelloWorldProject"
$projectVersion = Join-Path $project "ProjectSettings/ProjectVersion.txt"

if (-not (Test-Path $projectVersion)) {
    throw "找不到 Unity 工程：$project（缺少 ProjectSettings/ProjectVersion.txt）"
}
if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
    throw "找不到 unity CLI。安装：irm https://unity.com/install.ps1 | iex（见 README「构建与 CI」）"
}

if (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $project $OutputPath
}
$outputDir = Split-Path -Parent $OutputPath
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force
}

$unityArgs = @(
    "build", $project,
    "--target", "StandaloneWindows64",
    "--output-path", $OutputPath,
    "--timeout", "$TimeoutSeconds"
)
if (-not $RequireClean) {
    $unityArgs += "--allow-dirty-build"
}

Write-Host "构建 Windows x64 -> $OutputPath"
& unity @unityArgs
if ($LASTEXITCODE -ne 0) {
    throw "unity build 失败（exit $LASTEXITCODE）"
}
if (-not (Test-Path $OutputPath)) {
    throw "构建命令成功但未找到产物：$OutputPath"
}

$exe = Get-Item $OutputPath
Write-Host ("构建完成：{0}（{1:N1} MB）" -f $exe.FullName, ($exe.Length / 1MB))
