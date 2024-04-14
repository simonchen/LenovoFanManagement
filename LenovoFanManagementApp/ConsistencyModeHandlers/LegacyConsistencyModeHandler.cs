using DellFanManagement.App.FanControllers;
using DellFanManagement.App.TemperatureReaders;
using Salaros.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using DellFanManagement.DellSmbiozBzhLib;
using System.Threading;

namespace DellFanManagement.App.ConsistencyModeHandlers
{
    /// <summary>
    /// Used when full fan control is possible to "lock" the fan speed when conditions are right.
    /// </summary>
    public class LegacyConsistencyModeHandler : ConsistencyModeHandler
    {
        protected Dictionary<int, int[]> fanDict = new();
        FileSystemWatcher watcher;
        /// <summary>
        /// Semaphore for protecting access to fanDict changes.
        /// </summary>
        private readonly Semaphore _semaphore = new Semaphore(1,1);

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="core">Core object.</param>
        /// <param name="state">State object.</param>
        /// <param name="fanController">The fan controller currently in use.</param>
        public LegacyConsistencyModeHandler(Core core, State state, FanController fanController) : base(core, state, fanController)
        {
            // Init Fan info.
            InitFanInfo();

            // Monitor fan.ini if changed.
            string curDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            watcher = new FileSystemWatcher(curDir);

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Changed += new FileSystemEventHandler(OnFanInfoChanged);
            watcher.Created += new FileSystemEventHandler(OnFanInfoCreated);
            watcher.Renamed += new RenamedEventHandler(OnFanInfoRenamed);
            watcher.Filter = "fan.ini";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        private void InitFanInfo()
        {
            fanDict.Clear();
            string curDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string iniPath = string.Format("{0}{1}{2}", Directory.GetCurrentDirectory(), Path.DirectorySeparatorChar, "fan.ini");
            if (!File.Exists(iniPath))
            {
                return;
            }

            _semaphore.WaitOne();
            var config = new ConfigParser(iniPath);
            List<int> validLevel = new List<int>()
            {
                0,1,2,3,4,5,6,7,64
            };
            foreach (ConfigSection section in config.Sections)
            {
                /*
                if (section.SectionName == "RPM_DELAY")
                {
                    foreach (IConfigKeyValue item in section.Keys)
                    {

                    }
                }
                */

                if (section.SectionName == "RPM_TEMP")
                {
                    foreach (IConfigKeyValue item in section.Keys)
                    {
                        try
                        {
                            string[] arr = item.ValueRaw.ToString().Split("-");
                            int[] arr2 = new int[]
                            {
                                int.Parse(arr[0]),
                                int.Parse(arr[1])
                            };
                            if (item.Name.ToLower() == "auto")
                            {
                                fanDict.Add(AUTO_LEVEL, arr2);
                            }
                            else
                            {
                                int level = int.Parse(item.Name);
                                if (validLevel.IndexOf(level) >= 0)
                                {
                                    fanDict.Add(level, arr2);
                                }
                            }
                        }
                        catch (Exception expt)
                        {
                            // Do nothing

                        }
                    }
                }
            }
            _semaphore.Release();
        }

        private void OnFanInfoCreated(object sender, FileSystemEventArgs e)
        {
            InitFanInfo();
            string value = $"Created: {e.FullPath}";
            Console.WriteLine(value);
        }
        private void OnFanInfoRenamed(object sender, FileSystemEventArgs e)
        {
            InitFanInfo();
            Console.WriteLine($"Renamed:");
        }
        private void OnFanInfoChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            InitFanInfo();
            Console.WriteLine($"Changed: {e.FullPath}");
        }

        /// <summary>
        /// Consistency mode logic; lock the fans if temperature and RPM thresholds are met.
        /// </summary>
        static int cpu_cool_loops = 0;
        static int cpu_hot_loops = 0;
        const short CPU_COOL_DELAY = 5;
        const short CPU_HOT_COUNT = 5;
        public override void RunConsistencyModeLogic()
        {
            if (_core.LowerTemperatureThreshold != null && _core.UpperTemperatureThreshold != null && _core.RpmThreshold != null)
            {
                bool thresholdsMet = true;
                ulong? rpmLowerThreshold = _core.RpmThreshold - 400;
                bool is_cpu_warm = false;
                bool is_cpu_cool = false;
                bool is_cpu_too_hot = false;
                int temp_mid_val = (int)((_core.UpperTemperatureThreshold - _core.LowerTemperatureThreshold) / 2.0);

                // Is the fan speed too low?
                if (_state.Fan1Rpm < rpmLowerThreshold || (_state.Fan2Present && _state.Fan2Rpm < rpmLowerThreshold))
                {
                    if (_state.Fan1Rpm == 0 && (!_state.Fan2Present || _state.Fan2Rpm == 0))
                    {
                        if (!_core.EnableStopFan ||
                            LegacyConsistencyModeHandler.cpu_cool_loops <= 0)
                        {
                            thresholdsMet = false;
                            _state.ConsistencyModeStatus = string.Format("风扇未启动，等待EC激活{0}风扇", _state.Fan2Present ? "全部" : "");// Waiting for embedded controller to activate the fan{0}", _state.Fan2Present ? "s" : string.Empty);
                        }
                        else
                        {
                            _state.ConsistencyModeStatus = string.Format("风扇未启动，自动调节等待中...");
                        }
                    }
                    else
                    {
                        _state.ConsistencyModeStatus = "等待EC提高风扇速度"; // "Waiting for embedded controller to raise the fan speed";
                    }

                    //thresholdsMet = false;

                    if (!_state.EcFanControlEnabled)
                    {
                        Log.Write(string.Format("EC fan control disabled, but fan speed is too low: [{0}] [{1}]", _state.Fan1Rpm, _state.Fan2Present ? _state.Fan2Rpm : "N/A"));
                    }
                }

                // Is the CPU or GPU too hot?
                if (thresholdsMet)
                {
                    foreach (TemperatureComponent component in _state.Temperatures.Keys)
                    {
                        foreach (KeyValuePair<string, int> temperature in _state.Temperatures[component])
                        {
                            //if (component == TemperatureComponent.CPU &&
                            //    temperature.Key.ToLower().IndexOf("package") >= 0) continue; // Ignore CPU Package temperature
                            if (temperature.Value > _core.UpperTemperatureThreshold) //(_state.EcFanControlEnabled ?: _core.LowerTemperatureThreshold : _core.UpperTemperatureThreshold))
                            {
                                _state.ConsistencyModeStatus = string.Format("等待{0}温度下降", component);// Waiting for {0} temperature to fall", component);
                                thresholdsMet = false;
                                is_cpu_cool = false;
                                is_cpu_warm = false;
                                is_cpu_too_hot = true;
                                break;
                            }
                            else
                            {
                                if (temperature.Value > _core.LowerTemperatureThreshold)
                                {
                                    is_cpu_cool = false;
                                    if (temperature.Value <= (_core.LowerTemperatureThreshold + temp_mid_val))
                                    {
                                        is_cpu_warm = true;
                                    }
                                }
                                else
                                {
                                    is_cpu_cool = true;
                                }
                            }
                        }

                        if (!thresholdsMet)
                        {
                            break;
                        }
                    }
                }

                if (!is_cpu_cool)
                {
                    if (is_cpu_warm)
                    {
                        LegacyConsistencyModeHandler.cpu_hot_loops -= 1;
                        if (LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                        {
                            LegacyConsistencyModeHandler.cpu_hot_loops = 0;
                        }
                        if (LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                        {
                            _fanController.EnableAutomaticFanControl();
                            //_fanController.SetFanLevel(FanLevel.Low, FanIndex.AllFans);
                        }
                    }
                    else 
                    {
                        if (!is_cpu_too_hot)
                        {
                            LegacyConsistencyModeHandler.cpu_hot_loops -= 1;
                            if (LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                            {
                                LegacyConsistencyModeHandler.cpu_hot_loops = 0;
                            }
                            if (LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                            {
                                _fanController.EnableAutomaticFanControl();
                            }
                        }
                        else
                        {
                            LegacyConsistencyModeHandler.cpu_hot_loops += 1;
                            if (LegacyConsistencyModeHandler.cpu_hot_loops > CPU_HOT_COUNT)
                                LegacyConsistencyModeHandler.cpu_hot_loops = CPU_HOT_COUNT;
                            if (LegacyConsistencyModeHandler.cpu_hot_loops > 2) // Avoiding occasional hot in one second.
                            {
                                _fanController.SetFanLevel(FanLevel.SuperHigh, FanIndex.AllFans);
                            }
                            else
                            {
                                _fanController.EnableAutomaticFanControl();
                            }
                        }
                    }
                }
                else
                {
                    LegacyConsistencyModeHandler.cpu_hot_loops -= 1;
                    if (LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                    {
                        LegacyConsistencyModeHandler.cpu_hot_loops = 0;
                    }
                }

                 // Is the fan speed too high?
                if (thresholdsMet && (_state.Fan1Rpm > _core.RpmThreshold || (_state.Fan2Present && _state.Fan2Rpm > _core.RpmThreshold)))
                {
                    if (_state.Fan1Rpm < Core.RpmSanityCheck && (!_state.Fan2Present || _state.Fan2Rpm < Core.RpmSanityCheck))
                    {
                        _state.ConsistencyModeStatus = "风速过高, 等待EC减小风速";// "Waiting for embedded controller to reduce the fan speed";
                        //thresholdsMet = false;

                        if (!_state.EcFanControlEnabled)
                        {
                            Log.Write(string.Format("EC fan control disabled, but fan speed is too high: [{0}] [{1}]", _state.Fan1Rpm, _state.Fan2Present ? _state.Fan2Rpm : "N/A"));
                        }
                    }
                    else
                    {
                        Log.Write(string.Format("Recorded fan speed above sanity check level: [{0}] [{1}]", _state.Fan1Rpm, _state.Fan2Present ? _state.Fan2Rpm : "N/A"));
                    }

                    if (!is_cpu_cool && !is_cpu_warm) thresholdsMet = false;
                    if (!_state.EcFanControlEnabled) // cpu is warm / cool but fan speed is too high
                    {
                            //_fanController.DisableAutomaticFanControl();
                            if (is_cpu_warm)
                            {
                                _state.ConsistencyModeStatus = "温度在设定范围内，但风扇速度超过设定限值，EC自动调节。";
                            //_fanController.SetFanLevel(FanLevel.Medium, FanIndex.AllFans);
                            //_state.ConsistencyModeStatus = "温度在设定范围内，但风扇速度超过设定限值，尝试保持中速";
                        }
                            else if (is_cpu_cool &&
                                LegacyConsistencyModeHandler.cpu_cool_loops >= _core.CpuCoolDelay && 
                                _core.EnableStopFan)
                            {
                                _fanController.SetFanLevel(FanLevel.Off, FanIndex.AllFans);
                                _state.ConsistencyModeStatus = "温度低于下限，但风扇速度超过设定限值，尝试关闭风扇";
                            }

                            Log.Write("Disabled EC fan control – consistency mode – thresholds met");
                    }
                }

                //if (is_cpu_cool) thresholdsMet = false;
                if (is_cpu_warm || !thresholdsMet) LegacyConsistencyModeHandler.cpu_cool_loops = 0;

                if (thresholdsMet)
                {
                    if (!is_cpu_cool)
                    {
                        LegacyConsistencyModeHandler.cpu_cool_loops -= 1;

                        if (_state.EcFanControlEnabled)
                        {
                            _state.EcFanControlEnabled = false;
                            //_fanController.DisableAutomaticFanControl();
                            Log.Write("Disabled EC fan control – consistency mode – thresholds met");
                        }

                        if (!_state.ConsistencyModeStatus.StartsWith("风扇速度锁定"))//("Fan speed locked"))
                        {
                            _state.ConsistencyModeStatus = string.Format("风扇速度锁定 {0}", DateTime.Now); //"("Fan speed locked since {0}", DateTime.Now);
                        }

                        _core.TrayIconColor = TrayIconColor.Blue;
                    }
                    else
                    {
                        if (LegacyConsistencyModeHandler.cpu_cool_loops < 0)
                            LegacyConsistencyModeHandler.cpu_cool_loops = 0;
                        LegacyConsistencyModeHandler.cpu_cool_loops += 1;

                        if (!_state.EcFanControlEnabled && 
                            (!_core.EnableStopFan || LegacyConsistencyModeHandler.cpu_cool_loops < _core.CpuCoolDelay))
                        {
                            _state.EcFanControlEnabled = true;
                            if (is_cpu_warm && LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                            {
                                _fanController.EnableAutomaticFanControl();
                                //_fanController.SetFanLevel(FanLevel.Low, FanIndex.AllFans);
                            }
                            else
                            {
                                if (!is_cpu_too_hot && LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                                {
                                    _fanController.EnableAutomaticFanControl();
                                }
                                else
                                {
                                    if (LegacyConsistencyModeHandler.cpu_hot_loops > 2) // Avoiding occasional hot in one second.
                                    {
                                        _fanController.SetFanLevel(FanLevel.SuperHigh, FanIndex.AllFans);
                                    }
                                    else
                                    {
                                        _fanController.EnableAutomaticFanControl();
                                    }
                                }
                            }
                            _state.ConsistencyModeStatus = "温度低于下限，等待减小风速";
                            _core.TrayIconColor = TrayIconColor.Blue;
                        }
                        else if (LegacyConsistencyModeHandler.cpu_cool_loops >= _core.CpuCoolDelay) // stop fan after cpu was cooling for 15 secs
                        {
                            // EC did control the fan speed and  cpu has been kept on cooling for a longer, then we'll try to shutdown the fan.
                            if (_core.EnableStopFan && (_state.Fan1Rpm > 0 || (_state.Fan2Present && _state.Fan2Rpm > 0)))
                            {
                                _state.EcFanControlEnabled = false;
                                //_fanController.DisableAutomaticFanControl();
                                _fanController.SetFanLevel(FanLevel.Off, FanIndex.AllFans);
                                _state.ConsistencyModeStatus = "温度低于下限，尝试关闭风扇";
                                _core.TrayIconColor = TrayIconColor.Blue;
                            }
                        }
                        //else
                        {
                            if (_state.Fan1Rpm > 0 || (_state.Fan2Present && _state.Fan2Rpm > 0))
                                _core.TrayIconColor = TrayIconColor.Blue;
                            else
                                _core.TrayIconColor = TrayIconColor.Gray;
                        }
                    }
                }
                else if (!_state.EcFanControlEnabled && !thresholdsMet && !is_cpu_cool)
                {
                    _state.EcFanControlEnabled = true;
                    if (is_cpu_warm && LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                    {
                        _fanController.EnableAutomaticFanControl();
                        //_fanController.SetFanLevel(FanLevel.Low, FanIndex.AllFans);
                    }
                    else
                    {
                        if (!is_cpu_too_hot && LegacyConsistencyModeHandler.cpu_hot_loops <= 0)
                        {
                            _fanController.EnableAutomaticFanControl();
                        }
                        else
                        {
                            if (LegacyConsistencyModeHandler.cpu_hot_loops > 2) // Avoiding occasional hot in one second.
                            {
                                _fanController.SetFanLevel(FanLevel.SuperHigh, FanIndex.AllFans);
                            }
                            else
                            {
                                _fanController.EnableAutomaticFanControl();
                            }
                        }
                    }
                    Log.Write(string.Format("Enabled EC fan control – consistency mode – {0}", _state.ConsistencyModeStatus));
                    _core.TrayIconColor = TrayIconColor.Red;
                }
                else
                {
                    if (_state.Fan1Rpm > 0 || (_state.Fan2Present && _state.Fan2Rpm > 0))
                        _core.TrayIconColor = TrayIconColor.Red;
                    else
                        _core.TrayIconColor = TrayIconColor.Gray;
                }
            }
        }

        private class ClosestFanLevel
        {
            public int level;
            public int absDiff;
        }
        public static int AUTO_LEVEL = 99;
        public static int FAN_LEVEL_DELAY = 3; // in seconds
        public static int _fan_level_loops = 0;
        public static int _cur_fan_level = -1; // current fan level

        public override void RunConsistencyModeLogic2()
        {
            if (fanDict.Count == 0)
            {
                _fanController.EnableAutomaticFanControl();
                _cur_fan_level = AUTO_LEVEL;
                return;
            }

            int cur_temp = 0;
            foreach (TemperatureComponent component in _state.Temperatures.Keys)
            {
                foreach (KeyValuePair<string, int> temperature in _state.Temperatures[component])
                {
                    //if (component == TemperatureComponent.CPU &&
                    //    temperature.Key.ToLower().IndexOf("package") >= 0) continue; // Ignore CPU Package temperature
                    cur_temp = Math.Max(temperature.Value, cur_temp);
                }
            }

            // Look up the Fan level on fanDict
            _semaphore.WaitOne();
            int fanLevel = -1; // Invalid level
            bool bFound = false;
            List<ClosestFanLevel> rpmList = new List<ClosestFanLevel>();
            foreach (KeyValuePair<int, int[]> item in fanDict)
            {
                int configLevel = item.Key;
                int min_val = item.Value[0];
                int max_val = item.Value[1];

                if (cur_temp >= min_val && cur_temp <= max_val)
                {
                    fanLevel = configLevel;
                    bFound = true;
                    break;
                }
                else
                {
                    int absDiff = Math.Min(Math.Abs(cur_temp - min_val), Math.Abs(cur_temp - max_val));
                    if (absDiff <= 5) // For safely, we don't allow that difference is greater than 5 degree that would damage CPU/GPU
                    {
                        rpmList.Add(new ClosestFanLevel() { level = configLevel, absDiff = absDiff});
                    }
                }
            }
            _semaphore.Release();

            if (!bFound && rpmList.Count > 0)
            {
                rpmList.Sort((item1, item2) => (item1.absDiff > item2.absDiff ? 1 : -1));
                fanLevel = (int)rpmList[0].level;
            }

            if (fanLevel == -1 || fanLevel == AUTO_LEVEL)
            {
                _fanController.EnableAutomaticFanControl();
                _cur_fan_level = AUTO_LEVEL;
                _state.ConsistencyModeStatus = "EC自动调节...";
                _core.TrayIconColor = TrayIconColor.Gray;
                return;
            }

            if (_fan_level_loops > FAN_LEVEL_DELAY)
                _fan_level_loops = 0;
            if (_fan_level_loops == 0)
            {
                _fanController.SetFanLevelSpecific((BzhFanLevel)fanLevel, FanIndex.AllFans);
                _cur_fan_level = fanLevel;
                if (fanLevel == 0)
                {
                    _state.ConsistencyModeStatus = "关闭风扇";
                    _core.TrayIconColor = TrayIconColor.Gray;
                }
                else 
                {
                    if (fanLevel < 7)
                    {
                        _state.ConsistencyModeStatus = string.Format("风扇转速: {0} 档", fanLevel);
                        _core.TrayIconColor = TrayIconColor.Blue;
                    }
                    else
                    {
                        _state.ConsistencyModeStatus = string.Format("风扇转速: {0} 档", fanLevel);
                        _core.TrayIconColor = TrayIconColor.Red;
                    }
                }
            }
            _fan_level_loops += 1;
        }
    }
}
