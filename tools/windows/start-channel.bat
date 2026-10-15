@echo off
rem Start (or restart) the local channel service with a fresh evidence log.
rem The channel opens the log and its .bin shards with CreateNew, so leftovers
rem must be removed or the service dies at startup.
setlocal
set DNF70_TEST_ONE_HIT_KILL=1
set ROOT=D:\70S3A3
set RUN=%ROOT%\local-run
set BIN=%ROOT%\Server-70s3a3\build\stable
cd /d "%RUN%"
if exist "%RUN%\logs\channel-evidence.log" del /q "%RUN%\logs\channel-evidence.log"
if exist "%RUN%\logs\channel-evidence.log.*.bin" del /q "%RUN%\logs\channel-evidence.log.*.bin"
start "DNF70-Channel-FG" "%BIN%\DNF70-Channel-FG.exe" "%RUN%\logs\channel-evidence.log" --batch-store "%RUN%\DNF70-batch-test-FG" --interactive
endlocal
