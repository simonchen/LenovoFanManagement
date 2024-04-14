using DellFanManagement.DellSmbiozBzhLib;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

// Introduction to ACPI Control Method Battery (2) - 2012
// https://alexhungdmz.blogspot.com/2012/06/introduction-to-acpi-control-method.html
namespace DellFanManagement.App
{ 
    class BatteryChargeSettings
    {
        private static readonly byte _reg = 0x24;
        private string _barcode = "";
        private int _chargeStartControl;
        private int _chargeStopControl;
        private int _chargeStartPercentage;
        private int _chargeStopPercentage;

        private readonly RegistryKey _registryKey;

        public bool HasBattery;
        private readonly object restartObj = new object();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle([In] IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        public BatteryChargeSettings()
        {
            _chargeStartControl = _chargeStopControl = 0;
            _chargeStartPercentage = _chargeStopPercentage = 100;
            HasBattery = false;
            try
            {
                _registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Lenovo\PWRMGRV\ConfKeys\Data\Battery1", true);
                if (_registryKey != null)
                {
                    _barcode = _registryKey.GetValue("Barcode Number").ToString();
                    if (_barcode.Length > 0)
                    {
                        HasBattery = true;
                        _registryKey.Close();
                        _registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Lenovo\PWRMGRV\ConfKeys\Data\" + _barcode, true);
                        if (_registryKey != null)
                        {
                            _chargeStartControl = int.Parse(_registryKey.GetValue("ChargeStartControl", "0").ToString());
                            _chargeStopControl = int.Parse(_registryKey.GetValue("ChargeStopControl", "0").ToString());
                            _chargeStartPercentage = int.Parse(_registryKey.GetValue("ChargeStartPercentage", "100").ToString());
                            _chargeStopPercentage = int.Parse(_registryKey.GetValue("ChargeStopPercentage", "100").ToString());
                        }
                    }
                }
            }catch(Exception expt)
            {

            }
        }

        public string GetBarCode()
        {
            return _barcode;
        }

        public void RestartPowerMgr()
        {
            string pszProcName = "PowerMgr.exe";
            const uint TH32CS_SNAPPROCESS = 2;
            PROCESSENTRY32 pe = new PROCESSENTRY32();
            pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            uint dwPid = 0;
            bool bFound = false;

            lock (restartObj)
            {
                IntPtr hSP = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (hSP != IntPtr.Zero)
                {
                    if (Process32First(hSP, ref pe))
                    {
                        do
                        {
                            if (String.Compare(pszProcName, pe.szExeFile, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                dwPid = pe.th32ProcessID;
                                var process = Process.GetProcessById((int)dwPid);
                                var fileName = process.MainModule.FileName;
                                if (fileName.IndexOf("\\Lenovo\\") >= 0)
                                {
                                    bFound = true;
                                    try
                                    {
                                        process.Kill();
                                    }catch(Exception expt)
                                    {
                                        // Do nothing
                                    }
                                    finally
                                    {
                                        Process.Start(fileName);
                                    }
                                    break;
                                }
                            }
                        } while (Process32Next(hSP, ref pe) && !bFound);

                        CloseHandle(hSP);

                        if (bFound)
                        {
                            return;
                        }
                    }
                }
            }
            return;
        }

        public bool ChargeStartControl()
        {
            return (_chargeStartControl == 0 ? false : true);
        }
        public void EnableChargeStart(bool ok)
        {
            if (_registryKey == null) return;

            _chargeStartControl = ok ? 1 : 0;
            _registryKey.SetValue("ChargeStartControl", ok ? 1 : 0, RegistryValueKind.DWord);
            RestartPowerMgr();
        }

        public bool ChargeStopControl()
        {
            return (_chargeStopControl == 0 ? false : true);
        }
        public void EnableChargeStop(bool ok)
        {
            if (_registryKey == null) return;

            _chargeStopControl = ok ? 1 : 0;
            _registryKey.SetValue("ChargeStopControl", ok ? 1 : 0, RegistryValueKind.DWord);
            RestartPowerMgr();
        }

        public void SetChargeStartPercentage(int val)
        {
            if (_registryKey == null) return;

            _chargeStartPercentage = val;
            _registryKey.SetValue("ChargeStartPercentage", val, RegistryValueKind.DWord);
            RestartPowerMgr();
        }

        public int GetChargeStartPercentage()
        {
            return _chargeStartPercentage;
        }

        public void SetChargeStopPercentage(int val)
        {
            if (_registryKey == null) return;

            _chargeStopPercentage = val;
            _registryKey.SetValue("ChargeStopPercentage", val, RegistryValueKind.DWord);
            SetChargeThreshold((byte)val);
            RestartPowerMgr();
        }

        public int GetChargeStopPercentage()
        {
            return _chargeStopPercentage;
        }

        public void SetChargeThreshold(byte val) 
        {
            if (val >= 100) val = 0;
            if(DellSmbiosBzh.Initialize())
            {
                DellSmbiosBzh.writeByte(_reg, val);
            }
        }

        public byte GetChargeThreshold() 
        {
            byte t = 100;
            if (DellSmbiosBzh.Initialize())
            {
                t = DellSmbiosBzh.readByte(_reg);
                if (t <= 0) t = 100;
            }

            return t;
        }
    }
}
