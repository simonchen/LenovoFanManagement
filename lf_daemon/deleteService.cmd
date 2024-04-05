@echo off
@rem Intended to run from the same directory (e.g. bin); build should copy to bin.
sc stop "LenovoFanDaemon"
sc delete "LenovoFanDaemon"
