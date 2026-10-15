@echo off
rem Start (or restart) the local entrance service with a fresh log.
setlocal
set ROOT=D:\70S3A3
set RUN=%ROOT%\local-run
set BIN=%ROOT%\Server-70s3a3\build\stable
cd /d "%RUN%"
if exist "%RUN%\logs\entrance.log" del /q "%RUN%\logs\entrance.log"
if exist "%RUN%\logs\entrance.log.*.bin" del /q "%RUN%\logs\entrance.log.*.bin"
start "DNF70-Entrance-FG" "%BIN%\DNF70-Entrance-FG.exe" "%RUN%\logs\entrance.log" --script-response "%ROOT%\Server-70s3a3\data\DNF70-channel-info-CG.etc" 10 --test-port 17001 --interactive
endlocal
