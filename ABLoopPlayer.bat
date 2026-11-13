@echo off
setlocal
set "EDGE=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if not exist "%EDGE%" set "EDGE=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
set "HTML=%~dp0ABLoopPlayer.html"
if exist "%EDGE%" (
  start "" "%EDGE%" --app="file:///%HTML:\=/%"
) else (
  start "" "%HTML%"
)
endlocal
