# Unity Pipeline 客户端 helper。
#
# 用法:
#   pwsh -File Tools/pipe.ps1 -Compile                  # 一条龙: recompile → 轮询 → 报编译错误
#   pwsh -File Tools/pipe.ps1 editor_status
#   pwsh -File Tools/pipe.ps1 console -CmdArgs @{ level = 'Error'; tail = 50 }
#   pwsh -File Tools/pipe.ps1 menu -CmdArgs @{ path = 'Tools/Drama/...' }
#   pwsh -File Tools/pipe.ps1 -ListCommands             # 列出全部可用命令
#
# 端口和令牌从 Library/Pipeline/.unity-pipeline-port 自动读取。
# 文档: https://docs.unity3d.com/Packages/com.unity.pipeline@0.4/manual/index.html
#
# 注意: 参数名不能叫 $Args —— 那是 PowerShell 保留的自动变量。

param(
    [Parameter(Position = 0)][string]$Command,
    [hashtable]$CmdArgs = @{},
    [switch]$Compile,
    [switch]$ListCommands,
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [int]$TimeoutSec = 60
)

$ErrorActionPreference = 'Stop'

$descriptor = Join-Path $ProjectPath "Library\Pipeline\.unity-pipeline-port"
if (-not (Test-Path $descriptor)) {
    Write-Host "找不到端口描述文件: $descriptor" -ForegroundColor Red
    Write-Host "Unity Editor 没开，或 Pipeline 服务没启动（见 Assets/Settings/Pipeline/EditorPipelineManager）。" -ForegroundColor Red
    exit 2
}

$info = Get-Content $descriptor -Raw | ConvertFrom-Json
$baseUri = "http://127.0.0.1:$($info.port)"
$headers = @{ Authorization = "Bearer $($info.evalToken)" }

# Unity 在文件刚改动时会刷一堆导入噪音，和编译错误无关，过滤掉
$noisePatterns = @(
    'Build asset version error',
    'in SourceAssetDB has modification time'
)

function Invoke-Pipe {
    param([string]$Cmd, [hashtable]$A = @{}, [int]$Retries = 3)

    $body = @{ command = $Cmd }
    foreach ($k in $A.Keys) { $body[$k] = $A[$k] }
    $json = $body | ConvertTo-Json -Depth 10 -Compress

    for ($i = 0; $i -lt $Retries; $i++) {
        try {
            return Invoke-RestMethod -Uri "$baseUri/api/exec" -Method POST `
                -Headers $headers -ContentType "application/json" `
                -Body $json -TimeoutSec $TimeoutSec
        }
        catch {
            # 域重载期间 HTTP 会短暂断开，重试
            if ($i -eq $Retries - 1) { throw }
            Start-Sleep -Milliseconds 900
        }
    }
}

# 有些命令的 result 是 JSON 字符串而不是对象（例如 recompile_status），统一解一层
function Get-Result($response) {
    $r = $response.result
    if ($r -is [string] -and $r.TrimStart().StartsWith('{')) {
        return $r | ConvertFrom-Json
    }
    return $r
}

function Invoke-Compile {
    Write-Host "→ clear_console" -ForegroundColor DarkGray
    [void](Invoke-Pipe 'clear_console')

    Write-Host "→ recompile" -ForegroundColor DarkGray
    $r = Invoke-Pipe 'recompile'
    if (-not $r.success) {
        Write-Host "recompile 调用失败: $($r.error)" -ForegroundColor Red
        return 1
    }

    # 轮询 recompile_status。result 是 JSON 字符串: { status, failed, errors[] }
    $deadline = [datetime]::UtcNow.AddMinutes(6)
    $lastState = ''
    $final = $null

    while ([datetime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 1200
        try { $s = Get-Result (Invoke-Pipe 'recompile_status' @{} 6) } catch { continue }
        if ($null -eq $s) { continue }

        if ($s.status -ne $lastState) {
            Write-Host "   status: $($s.status)" -ForegroundColor DarkGray
            $lastState = $s.status
        }

        if ($s.status -in @('completed', 'up_to_date')) { $final = $s; break }
    }

    if ($null -eq $final) {
        Write-Host "`n⏱ 轮询超时，最后状态: '$lastState'" -ForegroundColor Yellow
        return 1
    }

    # 首选 recompile_status 自带的 errors，它已经是干净的编译错误
    $errors = @($final.errors | Where-Object { $_ })

    # 兜底：从 console 抓，并过滤导入噪音
    if ($errors.Count -eq 0 -and $final.failed) {
        $logs = Get-Result (Invoke-Pipe 'console' @{ level = 'Error'; tail = 300 })
        $errors = @($logs.entries | Where-Object {
            $m = $_.message
            $m -and -not ($noisePatterns | Where-Object { $m -like "*$_*" })
        } | ForEach-Object { $_.message })
    }

    if (-not $final.failed -and $errors.Count -eq 0) {
        Write-Host "`n✅ 编译通过（status=$($final.status)），无错误。" -ForegroundColor Green
        return 0
    }

    Write-Host "`n❌ 编译失败，$($errors.Count) 条错误:" -ForegroundColor Red
    foreach ($e in $errors) {
        $text = if ($e -is [string]) { $e } else { "$($e.file)($($e.line),$($e.column)): $($e.message)" }
        Write-Host "   $text" -ForegroundColor Red
    }
    return 1
}

if ($Compile) { exit (Invoke-Compile) }

if ($ListCommands) {
    $c = Invoke-RestMethod -Uri "$baseUri/api/commands" -Headers $headers -TimeoutSec $TimeoutSec
    $list = if ($c.commands) { $c.commands } else { $c }
    $list | Sort-Object name | ForEach-Object { "{0,-32} {1}" -f $_.name, $_.description }
    exit 0
}

if (-not $Command) {
    Write-Host "Unity Pipeline  port=$($info.port)  project=$($info.projectName)  unity=$($info.unityVersion)"
    Write-Host "用法: pipe.ps1 <command> [-CmdArgs @{...}]   |   pipe.ps1 -Compile   |   pipe.ps1 -ListCommands"
    exit 0
}

(Invoke-Pipe $Command $CmdArgs) | ConvertTo-Json -Depth 10
