@echo off
setlocal
title Custom Car Saves - Wipe All Data
echo.
echo  Wipe all Custom Car Saves data on this PC.
echo  Does NOT touch game saves / "NFSU2 BROWSER SAVE".
echo  Does NOT delete stock Presets / DYNO SAVES / Templates.
echo.
echo  Will be removed:
echo    Data\ next to this .bat / the exe  (rack, dyno library, liveries)
echo    %%LOCALAPPDATA%%\NFSU2CustomCarSaves
echo    %%TEMP%%\NFSU2CustomCarSaves
echo    legacy %%LOCALAPPDATA%%\NFSU2CarAdder  (if present)
echo    Desktop backups  (if you choose Yes)
echo.
pause

set "HERE=%~dp0"
set "DATA=%HERE%Data"
set "APP=%LOCALAPPDATA%\NFSU2CustomCarSaves"
set "TMPDIR=%TEMP%\NFSU2CustomCarSaves"
set "LEGACY=%LOCALAPPDATA%\NFSU2CarAdder"
set "LEGACYTMP=%TEMP%\NFSU2CarAdder"

if exist "%DATA%" (
  echo Removing %DATA% ...
  rmdir /s /q "%DATA%"
) else (
  echo Skip: %DATA% does not exist
)

if exist "%APP%" (
  echo Removing %APP% ...
  rmdir /s /q "%APP%"
) else (
  echo Skip: %APP% does not exist
)

if exist "%TMPDIR%" (
  echo Removing %TMPDIR% ...
  rmdir /s /q "%TMPDIR%"
) else (
  echo Skip: %TMPDIR% does not exist
)

if exist "%LEGACY%" (
  echo Removing legacy %LEGACY% ...
  rmdir /s /q "%LEGACY%"
)
if exist "%LEGACYTMP%" (
  echo Removing legacy %LEGACYTMP% ...
  rmdir /s /q "%LEGACYTMP%"
)

echo.
choice /M "Also delete Desktop NFSU2CustomCarSaves_Backup_* / NFSU2CarAdder_Backup_* folders"
if errorlevel 2 goto done_backups
if errorlevel 1 (
  for /d %%D in ("%USERPROFILE%\Desktop\NFSU2CustomCarSaves_Backup_*") do (
    echo Removing %%D ...
    rmdir /s /q "%%D"
  )
  for /d %%D in ("%USERPROFILE%\Desktop\NFSU2CarAdder_Backup_*") do (
    echo Removing %%D ...
    rmdir /s /q "%%D"
  )
)
:done_backups

echo.
echo Done. Stock Presets / DYNO SAVES / Templates are still there.
echo.
pause
