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

    # ★ 参数必须嵌在 "parameters" 里。平铺在顶层会被静默忽略
    #   （命令仍然执行，但所有参数当没传 —— 排查起来很坑）。
    $body = @{ command = $Cmd }
    if ($A.Count -gt 0) { $body['parameters'] = $A }
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

    # ★ 不要用 'recompile' 命令：实测它经常直接返回 up_to_date 而【根本没触发编译】，
    #   于是 recompile_status 报"无错误"，实际代码里有编译错 —— 假绿灯，非常坑。
    #   改成显式调 CompilationPipeline.RequestScriptCompilation()，它一定会真编。
    Write-Host "→ AssetDatabase.Refresh + RequestScriptCompilation" -ForegroundColor DarkGray
    $code = 'UnityEditor.AssetDatabase.Refresh(); ' +
            'UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); ' +
            'return "requested";'
    try { [void](Invoke-Pipe 'eval' @{ code = $code; timeout = 29000 } 3) }
    catch { Write-Host "   （触发时连接中断，通常是域重载，继续）" -ForegroundColor DarkGray }

    # 先等它真的进入 compiling，再等它结束 —— 否则可能在开编之前就误判成功
    $deadline = [datetime]::UtcNow.AddMinutes(8)
    $sawCompiling = $false
    $settled = $false

    while ([datetime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 1500
        try { $st = (Invoke-Pipe 'editor_status' @{} 8).result } catch { continue }
        if ($null -eq $st) { continue }

        if ($st.compiling -or $st.domainReloadInProgress) { $sawCompiling = $true; continue }
        if ($st.status -eq 'ready') {
            # 已就绪；如果从没见过 compiling，多等一轮防止抢跑
            if ($sawCompiling) { $settled = $true; break }
            Start-Sleep -Milliseconds 1500
            try { $st2 = (Invoke-Pipe 'editor_status' @{} 8).result } catch { continue }
            if ($st2 -and -not $st2.compiling -and -not $st2.domainReloadInProgress) { $settled = $true; break }
        }
    }

    if (-not $settled) {
        Write-Host "`n⏱ 等待编译超时" -ForegroundColor Yellow
        return 1
    }

    $final = Get-Result (Invoke-Pipe 'recompile_status' @{} 6)
    if ($null -eq $final) {
        Write-Host "`n⏱ 读不到 recompile_status" -ForegroundColor Yellow
        return 1
    }
    Write-Host "   status: $($final.status)" -ForegroundColor DarkGray

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
