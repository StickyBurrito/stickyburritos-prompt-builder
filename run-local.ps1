$ErrorActionPreference = 'Stop'
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundledPython = 'C:\Users\stick\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$python = if (Test-Path -LiteralPath $bundledPython) { $bundledPython } else { 'python' }
Start-Process 'http://localhost:8765'
& $python (Join-Path $appDir 'local_server.py')
