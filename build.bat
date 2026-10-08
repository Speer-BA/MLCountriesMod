@echo off
cd /d "%~dp0"
if not exist GamePath.props if "%GamePath%"=="" (
  copy GamePath.props.example GamePath.props >nul
  echo Created GamePath.props - open it, set your Broken Arrow folder, then run build.bat again.
  pause
  exit /b 1
)
dotnet build -c Release
if errorlevel 1 (
  echo.
  echo BUILD FAILED - see the errors above.
) else (
  echo.
  echo Done. MLCountriesMod.dll was copied to the game's Mods folder.
)
pause
