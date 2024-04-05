@echo off
@rem Intended to run from the same directory (e.g. bin); build should copy to bin.
sc create "LenovoFanDaemon" binpath= "%~dp0lf_daemon.exe LenovoFan.exe" displayname= "LenovoFanDaemon" 
sc start "LenovoFanDaemon" 
