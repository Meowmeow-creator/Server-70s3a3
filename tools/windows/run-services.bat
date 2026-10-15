@echo off
rem Detached service runner for local play. Gives each service its own console
rem (start) and rotates the evidence logs first: the channel opens its evidence
rem log with CreateNew and crashes if the file already exists.
setlocal
set DNF70_TEST_ONE_HIT_KILL=1
set ROOT=D:\70S3A3
set BIN=%ROOT%\Server-70s3a3\build\stable
set RUN=%ROOT%\local-run
cd /d "%RUN%"

taskkill /f /im DNF70-Channel-FG.exe >nul 2>&1
taskkill /f /im DNF70-Entrance-FG.exe >nul 2>&1
timeout /t 1 /nobreak >nul

if exist "%RUN%\logs\channel-evidence.log" del /q "%RUN%\logs\channel-evidence.log"
if exist "%RUN%\logs\channel-evidence.log.*.bin" del /q "%RUN%\logs\channel-evidence.log.*.bin"
if exist "%RUN%\logs\entrance.log" del /q "%RUN%\logs\entrance.log"
if exist "%RUN%\logs\entrance.log.*.bin" del /q "%RUN%\logs\entrance.log.*.bin"

start "DNF70-Channel-FG" "%BIN%\DNF70-Channel-FG.exe" "%RUN%\logs\channel-evidence.log" --batch-store "%RUN%\DNF70-batch-test-FG" --interactive
start "DNF70-Entrance-FG" "%BIN%\DNF70-Entrance-FG.exe" "%RUN%\logs\entrance.log" --script-response "%ROOT%\Server-70s3a3\data\DNF70-channel-info-CG.etc" 10 --test-port 17001 --interactive
endlocal
