$workspaceRoot = Split-Path -Parent $PSScriptRoot
$portableGitPath = Join-Path $workspaceRoot '.tools\PortableGit\cmd'
$graphiteCommand = Join-Path $env:APPDATA 'npm\gt.cmd'

if (-not (Test-Path -LiteralPath (Join-Path $portableGitPath 'git.exe'))) {
    throw "Workspace PortableGit is missing: $portableGitPath"
}

if (-not (Test-Path -LiteralPath $graphiteCommand)) {
    throw "Graphite CLI is missing. Install @withgraphite/graphite-cli first."
}

# 로그인은 사람이 한 번 해야 한다. 안 해 두면 `submit` 만 알 수 없는 이유로 실패하므로 미리 말해 준다.
if (-not (Test-Path -LiteralPath (Join-Path $env:USERPROFILE '.graphite_user_config'))) {
    if ($args -contains 'submit') {
        throw "Graphite 에 로그인하지 않았습니다. 먼저: gt auth --token <graphite.dev 토큰>"
    }

    Write-Warning 'Graphite 에 로그인되어 있지 않습니다. 로컬 스택 작업은 되지만 submit(PR 올리기)은 막힙니다 — gt auth --token <토큰>'
}

$env:PATH = "$portableGitPath;$env:PATH"
& $graphiteCommand @args
exit $LASTEXITCODE
