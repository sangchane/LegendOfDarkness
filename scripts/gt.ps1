$workspaceRoot = Split-Path -Parent $PSScriptRoot
$portableGitPath = Join-Path $workspaceRoot '.tools\PortableGit\cmd'
$graphiteCommand = Join-Path $env:APPDATA 'npm\gt.cmd'

if (-not (Test-Path -LiteralPath (Join-Path $portableGitPath 'git.exe'))) {
    throw "Workspace PortableGit is missing: $portableGitPath"
}

if (-not (Test-Path -LiteralPath $graphiteCommand)) {
    throw "Graphite CLI is missing. Install @withgraphite/graphite-cli first."
}

$env:PATH = "$portableGitPath;$env:PATH"
& $graphiteCommand @args
exit $LASTEXITCODE
