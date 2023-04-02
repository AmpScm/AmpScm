@echo off
if "%1" == "" (
  echo "Add /p:version:1.2.3 like argument"
  exit /b 1
)
msbuild /v:m /m /p:Configuration=Release /t:clean /t:pack %*