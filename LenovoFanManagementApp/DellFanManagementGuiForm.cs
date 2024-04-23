using DellFanManagement.App.TemperatureReaders;
using DellFanManagement.DellSmbiosSmiLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.Text;
using Securite.Win32;
using System.Security.Principal;
using System.Security.AccessControl;

using PowerManagerAPI;
using System.Collections.Generic;

namespace DellFanManagement.App
{
    /// <summary>
    /// This class manages the Windows Forms application for the app.
    /// </summary>
    public partial class DellFanManagementGuiForm : Form
    {
        /// <summary>
        ///  Power schemes supplied by Windows 10/11
        /// </summary>
        Guid balancedPlanGuid = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        Guid highPerformancePlanGuid = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        Guid powerSaverPlanGuid = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");

        /// BatterySettings
        private readonly BatteryChargeSettings _battery;

        /// <summary>
        /// Shared object which contains the application state.
        /// </summary>
        private readonly State _state;

        /// <summary>
        /// The "Core" object does the actual system interations.
        /// </summary>
        private readonly Core _core;

        /// <summary>
        /// Handles storing the options selected in the program in the registry.
        /// </summary>
        private readonly ConfigurationStore _configurationStore;

        /// <summary>
        /// Pre-loaded icons to use in the system tray.
        /// </summary>
        private readonly Icon[] _trayIcons;

        /// <summary>
        /// Next tray icon animation to be displayed.
        /// </summary>
        private int _trayIconIndex;

        /// <summary>
        /// Indicates that the program is closing, so background threads should stop.
        /// </summary>
        private bool _formClosed;

        /// <summary>
        /// Indicates whether the initial setup code has finished or not.
        /// </summary>
        private readonly bool _initializationComplete;

        /// <summary>
        /// Last observed Windows power profile.
        /// </summary>
        private Guid? _registeredPowerProfile;

        private readonly string STARTUP_NAME;
        private List<int> _disabledStartChargeItems;
        private List<int> _disabledStopChargeItems;
        private int _prevStartChargeIndex;
        private int _prevStopChargeIndex;

        private Guid _activePlan;

        /// <summary>
        /// Constructor.  Get everything set up before the window is displayed.
        /// </summary>
        public DellFanManagementGuiForm()
        {
            _initializationComplete = false;

            InitializeComponent();

            _disabledStartChargeItems = new List<int>();
            _disabledStopChargeItems = new List<int>();
            _prevStartChargeIndex = -1;
            _prevStopChargeIndex = -1;

            // Initialize objects.
            _configurationStore = new();
            _battery = new BatteryChargeSettings();
            _state = new State(_configurationStore);
            _core = new Core(_state, this);
            _formClosed = false;
            STARTUP_NAME = "Lenovo Fan Management";

            _trayIcons = new Icon[48];
            _trayIconIndex = 0;
            LoadTrayIcons();

            // Disclaimer.
            if (_configurationStore.GetIntOption(ConfigurationOption.DisclaimerShown) != 1 && !UacHelper.IsSystemProcess())
            {
                ShowDisclaimer();
                _configurationStore.SetOption(ConfigurationOption.DisclaimerShown, 1);
            }

            // Version number in the about box.
            aboutProductLabel.Text = string.Format("Dell Fan Management, version {0}", DellFanManagementApp.Version);

            // Set event handlers.
            FormClosed += new FormClosedEventHandler(FormClosedEventHandler);
            Resize += new EventHandler(OnResizeEventHandler);
            Load += new EventHandler(OnLoad);
            trayIcon.Click += new EventHandler(TrayIconOnClickEventHandler);

            // ...Thermal setting radio buttons...
            //thermalSettingRadioButtonOptimized.CheckedChanged += new EventHandler(ThermalSettingChangedEventHandler);
            //thermalSettingRadioButtonCool.CheckedChanged += new EventHandler(ThermalSettingChangedEventHandler);
            //thermalSettingRadioButtonQuiet.CheckedChanged += new EventHandler(ThermalSettingChangedEventHandler);
            //thermalSettingRadioButtonPerformance.CheckedChanged += new EventHandler(ThermalSettingChangedEventHandler);

            // ...EC fan control radio buttons...
            ecFanControlRadioButtonOn.CheckedChanged += new EventHandler(EcFanControlSettingChangedEventHandler);
            ecFanControlRadioButtonOff.CheckedChanged += new EventHandler(EcFanControlSettingChangedEventHandler);

            // ...Manual fan control radio buttons...
            manualFan1RadioButtonOff.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan1RadioButtonLow.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan1RadioButtonMedium.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan1RadioButtonHigh.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan1RadioButtonHighest.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan2RadioButtonOff.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan2RadioButtonMedium.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            manualFan2RadioButtonHigh.CheckedChanged += new EventHandler(FanLevelChangedEventHandler);
            
            // ...Restart background thread button...
            restartBackgroundThreadButton.Click += new EventHandler(ThermalSettingChangedEventHandler);

            // ...Operation mode radio buttons...
            operationModeRadioButtonAutomatic.CheckedChanged += new EventHandler(ConfigurationRadioButtonAutomaticEventHandler);
            operationModeRadioButtonManual.CheckedChanged += new EventHandler(ConfigurationRadioButtonManualEventHandler);
            operationModeRadioButtonConsistency.CheckedChanged += new EventHandler(ConfigurationRadioButtonConsistencyEventHandler);
            operationModeRadioButtonCustom.CheckedChanged += new EventHandler(ConfigurationRadioButtonCustomEventHandler);

            editFanButton.Click += new EventHandler(EditFanSpeedEventHandler);

            // ...Consistency mode section...
            stopFanCheckbox.CheckedChanged += new EventHandler(ConsistencyModeTextBoxesChangedEventHandler);
            coolStopFanDelay.TextChanged += new EventHandler(ConsistencyModeTextBoxesChangedEventHandler);
            consistencyModeLowerTemperatureThresholdTextBox.TextChanged += new EventHandler(ConsistencyModeTextBoxesChangedEventHandler);
            consistencyModeUpperTemperatureThresholdTextBox.TextChanged += new EventHandler(ConsistencyModeTextBoxesChangedEventHandler);
            consistencyModeRpmThresholdTextBox.TextChanged += new EventHandler(ConsistencyModeTextBoxesChangedEventHandler);
            consistencyModeApplyChangesButton.Click += new EventHandler(ConsistencyApplyChangesButtonClickedEventHandler);

            // ...Cpu Freq limit...
            ACCpuFreq1TextBox.TextChanged += new EventHandler(CpuFreqChangedEventHandler);
            DCCpuFreq1TextBox.TextChanged += new EventHandler(CpuFreqChangedEventHandler);
            ACCpuFreqTextBox.TextChanged += new EventHandler(CpuFreqChangedEventHandler);
            DCCpuFreqTextBox.TextChanged += new EventHandler(CpuFreqChangedEventHandler);
            ApplyCpuFreqButton.Click += new EventHandler(ApplyCpuFreqButtonClickedEventHandler);

            // ...Battery charge threshold...
            allowBacklightCheckBox.CheckedChanged += new EventHandler(AllowBacklightCheckBoxChangedEventHandler);
            bkgLightDelay.TextChanged += new EventHandler(AllowBacklightCheckBoxChangedEventHandler);

            // ...Audio keep alive controls...
            audioKeepAliveComboBox.SelectedValueChanged += new EventHandler(AudioDeviceChangedEventHandler);
            audioKeepAliveCheckbox.CheckedChanged += new EventHandler(AudioKeepAliveCheckboxChangedEventHandler);

            // ...Tray icon checkboxes...
            trayIconCheckBox.CheckedChanged += new EventHandler(TrayIconCheckBoxChangedEventHandler);
            animatedCheckBox.CheckedChanged += new EventHandler(AnimatedCheckBoxChangedEventHandler);
            startupCheckBox.CheckedChanged += new EventHandler(StartupCheckBoxChangedEventHandler);

            gpuTCheckBox.CheckedChanged += new EventHandler(GpuTCheckBoxChangedEventHandler);
            hideWatermarkCheckBox.CheckedChanged += new EventHandler(HideWatermarkCheckBoxChangedEventHandler);
            chargeStartThresholdComboBox.SelectedValueChanged += new EventHandler(ChargeThresholdSelectEventHandler);
            chargeStopThresholdComboBox.SelectedValueChanged += new EventHandler(ChargeThresholdSelectEventHandler);
            chargeStartThresholdComboBox.DrawItem += new DrawItemEventHandler(ChargeStartThresholdComboBox_DrawItem);
            chargeStopThresholdComboBox.DrawItem += new DrawItemEventHandler(ChargeStopThresholdComboBox_DrawItem);

            chargeStartControlCheckBox.CheckedChanged += new EventHandler(ChargeStartControlChangedEventHandler);
            chargeStopControlCheckBox.CheckedChanged += new EventHandler(ChargeStopControlChangedEventHandler);

            logCheckBox.CheckedChanged += new EventHandler(LogCheckBoxChangedEventHandler);

            if (UacHelper.IsSystemProcess())
            {
                //hideWatermarkCheckBox.Enabled = false;
                //startupCheckBox.Enabled = false;
            }

            if (_battery.HasBattery)
            {
                batteryChargeGroupBox.Enabled = true;
                batteryChargeLabel.Text = string.Format("{0}({1})", batteryChargeLabel.Text, _battery.GetBarCode());

                int t = _battery.GetChargeStopPercentage();
                string pct_str = string.Format("{0}%", t);
                int idx = chargeStopThresholdComboBox.FindStringExact(pct_str);
                chargeStopThresholdComboBox.SelectedIndex = idx;
                t = _battery.GetChargeStartPercentage();
                pct_str = string.Format("{0}%", t);
                idx = chargeStartThresholdComboBox.FindStringExact(pct_str);
                chargeStartThresholdComboBox.SelectedIndex = idx;

                chargeStartControlCheckBox.Checked = _battery.ChargeStartControl();
                chargeStopControlCheckBox.Checked = _battery.ChargeStopControl();

                chargeStartThresholdComboBox.Enabled = chargeStartControlCheckBox.Checked;
                chargeStopThresholdComboBox.Enabled = chargeStopControlCheckBox.Checked;

            }
            else
            {
                batteryChargeGroupBox.Enabled = false;
            }

            // Empty out pre-populated temperature label text fields.
            // (There are so many to allow support for lots of CPU cores, which many systems will not have.)
            temperatureLabel1.Text = string.Empty;
            temperatureLabel2.Text = string.Empty;
            temperatureLabel3.Text = string.Empty;
            temperatureLabel4.Text = string.Empty;
            temperatureLabel5.Text = string.Empty;
            temperatureLabel6.Text = string.Empty;
            temperatureLabel7.Text = string.Empty;
            temperatureLabel8.Text = string.Empty;
            temperatureLabel9.Text = string.Empty;
            temperatureLabel10.Text = string.Empty;
            temperatureLabel11.Text = string.Empty;
            temperatureLabel12.Text = string.Empty;
            temperatureLabel13.Text = string.Empty;
            temperatureLabel14.Text = string.Empty;
            temperatureLabel15.Text = string.Empty;
            temperatureLabel16.Text = string.Empty;
            temperatureLabel17.Text = string.Empty;
            temperatureLabel18.Text = string.Empty;

            // Disable some options depending on fan control capability.
            if (!_core.IsAutomaticFanControlDisableSupported)
            {
                operationModeRadioButtonManual.Enabled = false;
                //operationModeRadioButtonConsistency.Enabled = false;
                consistencyModeLowerTemperatureThresholdLabel.Enabled = false;
                consistencyModeUpperTemperatureThresholdLabel.Enabled = false;
                consistencyModeLowerTemperatureThresholdTextBox.Enabled = false;
                consistencyModeUpperTemperatureThresholdTextBox.Enabled = false;
            }
            if (!_core.IsSpecificFanControlSupported)
            {
                operationModeRadioButtonManual.Enabled = false;
            }

            // Apply configuration loaded from registry.
            ApplyConfiguration();

            // Save initial keep alive configuration.
            WriteConsistencyModeConfiguration();

            // Initial update of the tray icon (required for it to appear for display).
            UpdateTrayIcon(false);

            // Update form with default state values.
            UpdateForm();

            // Apply audio keep alive configuration from registry.
            ApplyAudioKeepAliveConfiguration();

            // Apply manual fan control configuration from registry.
            ApplyManualModeConfiguration();

            // Start threads to do background work.
            _core.StartBackgroundThread();
            StartTrayIconThread();

            _initializationComplete = true;
        }

        /// <summary>
        /// Apply configuration settings loaded from the registry.
        /// </summary>
        private void ApplyConfiguration()
        {
            // The tray icon is enabled by default; disable only if that has been explicitly set.
            if (_configurationStore.GetIntOption(ConfigurationOption.TrayIconEnabled) == 0)
            {
                trayIconCheckBox.Checked = false;
            }

            // Similar for tray icon animation.
            if (_configurationStore.GetIntOption(ConfigurationOption.TrayIconAnimationEnabled) == 0)
            {
                animatedCheckBox.Checked = false;
            }

            // Logging enabled?
            if (_configurationStore.GetIntOption(ConfigurationOption.AllowLogWriteToFile) == 0 ||
                _configurationStore.GetIntOption(ConfigurationOption.AllowLogWriteToFile) == null)
            {
                logCheckBox.Checked = false;
            }

            // Gpu Temparature detected?
            int? gpuT = _configurationStore.GetIntOption(ConfigurationOption.GpuTEnabled);
            if (gpuT == null || gpuT == 0)
            {
                gpuTCheckBox.Checked = false;
            }

            // Hide watermark on Windows desktop background
            int? hideWatermark = _configurationStore.GetIntOption(ConfigurationOption.HideWatermarkEnabled);
            if (hideWatermark == null || hideWatermark == 0)
            {
                hideWatermarkCheckBox.Checked = false;
            }

            // Startup run
            int ? ret = _configurationStore.GetIntOption(ConfigurationOption.StartupEnabled);
            /*
            RegistryKey _registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            string path = _registryKey.GetValue(STARTUP_NAME, "").ToString();
            if (ret == 0 || path.Length == 0)
            {
                startupCheckBox.Checked = false;
            }
            _registryKey.Close();
            */
            string output = "";
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
                p.StartInfo.Arguments = " /query /tn \"LenovoFanManagement\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                p.WaitForExit();
                output = p.StandardOutput.ReadToEnd();
                p.Close();
            }catch (Exception)
            {
                // do nothing
            }

            if (ret == 0 || !output.Contains("LenovoFanManagement") )
            {
                startupCheckBox.Checked = false;
            }

            // Consistency mode settings.
            int? lowerTemperatureThreshold = _configurationStore.GetIntOption(ConfigurationOption.ConsistencyModeLowerTemperatureThreshold);
            int? upperTemperatureThreshold = _configurationStore.GetIntOption(ConfigurationOption.ConsistencyModeUpperTemperatureThreshold);

            if (lowerTemperatureThreshold != null && lowerTemperatureThreshold >= 0 && lowerTemperatureThreshold < 100 &&
                upperTemperatureThreshold != null && upperTemperatureThreshold >= 0 && upperTemperatureThreshold < 100 &&
                lowerTemperatureThreshold <= upperTemperatureThreshold)
            {
                consistencyModeLowerTemperatureThresholdTextBox.Text = lowerTemperatureThreshold.ToString();
                consistencyModeUpperTemperatureThresholdTextBox.Text = upperTemperatureThreshold.ToString();
            }

            int? rpmThreshold = _configurationStore.GetIntOption(ConfigurationOption.ConsistencyModeRpmThreshold);
            if (rpmThreshold != null && rpmThreshold > 0 && rpmThreshold < 10000)
            {
                consistencyModeRpmThresholdTextBox.Text = rpmThreshold.ToString();
            }

            // Stop fan after x seconds
            if (_configurationStore.GetIntOption(ConfigurationOption.StopFanEnabled) == null ||
                _configurationStore.GetIntOption(ConfigurationOption.StopFanEnabled) == 0)
            {
                stopFanCheckbox.Checked = false;
                coolStopFanDelay.Enabled = false;
            }
            else
            {
                stopFanCheckbox.Checked = true;
                coolStopFanDelay.Enabled = true;
            }
            int? cpuCoolDelay = _configurationStore.GetIntOption(ConfigurationOption.CpuCoolDelay);
            if (cpuCoolDelay != null && cpuCoolDelay >= 0 && cpuCoolDelay < 100)
            {
                coolStopFanDelay.Text = cpuCoolDelay.ToString();
            }

            // Auto turn off keyboard backlight after x seconds
            if (_configurationStore.GetIntOption(ConfigurationOption.AllowBacklightDelay) == null ||
                _configurationStore.GetIntOption(ConfigurationOption.AllowBacklightDelay) == 0)
            {
                allowBacklightCheckBox.Checked = false;
                bkgLightDelay.Enabled = false;
            }
            else
            {
                allowBacklightCheckBox.Checked = true;
                bkgLightDelay.Enabled = true;
            }
            int? backlightDelay = _configurationStore.GetIntOption(ConfigurationOption.BacklightDelay);
            if (backlightDelay != null && backlightDelay >= 2 && backlightDelay < 100)
            {
                bkgLightDelay.Text = backlightDelay.ToString();
            }

            // CPU Frequency limits

            // P-Cores
            int? ACCpuFreq1 = _configurationStore.GetIntOption(ConfigurationOption.ACCpuFreq1);
            if (ACCpuFreq1 == null)
            {
                ACCpuFreq1TextBox.Text = "";
            }
            else
            {
                ACCpuFreq1TextBox.Text = ACCpuFreq1.ToString();
            }
            int? DCCpuFreq1 = _configurationStore.GetIntOption(ConfigurationOption.DCCpuFreq1);
            if (DCCpuFreq1 == null)
            {
                DCCpuFreq1TextBox.Text = "";
            }
            else
            {
                DCCpuFreq1TextBox.Text = DCCpuFreq1.ToString();
            }

            // E-Cores
            int? ACCpuFreq = _configurationStore.GetIntOption(ConfigurationOption.ACCpuFreq);
            if (ACCpuFreq == null)
            {
                ACCpuFreqTextBox.Text = "";
            }
            else
            {
                ACCpuFreqTextBox.Text = ACCpuFreq.ToString();
            }
            int? DCCpuFreq = _configurationStore.GetIntOption(ConfigurationOption.DCCpuFreq);
            if (DCCpuFreq == null)
            {
                DCCpuFreqTextBox.Text = "";
            }
            else
            {
                DCCpuFreqTextBox.Text = DCCpuFreq.ToString();
            }

            /*
            List<Guid> plans = PowerManager.ListPlans();
            for (int i = 0; i < plans.Count; i++)
            {
                Guid plan = plans[i];
            }

            var name = PowerManager.GetPlanName(highPerformancePlanGuid);
            PowerManager.SetActivePlan(highPerformancePlanGuid);
            */

            // Read previous operation mode from configuration.
            bool modeSet = false;
            if (Enum.TryParse(_configurationStore.GetStringOption(ConfigurationOption.OperationMode), out OperationMode operationMode))
            {
                switch (operationMode)
                {
                    case OperationMode.Automatic:
                        operationModeRadioButtonAutomatic.Checked = true;
                        modeSet = true;
                        break;
                    case OperationMode.Manual:
                        if (operationModeRadioButtonManual.Enabled)
                        {
                            operationModeRadioButtonManual.Checked = true;
                            modeSet = true;
                        }
                        break;
                    case OperationMode.Consistency:
                        if (operationModeRadioButtonConsistency.Enabled)
                        {
                            operationModeRadioButtonConsistency.Checked = true;
                            modeSet = true;
                        }
                        break;
                    case OperationMode.Consistency2:
                        if (operationModeRadioButtonCustom.Enabled)
                        {
                            operationModeRadioButtonCustom.Checked = true;
                            modeSet = true;
                        }
                        break;
                }
            }
            if (!modeSet)
            {
                // Default to automatic mode.
                //operationModeRadioButtonAutomatic.Checked = true;

                // Default to consistency mode
                operationModeRadioButtonConsistency.Checked = true;
            }
        }

        /// <summary>
        /// Apply audio keep alive configuration using values from the registry.
        /// </summary>
        private void ApplyAudioKeepAliveConfiguration()
        {
            if (_configurationStore.GetIntOption(ConfigurationOption.AudioKeepAliveEnabled) == 1)
            {
                // Audio keep alive should be enabled.  Let's see if the audio device is present.
                string storedAudioDeviceId = _configurationStore.GetStringOption(ConfigurationOption.AudioKeepAliveSelectedDevice);

                foreach (object deviceObject in audioKeepAliveComboBox.Items)
                {
                    AudioDevice device = (AudioDevice)deviceObject;
                    if (device.DeviceId == storedAudioDeviceId)
                    {
                        // Found it!
                        audioKeepAliveComboBox.SelectedItem = deviceObject;
                        audioKeepAliveCheckbox.Checked = true; // This will kick off the audio thread.
                        break;
                    }
                }

                // If we get down here and the checkbox is not checked, then the device was not in the list.
                if (!audioKeepAliveCheckbox.Checked)
                {
                    _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveEnabled, 0);
                }
            }
        }

        /// <summary>
        /// Apply manual mode configuration from the registry.
        /// </summary>
        private void ApplyManualModeConfiguration()
        {
            if (operationModeRadioButtonManual.Checked)
            {
                // Apply saved manual mode configuration.
                if (_configurationStore.GetIntOption(ConfigurationOption.ManualModeEcFanControlEnabled) == 0)
                {
                    ecFanControlRadioButtonOff.Checked = true;

                    if (Enum.TryParse(_configurationStore.GetStringOption(ConfigurationOption.ManualModeFan1Level), out FanLevel fan1Level))
                    {
                        switch (fan1Level)
                        {
                            case FanLevel.Off:
                                manualFan1RadioButtonOff.Checked = true;
                                break;
                            case FanLevel.Medium:
                                manualFan1RadioButtonMedium.Checked = true;
                                break;
                            case FanLevel.High:
                                manualFan1RadioButtonHigh.Checked = true;
                                break;
                        }
                    }

                    if (Enum.TryParse(_configurationStore.GetStringOption(ConfigurationOption.ManualModeFan2Level), out FanLevel fan2Level))
                    {
                        switch (fan2Level)
                        {
                            case FanLevel.Off:
                                manualFan2RadioButtonOff.Checked = true;
                                break;
                            case FanLevel.Medium:
                                manualFan2RadioButtonMedium.Checked = true;
                                break;
                            case FanLevel.High:
                                manualFan2RadioButtonHigh.Checked = true;
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clear the manual operation mode configuration (when we switch to a different mode, it should be reset).
        /// </summary>
        private void ClearManualControlConfiguration()
        {
            _configurationStore.SetOption(ConfigurationOption.ManualModeEcFanControlEnabled, null);
            _configurationStore.SetOption(ConfigurationOption.ManualModeFan1Level, null);
            _configurationStore.SetOption(ConfigurationOption.ManualModeFan2Level, null);
        }

        /// <summary>
        /// Update the form based on the current state.
        /// </summary>
        public void UpdateForm()
        {
            // This method does not write to the state, but we should still make sure that the state is not changing
            // during the update.
            _state.WaitOne();

            AudioDevice bringBackAudioDevice = null;

            // Fan RPM.
            fan1RpmLabel.Text = string.Format("Fan 1 RPM: {0}", _state.Fan1Rpm != null ? _state.Fan1Rpm : "(Error)");

            if (_state.Fan2Present)
            {
                fan2RpmLabel.Text = string.Format("Fan 2 RPM: {0}", _state.Fan2Rpm != null ? _state.Fan2Rpm : "(Error)");
                fan2RpmLabel.Enabled = true;

                if (_core.IsIndividualFanControlSupported)
                {
                    manualFan2GroupBox.Enabled = true;
                }
            }
            else
            {
                fan2RpmLabel.Text = string.Format("Fan 2 not present");
                fan2RpmLabel.Enabled = false;
            }

            // Temperatures.
            int labelIndex = 0;
            int cpu_count = 0;
            int total_temperature = 0;
            int avg_temperature = 0;
            foreach (TemperatureComponent component in _state.Temperatures.Keys)
            {
                foreach (string key in _state.Temperatures[component].Keys)
                {
                    if (key.IndexOf("CPU") >= 0)
                    {
                        cpu_count += 1;
                        total_temperature += _state.Temperatures[component][key];
                    }
                    string temperature = _state.Temperatures[component][key] != 0 ? _state.Temperatures[component][key].ToString() : "--";

                    string labelValue;
                    if (_state.MinimumTemperatures[component].ContainsKey(key) && _state.MaximumTemperatures[component].ContainsKey(key))
                    {
                        labelValue = string.Format("{0}: {1} ({2}-{3})", key, temperature, _state.MinimumTemperatures[component][key], _state.MaximumTemperatures[component][key]);
                    }
                    else
                    {
                        labelValue = string.Format("{0}: {1}", key, temperature);
                    }

                    switch (labelIndex)
                    {
                        case 0: temperatureLabel1.Text = labelValue; break;
                        case 1: temperatureLabel2.Text = labelValue; break;
                        case 2: temperatureLabel3.Text = labelValue; break;
                        case 3: temperatureLabel4.Text = labelValue; break;
                        case 4: temperatureLabel5.Text = labelValue; break;
                        case 5: temperatureLabel6.Text = labelValue; break;
                        case 6: temperatureLabel7.Text = labelValue; break;
                        case 7: temperatureLabel8.Text = labelValue; break;
                        case 8: temperatureLabel9.Text = labelValue; break;
                        case 9: temperatureLabel10.Text = labelValue; break;
                        case 10: temperatureLabel11.Text = labelValue; break;
                        case 11: temperatureLabel12.Text = labelValue; break;
                        case 12: temperatureLabel13.Text = labelValue; break;
                        case 13: temperatureLabel14.Text = labelValue; break;
                        case 14: temperatureLabel15.Text = labelValue; break;
                        case 15: temperatureLabel16.Text = labelValue; break;
                        case 16: temperatureLabel17.Text = labelValue; break;
                        case 17: temperatureLabel18.Text = labelValue; break;
                    }

                    labelIndex++;
                }
            }
            if (cpu_count > 0)
            {
                avg_temperature = total_temperature / cpu_count;
            }

            // EC fan control enabled?
            if (_state.OperationMode != OperationMode.Manual)
            {
                if (_state.EcFanControlEnabled && !ecFanControlRadioButtonOn.Checked)
                {
                    ecFanControlRadioButtonOn.Checked = true;
                }
                else if (!_state.EcFanControlEnabled && !ecFanControlRadioButtonOff.Checked)
                {
                    ecFanControlRadioButtonOff.Checked = true;
                }
            }

            // Consistency mode status.
            consistencyModeStatusLabel.Text = _state.ConsistencyModeStatus;

            // Thermal setting.
            /*
            if (_core.RequestedThermalSetting == null)
            {
                switch (_state.ThermalSetting)
                {
                    case ThermalSetting.Optimized:
                        SetThermalSettingAvaiability(true);
                        thermalSettingRadioButtonOptimized.Checked = true;
                        break;
                    case ThermalSetting.Cool:
                        SetThermalSettingAvaiability(true);
                        thermalSettingRadioButtonCool.Checked = true;
                        break;
                    case ThermalSetting.Quiet:
                        SetThermalSettingAvaiability(true);
                        thermalSettingRadioButtonQuiet.Checked = true;
                        break;
                    case ThermalSetting.Performance:
                        SetThermalSettingAvaiability(true);
                        thermalSettingRadioButtonPerformance.Checked = true;
                        break;
                    case ThermalSetting.Error:
                        SetThermalSettingAvaiability(false);
                        break;
                }
            }
            */

            // Restart background thread button.
            restartBackgroundThreadButton.Enabled = !_state.BackgroundThreadRunning;

            // Sync up audio devices list.
            List<AudioDevice> devicesToAdd = new();
            List<AudioDevice> devicesToRemove = new();

            // Items to add.
            foreach (AudioDevice audioDevice in _state.AudioDevices)
            {
                if (!audioKeepAliveComboBox.Items.Contains(audioDevice))
                {
                    devicesToAdd.Add(audioDevice);
                }
            }

            // Items to remove.
            foreach (AudioDevice audioDevice in audioKeepAliveComboBox.Items)
            {
                if (!_state.AudioDevices.Contains(audioDevice))
                {
                    devicesToRemove.Add(audioDevice);
                }
            }

            // Perform additions and removals.
            foreach (AudioDevice audioDevice in devicesToAdd)
            {
                audioKeepAliveComboBox.Items.Add(audioDevice);

                // ...If this happens to be the previously selected audio device that disappeared, set it back and start
                // the thread.
                if (audioDevice == _state.BringBackAudioDevice || audioDevice.DeviceId == _configurationStore.GetStringOption(ConfigurationOption.AudioKeepAliveBringBackDevice))
                {
                    bringBackAudioDevice = audioDevice;
                }
            }
            foreach (AudioDevice audioDevice in devicesToRemove)
            {
                audioKeepAliveComboBox.Items.Remove(audioDevice);
            }

            if (audioKeepAliveComboBox.SelectedItem == null)
            {
                audioKeepAliveCheckbox.Enabled = false;
            }

            if (audioKeepAliveCheckbox.Checked && !_state.AudioThreadRunning)
            {
                audioKeepAliveCheckbox.Checked = false;
            }

            // Tray icon hover text.
            if (_state.Fan2Present)
            {
                trayIcon.Text = string.Format("Lenovo 风扇管理\nCPU: {0}\u00B0\n{1}\n{2}", avg_temperature, fan1RpmLabel.Text, fan2RpmLabel.Text);
            }
            else
            {
                trayIcon.Text = string.Format("Lenovo 风扇管理\nCPU: {0}\u00B0\n{1}", avg_temperature, fan1RpmLabel.Text);
            }

            UpdateTrayIcon(false);

            // Error message.
            if (_state.Error != null)
            {
                MessageBox.Show(_state.Error, "Error in background thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _state.Error = null; // ...The one place where the state is actually updated.
            }

            // Power profiles management.
            if (_state.ActivePowerProfile != null)
            {
                if (_state.ActivePowerProfile != _registeredPowerProfile)
                {
                    if (_registeredPowerProfile == null)
                    {
                        Log.Write(string.Format("The active power profile is {0}", _state.ActivePowerProfile));
                    }
                    else
                    {
                        Log.Write(string.Format("Power profile changed from {0} to {1}", _registeredPowerProfile, _state.ActivePowerProfile));

                        // Check to see if we should change the thermal setting.
                        ThermalSetting? thermalSettingOverride = _configurationStore.GetThermalSettingOverride((Guid)_state.ActivePowerProfile);
                        if (thermalSettingOverride != null)
                        {
                            _core.RequestThermalSetting((ThermalSetting) thermalSettingOverride);
                            Log.Write(string.Format("Thermal setting override: {0}", thermalSettingOverride));
                        }

                        // Check to see if we should change the power mode.
                        Guid? powerMode = _configurationStore.GetPowerModeOverride((Guid)_state.ActivePowerProfile);
                        if (powerMode != null)
                        {
                            Utility.PowerSetActiveOverlayScheme((Guid)powerMode); // NULL check above.
                            Log.Write(string.Format("Power mode overrode: {0}", powerMode));
                        }

                        // Check to see if we should change the NVIDIA GPU P-state.
                        int? nvPstate = _configurationStore.GetNvPstateOverride((Guid)_state.ActivePowerProfile);
                        if (nvPstate != null)
                        {
                            string nvInspectorPath = _configurationStore.GetStringOption(ConfigurationOption.NVPStateApplicationPath);
                            if (nvInspectorPath != null)
                            {
                                Utility.SetNvidiaGpuPstate(nvInspectorPath, (int)nvPstate); // NULL check above.
                                Log.Write(string.Format("NVIDIA P-state override: {0}", nvPstate));
                            }
                        }
                    }
                    _registeredPowerProfile = _state.ActivePowerProfile;
                }
            }

            // Power schemes (Always monitoring current actived power scheme)
            Guid activePlan = PowerManager.GetActivePlan();
            if (activePlan != _activePlan)
            {
                _activePlan = activePlan;
                activePowerPlanLabel.Text = "(当前电源计划:" + PowerManager.GetPlanName(activePlan) + ")";
                uint acFreqMax = PowerManager.GetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX, PowerMode.AC);
                uint dcFreqMax = PowerManager.GetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX, PowerMode.DC);
                uint acFreqMax1 = PowerManager.GetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX1, PowerMode.AC);
                uint dcFreqMax1 = PowerManager.GetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX1, PowerMode.DC);

                ACCpuFreqTextBox.Text = acFreqMax.ToString();
                ACCpuFreq1TextBox.Text = acFreqMax1.ToString();
                DCCpuFreqTextBox.Text = dcFreqMax.ToString();
                DCCpuFreq1TextBox.Text = dcFreqMax1.ToString();
            }

            _state.Release();

            if (bringBackAudioDevice != null)
            {
                audioKeepAliveComboBox.SelectedItem = bringBackAudioDevice;
                audioKeepAliveCheckbox.Checked = true;
            }
        }

        /// <summary>
        /// Called when "Automatic" configuration radio button is clicked.
        /// </summary>
        private void ConfigurationRadioButtonAutomaticEventHandler(Object sender, EventArgs e)
        {
            _core.SetAutomaticMode();
            _configurationStore.SetOption(ConfigurationOption.OperationMode, OperationMode.Automatic);
            ClearManualControlConfiguration();

            SetFanControlsAvailability(false);
            SetConsistencyModeControlsAvailability(false);
            editFanButton.Enabled = false;
            SetEcFanControlsAvailability(false);

            UpdateTrayIcon(false);
        }

        /// <summary>
        /// Called when "Manual" configuration radio button is clicked.
        /// </summary>
        private void ConfigurationRadioButtonManualEventHandler(Object sender, EventArgs e)
        {
            ecFanControlRadioButtonOn.Checked = true;
            _core.SetManualMode();
            _configurationStore.SetOption(ConfigurationOption.OperationMode, OperationMode.Manual);

            SetFanControlsAvailability(false);
            SetConsistencyModeControlsAvailability(false);
            editFanButton.Enabled = false;
            SetEcFanControlsAvailability(true);

            UpdateTrayIcon(false);
        }

        /// <summary>
        /// Called when "Consistency" configuration radio button is clicked.
        /// </summary>
        private void ConfigurationRadioButtonConsistencyEventHandler(Object sender, EventArgs e)
        {
            _core.SetConsistencyMode();
            _configurationStore.SetOption(ConfigurationOption.OperationMode, OperationMode.Consistency);
            ClearManualControlConfiguration();

            SetFanControlsAvailability(false);
            SetConsistencyModeControlsAvailability(true);
            editFanButton.Enabled = false;
            SetEcFanControlsAvailability(false);

            UpdateTrayIcon(false);
        }

        /// <summary>
        /// Called when "Custom" configuration radio button is clicked.
        /// </summary>
        private void ConfigurationRadioButtonCustomEventHandler(Object sender, EventArgs e)
        {
            _core.SetConsistencyMode2();
            _configurationStore.SetOption(ConfigurationOption.OperationMode, OperationMode.Consistency2);
            ClearManualControlConfiguration();

            SetFanControlsAvailability(false);
            SetConsistencyModeControlsAvailability(false);
            editFanButton.Enabled = true;
            SetEcFanControlsAvailability(false);

            UpdateTrayIcon(false);
        }

        /// <summary>
        /// Enable or disable the manual fan control controls.
        /// </summary>
        /// <param name="enabled">Indicates whether to enable or disable the controls</param>
        private void SetFanControlsAvailability(bool enabled)
        {
            manualGroupBox.Enabled = enabled;

            if (!enabled)
            {
                manualFan1RadioButtonOff.Checked = false;
                manualFan1RadioButtonLow.Checked = false;
                manualFan1RadioButtonMedium.Checked = false;
                manualFan1RadioButtonHigh.Checked = false;
                manualFan1RadioButtonHighest.Checked = false;
                manualFan2RadioButtonOff.Checked = false;
                manualFan2RadioButtonMedium.Checked = false;
                manualFan2RadioButtonHigh.Checked = false;
            }
            else
            {
                // Disable manual fan control fields if needed.
                if (!_core.IsIndividualFanControlSupported)
                {
                    manualFan2GroupBox.Enabled = false;
                }
                if (!_state.Fan2Present)
                {
                    manualFan2GroupBox.Enabled = false;
                    manualFan2RadioButtonOff.Checked = false;
                    manualFan2RadioButtonMedium.Checked = false;
                    manualFan2RadioButtonHigh.Checked = false;
                }
            }
        }

        /// <summary>
        /// Enable or disbale the consistency mode configuration controls.
        /// </summary>
        /// <param name="enabled">Indicates whether to enable or disable the controls</param>
        private void SetConsistencyModeControlsAvailability(bool enabled)
        {
            consistencyModeGroupBox.Enabled = enabled;
        }

        /// <summary>
        /// Enable or disable the EC fan control on/off controls.
        /// </summary>
        /// <param name="enabled">Indicates whether to enable or disable the controls</param>
        private void SetEcFanControlsAvailability(bool enabled)
        {
            ecFanControlRadioButtonOn.Enabled = enabled;
            ecFanControlRadioButtonOff.Enabled = enabled;
        }

        /// <summary>
        /// Enable or disable the thermal setting controls.
        /// </summary>
        /// <param name="enabled">Indicates whether to enable or disable the controls</param>
        private void SetThermalSettingAvaiability(bool enabled)
        {
            //thermalSettingGroupBox.Enabled = enabled;

            if (!enabled)
            {
                /*
                thermalSettingRadioButtonOptimized.Checked = false;
                thermalSettingRadioButtonCool.Checked = false;
                thermalSettingRadioButtonQuiet.Checked = false;
                thermalSettingRadioButtonPerformance.Checked = false;
                */
            }
        }

        /// <summary>
        /// If the audio thread terminates, the checkbox should be unchecked to indicate as much.
        /// </summary>
        public void UncheckAudioKeepAlive()
        {
            audioKeepAliveCheckbox.Checked = false;
        }

        /// <summary>
        /// Called when the form is closed.
        /// </summary>
        private void FormClosedEventHandler(Object sender, FormClosedEventArgs e)
        {
            _formClosed = true;

            _state.WaitOne();
            _state.BackgroundThreadRunning = false; // Request termination of background thread.
            _core.StopAudioThread(); // Request termination of the audio thread.
            _state.FormClosed = true;
            _state.Release();
        }

        /// <summary>
        /// Called when the "Restart BG Thread" button is clicked.  Just starts the background thread.
        /// </summary>
        private void RestartBackgroundThreadButtonClickedEventHandler(Object sender, EventArgs e)
        {
            _core.StartBackgroundThread();
        }

        /// <summary>
        /// Called when any of the "thermal setting" radio buttons are clicked.
        /// </summary>
        private void ThermalSettingChangedEventHandler(Object sender, EventArgs e)
        {
            /*
            if (thermalSettingRadioButtonOptimized.Checked)
            {
                _core.RequestThermalSetting(ThermalSetting.Optimized);
            }
            else if (thermalSettingRadioButtonCool.Checked)
            {
                _core.RequestThermalSetting(ThermalSetting.Cool);
            }
            else if (thermalSettingRadioButtonQuiet.Checked)
            {
                _core.RequestThermalSetting(ThermalSetting.Quiet);
            }
            else if (thermalSettingRadioButtonPerformance.Checked)
            {
                _core.RequestThermalSetting(ThermalSetting.Performance);
            }
            */
        }

        private void EditFanSpeedEventHandler(Object sender, EventArgs e)
        {
            string strLogFile = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "fan.ini");
            string strLogTempl = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "fan.ini.templ");
            if (File.Exists(strLogTempl) && !File.Exists(strLogFile))
            {
                File.Copy(strLogTempl, strLogFile);
            }
            try
            {
                Process.Start("notepad.exe", strLogFile);
            }
            catch(Exception expt)
            {

            }
        }

        /// <summary>
        /// Called when the EC fan control on/off radio buttons are clicked.
        /// </summary>
        private void EcFanControlSettingChangedEventHandler(Object sender, EventArgs e)
        {
            if (ecFanControlRadioButtonOn.Checked)
            {
                _core.RequestEcFanControl(true);
                SetFanControlsAvailability(false);
                if (operationModeRadioButtonManual.Checked && _initializationComplete)
                {
                    _configurationStore.SetOption(ConfigurationOption.ManualModeEcFanControlEnabled, 1);
                    _configurationStore.SetOption(ConfigurationOption.ManualModeFan1Level, null);
                    _configurationStore.SetOption(ConfigurationOption.ManualModeFan2Level, null);
                }
            }
            else if (ecFanControlRadioButtonOff.Checked)
            {
                _core.RequestEcFanControl(false);
                if (operationModeRadioButtonManual.Checked)
                {
                    SetFanControlsAvailability(true);
                    _configurationStore.SetOption(ConfigurationOption.ManualModeEcFanControlEnabled, 0);
                }
            }
        }

        /// <summary>
        /// Called when one of the manual fan control level radio buttons is clicked.
        /// </summary>
        private void FanLevelChangedEventHandler(Object sender, EventArgs e)
        {
            // Fan 1.
            FanLevel? fan1LevelRequested = null;
            if (manualFan1RadioButtonOff.Checked)
            {
                fan1LevelRequested = FanLevel.Off;
                if (!_core.IsIndividualFanControlSupported)
                {
                    manualFan2RadioButtonOff.Checked = true;
                }
            }
            else if (manualFan1RadioButtonLow.Checked)
            {
                fan1LevelRequested = FanLevel.Low;
            }
            else if (manualFan1RadioButtonMedium.Checked)
            {
                fan1LevelRequested = FanLevel.Medium;
                if (!_core.IsIndividualFanControlSupported)
                {
                    manualFan2RadioButtonMedium.Checked = true;
                }
            }
            else if (manualFan1RadioButtonHigh.Checked)
            {
                fan1LevelRequested = FanLevel.High;
                if (!_core.IsIndividualFanControlSupported)
                {
                    manualFan2RadioButtonHigh.Checked = true;
                }
            }
            else if (manualFan1RadioButtonHighest.Checked)
            {
                fan1LevelRequested = FanLevel.SuperHigh;
            }

            if (fan1LevelRequested != null)
            {
                _core.RequestFan1Level(fan1LevelRequested);
                _configurationStore.SetOption(ConfigurationOption.ManualModeFan1Level, fan1LevelRequested);
            }

            // Fan 2.
            FanLevel? fan2LevelRequested = null;
            if (manualFan2RadioButtonOff.Checked)
            {
                fan2LevelRequested = FanLevel.Off;
            }
            else if (manualFan2RadioButtonMedium.Checked)
            {
                fan2LevelRequested = FanLevel.Medium;
            }
            else if (manualFan2RadioButtonHigh.Checked)
            {
                fan2LevelRequested = FanLevel.High;
            }

            if (fan2LevelRequested != null && _core.IsIndividualFanControlSupported)
            {
                _core.RequestFan2Level(fan2LevelRequested);
                _configurationStore.SetOption(ConfigurationOption.ManualModeFan2Level, fan2LevelRequested);
            }
        }

        /// <summary>
        /// Called when the audio device drop-down selection is changed.
        /// </summary>
        private void AudioDeviceChangedEventHandler(Object sender, EventArgs e)
        {
            _core.RequestAudioDevice((AudioDevice)audioKeepAliveComboBox.SelectedItem);

            if (audioKeepAliveComboBox.SelectedItem != null)
            {
                audioKeepAliveCheckbox.Enabled = true;
                _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveBringBackDevice, null);
            }
            else
            {
                audioKeepAliveCheckbox.Enabled = false;
            }
        }

        /// <summary>
        /// Called when the "audio keep alive" checkbox is checked or unchecked.
        /// </summary>
        private void AudioKeepAliveCheckboxChangedEventHandler(Object sender, EventArgs e)
        {
            if (audioKeepAliveCheckbox.Checked)
            {
                _core.StartAudioThread();
                _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveEnabled, 1);
                _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveSelectedDevice, ((AudioDevice)audioKeepAliveComboBox.SelectedItem).DeviceId);
                _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveBringBackDevice, null);
            }
            else
            {
                _core.StopAudioThread();

                if (!_formClosed)
                {
                    _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveEnabled, 0);
                    _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveSelectedDevice, null);

                    if (_state.BringBackAudioDevice != null)
                    {
                        _configurationStore.SetOption(ConfigurationOption.AudioKeepAliveBringBackDevice, _state.BringBackAudioDevice.DeviceId);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the consistency mode configuration text boxes are modified.
        /// </summary>
        private void ConsistencyModeTextBoxesChangedEventHandler(Object sender, EventArgs e)
        {
            // Enforce digits only in these text boxes.
            if (Regex.IsMatch(coolStopFanDelay.Text, "[^0-9]"))
            {
                coolStopFanDelay.Text = Regex.Replace(coolStopFanDelay.Text, "[^0-9]", "");
            }

            if (Regex.IsMatch(consistencyModeLowerTemperatureThresholdTextBox.Text, "[^0-9]"))
            {
                consistencyModeLowerTemperatureThresholdTextBox.Text = Regex.Replace(consistencyModeLowerTemperatureThresholdTextBox.Text, "[^0-9]", "");
            }

            if (Regex.IsMatch(consistencyModeUpperTemperatureThresholdTextBox.Text, "[^0-9]"))
            {
                consistencyModeUpperTemperatureThresholdTextBox.Text = Regex.Replace(consistencyModeUpperTemperatureThresholdTextBox.Text, "[^0-9]", "");
            }

            if (Regex.IsMatch(consistencyModeRpmThresholdTextBox.Text, "[^0-9]"))
            {
                consistencyModeRpmThresholdTextBox.Text = Regex.Replace(consistencyModeRpmThresholdTextBox.Text, "[^0-9]", "");
            }

            CheckConsistencyModeOptionsConsistency();
        }

        private void CpuFreqChangedEventHandler(Object sender, EventArgs e)
        {
            bool success = false;

            // P-cores
            if (Regex.IsMatch(ACCpuFreq1TextBox.Text, "[^0-9]"))
            {
                ACCpuFreq1TextBox.Text = Regex.Replace(ACCpuFreq1TextBox.Text, "[^0-9]", "");
            }
            success = (ACCpuFreq1TextBox.Text == "") || int.TryParse(ACCpuFreq1TextBox.Text, out int ACCpuFreq1);

            if (Regex.IsMatch(DCCpuFreq1TextBox.Text, "[^0-9]"))
            {
                DCCpuFreq1TextBox.Text = Regex.Replace(DCCpuFreq1TextBox.Text, "[^0-9]", "");
            }
            success = (DCCpuFreq1TextBox.Text == "") || int.TryParse(DCCpuFreq1TextBox.Text, out int DCCpuFreq1);

            // E-cores
            if (Regex.IsMatch(ACCpuFreqTextBox.Text, "[^0-9]"))
            {
                ACCpuFreqTextBox.Text = Regex.Replace(ACCpuFreqTextBox.Text, "[^0-9]", "");
            }
            success = (ACCpuFreqTextBox.Text == "") || int.TryParse(ACCpuFreqTextBox.Text, out int ACCpuFreq);

            if (Regex.IsMatch(DCCpuFreqTextBox.Text, "[^0-9]"))
            {
                DCCpuFreqTextBox.Text = Regex.Replace(DCCpuFreqTextBox.Text, "[^0-9]", "");
            }
            success = (DCCpuFreqTextBox.Text == "") || int.TryParse(DCCpuFreqTextBox.Text, out int DCCpuFreq);

            ApplyCpuFreqButton.Enabled = success;
        }

        private void ApplyCpuFreqButtonClickedEventHandler(Object sender, EventArgs e)
        {
            // Create a new task using existing xml description
            try
            {
                // P-cores
                /*
                Process p = new Process();
                p.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
                p.StartInfo.Arguments = "  /setDCvalueindex scheme_current SUB_PROCESSOR PROCFREQMAX1 " + (DCCpuFreq1TextBox.Text == "" ? "0" : DCCpuFreq1TextBox.Text);
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                p.WaitForExit();
                p.Close();

                p.StartInfo.Arguments = "  /setACvalueindex scheme_current SUB_PROCESSOR PROCFREQMAX1 " + (ACCpuFreq1TextBox.Text == "" ? "0" : ACCpuFreq1TextBox.Text);
                p.Start();
                p.WaitForExit();
                p.Close();

                // E-cores
                p.StartInfo.Arguments = "  /setDCvalueindex scheme_current SUB_PROCESSOR PROCFREQMAX " + (DCCpuFreqTextBox.Text == "" ? "0" : DCCpuFreqTextBox.Text);
                p.Start();
                p.WaitForExit();
                p.Close();

                p.StartInfo.Arguments = "  /setACvalueindex scheme_current SUB_PROCESSOR PROCFREQMAX " + (ACCpuFreqTextBox.Text == "" ? "0" : ACCpuFreqTextBox.Text);
                p.Start();
                p.WaitForExit();
                p.Close();

                // Apply
                p.StartInfo.Arguments = "  /setactive scheme_current";
                p.Start();
                p.WaitForExit();
                p.Close();
                */
                Guid activePlan = PowerManager.GetActivePlan();
                uint acFreq1 = (ACCpuFreq1TextBox.Text == "") ? 0 : uint.Parse(ACCpuFreq1TextBox.Text);
                uint dcFreq1 = (DCCpuFreq1TextBox.Text == "") ? 0 : uint.Parse(DCCpuFreq1TextBox.Text);
                uint acFreq = (ACCpuFreqTextBox.Text == "") ? 0 : uint.Parse(ACCpuFreqTextBox.Text);
                uint dcFreq = (DCCpuFreqTextBox.Text == "") ? 0 : uint.Parse(DCCpuFreqTextBox.Text);
                // assign freq. for P/E cores
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX1, PowerMode.AC, acFreq1);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX1, PowerMode.DC, dcFreq1);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX, PowerMode.AC, acFreq);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCFREQMAX, PowerMode.DC, dcFreq);
                // if we'd limit the freq. for P/E cores, we'll have to set the minimize processor state as 5% better, and maximum processor state as 100%,
                // otherwise, above assigned freq. wouldn't be applied as well.
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMAX1, PowerMode.AC, 100);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMAX1, PowerMode.DC, 100);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMAX, PowerMode.AC, 100);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMAX, PowerMode.DC, 100);

                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMIN1, PowerMode.AC, 100);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMIN1, PowerMode.DC, 100);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMIN, PowerMode.AC, 5);
                PowerManager.SetPlanSetting(activePlan, SettingSubgroup.PROCESSOR_SETTINGS_SUBGROUP, Setting.PROCTHROTTLEMIN, PowerMode.DC, 5);

                // active current scheme
                PowerManager.SetActivePlan(activePlan);

                bool success = int.TryParse(ACCpuFreq1TextBox.Text, out int ACCpuFreq1);
                //if (success)
                {
                    _configurationStore.SetOption(ConfigurationOption.ACCpuFreq1, ACCpuFreq1);
                }
                success = int.TryParse(DCCpuFreq1TextBox.Text, out int DCCpuFreq1);
                //if (success)
                {
                    _configurationStore.SetOption(ConfigurationOption.DCCpuFreq1, DCCpuFreq1);
                }

                success = int.TryParse(ACCpuFreqTextBox.Text, out int ACCpuFreq);
                //if (success)
                {
                    _configurationStore.SetOption(ConfigurationOption.ACCpuFreq, ACCpuFreq);
                }
                success = int.TryParse(DCCpuFreqTextBox.Text, out int DCCpuFreq);
                //if (success)
                {
                    _configurationStore.SetOption(ConfigurationOption.DCCpuFreq, DCCpuFreq);
                }

                ApplyCpuFreqButton.Enabled = false;
            }
            catch (Exception)
            {
                // do nothing
                // do nothing
            }
        }

        private void AllowBacklightCheckBoxChangedEventHandler(Object sender, EventArgs e)
        {
            if (Regex.IsMatch(bkgLightDelay.Text, "[^0-9]"))
            {
                bkgLightDelay.Text = Regex.Replace(bkgLightDelay.Text, "[^0-9]", "");
            }

            bool enableBacklight = allowBacklightCheckBox.Checked;
            bkgLightDelay.Enabled = enableBacklight;

            bool success = int.TryParse(bkgLightDelay.Text, out int backLightDelay);
            //bool enableStopFan = stopFanCheckbox.Checked;
            if (success)
            {
                if (backLightDelay < 2)
                {
                    bkgLightDelay.Text = "2";
                    backLightDelay = 2;
                }
                _configurationStore.SetOption(ConfigurationOption.AllowBacklightDelay, enableBacklight ? 1 : 0);
                _configurationStore.SetOption(ConfigurationOption.BacklightDelay, backLightDelay);
                _state.EnableBacklight(enableBacklight);
                _state.SetBacklightDelay(backLightDelay);
            }
        }

        /// <summary>
        /// Called when the consistency mode "Apply changes" button is clicked.
        /// </summary>
        private void ConsistencyApplyChangesButtonClickedEventHandler(Object sender, EventArgs e)
        {
            WriteConsistencyModeConfiguration();
        }

        /// <summary>
        /// Called when the "tray icon" checkbox is clicked.
        /// </summary>
        private void TrayIconCheckBoxChangedEventHandler(Object sender, EventArgs e)
        {
            UpdateTrayIcon(false);
            animatedCheckBox.Enabled = trayIconCheckBox.Checked;

            _configurationStore.SetOption(ConfigurationOption.TrayIconEnabled, trayIconCheckBox.Checked ? 1 : 0);
        }

        /// <summary>
        /// Called when the "animated" checkbox is clicked.
        /// </summary>
        private void AnimatedCheckBoxChangedEventHandler(Object sender, EventArgs e)
        {
            UpdateTrayIcon(false);

            _configurationStore.SetOption(ConfigurationOption.TrayIconAnimationEnabled, animatedCheckBox.Checked ? 1 : 0);
        }

        private void LogCheckBoxChangedEventHandler(Object sender, EventArgs e)
        {
            Log.AllowingLogWriteToFile = logCheckBox.Checked ? true : false;
            _configurationStore.SetOption(ConfigurationOption.AllowLogWriteToFile, logCheckBox.Checked ? 1 : 0);
        }

        private void StartupCheckBoxChangedEventHandler(Object sender, EventArgs e)
        {
            _configurationStore.SetOption(ConfigurationOption.StartupEnabled, startupCheckBox.Checked ? 1 : 0);
            string xmlPath = string.Format("{0}{1}{2}", Directory.GetCurrentDirectory(), Path.DirectorySeparatorChar, "task.xml");
            if (startupCheckBox.Checked)
            {
                if (File.Exists(xmlPath))
                {
                    // Replace {exe_path} with current path
                    string filename = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
                    string curDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    string filePath = string.Format("\"{0}{1}{2}.exe\"", Directory.GetCurrentDirectory(), Path.DirectorySeparatorChar, filename);
                    StreamReader sr = File.OpenText(xmlPath);
                    string xmlContent = sr.ReadToEnd().Replace("{exe_path}", filePath);
                    sr.Close();
                    StreamWriter sw = File.CreateText(xmlPath);
                    sw.Write(xmlContent);
                    sw.Close();

                    // Create a new task using existing xml description
                    try
                    {
                        Process p = new Process();
                        p.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
                        p.StartInfo.Arguments = " /create /xml \"" + xmlPath + "\" /tn \"LenovoFanManagement\" -f";
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.Start();
                        p.WaitForExit();
                        p.Close();
                    }catch (Exception)
                    {
                        // do nothing
                    }
                }
            }
            else
            {
                // Delete existing task
                try
                {
                    Process p = new Process();
                    p.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
                    p.StartInfo.Arguments = " /delete /tn \"LenovoFanManagement\" -f";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    p.WaitForExit();
                    p.Close();
                }catch (Exception)
                {
                    // do nothing
                }
            }
            /*
            RegistryKey _registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (startupCheckBox.Checked)
            {
                string filename = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
                string filePath = string.Format("\"{0}{1}{2}.exe\"", Directory.GetCurrentDirectory(), Path.DirectorySeparatorChar, filename);
                _registryKey.SetValue(STARTUP_NAME, filePath);
            }
            else
            {
                if (_registryKey.GetValue(STARTUP_NAME, null) != null)
                    _registryKey.DeleteValue(STARTUP_NAME);
            }
            _registryKey.Close();
            */
        }

        private void GpuTCheckBoxChangedEventHandler(object sender, EventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            _configurationStore.SetOption(ConfigurationOption.GpuTEnabled, gpuTCheckBox.Checked ? 1 : 0);
            if (chk.Focused == false) return;

            MessageBox.Show("改动后请手动重启程序！");
            /*
            DialogResult dialogResult = MessageBox.Show("改动后请手动重启程序！", "确认", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                //System.Diagnostics.Process.Start(Application.ExecutablePath);
                //Application.Exit();
                ProcessStartInfo Info = new ProcessStartInfo();
                Info.Arguments = "/C ping 127.0.0.1 -n 2 && \"" + Application.ExecutablePath + "\"";
                Info.WindowStyle = ProcessWindowStyle.Hidden;
                Info.CreateNoWindow = true;
                Info.FileName = "cmd.exe";
                Process.Start(Info);
                Application.Exit();
            }
            */
        }

        // OBSOLETEDv
        private void _UpdateWatermarkRegistry(string val=@"%SystemRoot%\system32\explorerframe.dll")
        {
            string originalString = val;
            byte[] bytes = Encoding.Unicode.GetBytes(originalString);
            string hexString = BitConverter.ToString(bytes).Replace("-", ",") + ",00,00";

            var registryFilePath = $"{Directory.GetCurrentDirectory()}\\watermark.reg";
            if (File.Exists(registryFilePath))
            {
                File.Delete(registryFilePath);
            }
            string registryStr =
                "﻿Windows Registry Editor Version 5.00\r\n" +
                "\r\n" +
                @"[HKEY_CLASSES_ROOT\CLSID\{ab0b37ec-56f6-4a0e-a8fd-7a8bf7c2da96}\InProcServer32]" +
                "\r\n@=hex(2):"+ hexString+ "\r\n" +
                "\r\n";

            // 替换
            File.WriteAllText(registryFilePath, registryStr);

            Process regeditProcess = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "regedt32.exe"), $"/s {registryFilePath}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            regeditProcess.WaitForExit();
        }

        private void RestartExplorer()
        {
            foreach (Process p in Process.GetProcesses())
            {
                // In case we get Access Denied
                try
                {
                    if (p.MainModule.FileName.ToLower().EndsWith(":\\windows\\explorer.exe"))
                    {
                        p.Kill();
                        break;
                    }
                }
                catch
                { }
            }
            Process.Start("explorer.exe");
        }

        private void UpdateWatermarkRegistry(string val = @"%SystemRoot%\system32\explorerframe.dll")
        {
            string originalString = val;
            byte[] bytes = Encoding.Unicode.GetBytes(originalString);
            string hexString = BitConverter.ToString(bytes).Replace("-", ",") + ",00,00";

            try
            {
                /* Get the ID of the current user (aka Amin)
                 */
                WindowsIdentity id = WindowsIdentity.GetCurrent();

                /* Add the TakeOwnership Privilege
                 */
                bool blRc = natif.MySetPrivilege(natif.TakeOwnership, true);
                if (!blRc)
                    throw new PrivilegeNotHeldException(natif.TakeOwnership);

                /* Add the Restore Privilege (must be done to change the owner)
                 */
                blRc = natif.MySetPrivilege(natif.Restore, true);
                if (!blRc)
                    throw new PrivilegeNotHeldException(natif.Restore);

                /* Open a registry which I don't own
                 */
                RegistryKey rkADSnapInsNodesTypes = Registry.ClassesRoot.OpenSubKey(@"CLSID\{ab0b37ec-56f6-4a0e-a8fd-7a8bf7c2da96}\InProcServer32", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership);
                RegistrySecurity regSecTempo = rkADSnapInsNodesTypes.GetAccessControl(AccessControlSections.All);

                /* Get the real owner
                 */
                IdentityReference oldId = regSecTempo.GetOwner(typeof(SecurityIdentifier));
                SecurityIdentifier siTrustedInstaller = new SecurityIdentifier(oldId.ToString());
                Console.WriteLine(oldId.ToString());

                /* process user become the owner
                 */
                regSecTempo.SetOwner(id.User);
                rkADSnapInsNodesTypes.SetAccessControl(regSecTempo);

                /* Add the full control
                 */
                RegistryAccessRule regARFullAccess = new RegistryAccessRule(id.User, RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow);
                regSecTempo.AddAccessRule(regARFullAccess);
                rkADSnapInsNodesTypes.SetAccessControl(regSecTempo);

                /* What I have TO DO
                 */
                //rkADSnapInsNodesTypes.DeleteSubKey("{3bcd9db8-f84b-451c-952f-6c52b81f9ec6}");
                // Open same key again with SetValue permission
                RegistryKey rw = Registry.ClassesRoot.OpenSubKey(@"CLSID\{ab0b37ec-56f6-4a0e-a8fd-7a8bf7c2da96}\InProcServer32", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.SetValue);
                rw.SetValue("", val, RegistryValueKind.ExpandString);

                /* Put back the original owner
                 */
                //regSecTempo.SetOwner(siFinalOwner);
                regSecTempo.SetOwner(siTrustedInstaller);
                rkADSnapInsNodesTypes.SetAccessControl(regSecTempo);

                /* Put back the original Rights
                 */
                regSecTempo.RemoveAccessRule(regARFullAccess);
                rkADSnapInsNodesTypes.SetAccessControl(regSecTempo);
            }
            catch (Exception excpt)
            {
                throw excpt;
            }
        }

        private void HideWatermarkCheckBoxChangedEventHandler(object sender, EventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            _configurationStore.SetOption(ConfigurationOption.HideWatermarkEnabled, hideWatermarkCheckBox.Checked ? 1 : 0);
            if (chk.Focused == false) 
                return;

            //RegistryKey _registryKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\{ab0b37ec-56f6-4a0e-a8fd-7a8bf7c2da96}\InProcServer32", 
            //    RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.ReadPermissions);
            string msg = "这将关闭当前所有资源管理器, 并重启生效!";
            DialogResult dialogResult = MessageBox.Show(msg, "确认", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.No)
            {
                return;
            }

            if (hideWatermarkCheckBox.Checked)
            {
                string filename = "painter_x64.dll";
                string curDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                string filePath = string.Format("{0}{1}{2}", curDir, Path.DirectorySeparatorChar, filename);
                UpdateWatermarkRegistry(filePath);
            }
            else
            {
                UpdateWatermarkRegistry(@"%SystemRoot%\system32\explorerframe.dll");
            }
            RestartExplorer();
            //_registryKey.Close();
        }

        private void ChargeThresholdSelectEventHandler(object sender, EventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            //if (!cb.Focused)
            {
                if (sender == chargeStopThresholdComboBox)
                {
                    _prevStopChargeIndex = cb.SelectedIndex;
                } else
                {
                    _prevStartChargeIndex = cb.SelectedIndex;
                }
                //return;
            }
            
            string val_str = cb.GetItemText(cb.Items[cb.SelectedIndex]);
            int pct = int.Parse(val_str.Replace("%", ""));

            if (sender == chargeStopThresholdComboBox)
            {
                if (_disabledStopChargeItems.IndexOf(cb.SelectedIndex) >= 0)
                {
                    chargeStopThresholdComboBox.Parent.Focus();
                    chargeStopThresholdComboBox.SelectedIndex = _prevStopChargeIndex;
                    return;
                }

                _prevStopChargeIndex = cb.SelectedIndex;
                _disabledStartChargeItems.RemoveAll(item => true);
                int max_pct_start_allowed = 0;
                for (int i = 0; i < chargeStartThresholdComboBox.Items.Count; i++)
                {
                    string text = chargeStartThresholdComboBox.GetItemText(chargeStartThresholdComboBox.Items[i]);
                    int cur_pct = int.Parse(text.Replace("%", ""));
                    if (cur_pct >= pct)
                    {
                        _disabledStartChargeItems.Add(i);
                    }
                    else
                    {
                        max_pct_start_allowed = Math.Max(cur_pct, max_pct_start_allowed);
                    }
                }
                int start_idx = chargeStartThresholdComboBox.SelectedIndex;
                if (start_idx == -1)
                {
                    start_idx = 0;
                }
                string var_start = chargeStartThresholdComboBox.GetItemText(chargeStartThresholdComboBox.Items[start_idx]);
                int pct_start = int.Parse(var_start.Replace("%", ""));
                if (pct_start >= pct)
                {
                    start_idx = chargeStartThresholdComboBox.FindStringExact(string.Format("{0}%", max_pct_start_allowed));
                    //if (start_idx != -1)
                    {
                        chargeStartThresholdComboBox.SelectedIndex = start_idx;
                    }
                    _battery.SetChargeStartPercentage(max_pct_start_allowed);
                }

                _battery.SetChargeStopPercentage(pct);
            }
            else
            {
                if (_disabledStartChargeItems.IndexOf(cb.SelectedIndex) >= 0)
                {
                    chargeStartThresholdComboBox.Parent.Focus();
                    chargeStartThresholdComboBox.SelectedIndex = _prevStartChargeIndex;
                    return;
                }

                _prevStartChargeIndex = cb.SelectedIndex;
                _disabledStopChargeItems.RemoveAll(item => true);
                for (int i = 0; i < chargeStopThresholdComboBox.Items.Count; i++)
                {
                    string text = chargeStopThresholdComboBox.GetItemText(chargeStopThresholdComboBox.Items[i]);
                    int cur_pct = int.Parse(text.Replace("%", ""));
                    if (cur_pct <= pct)
                    {
                        _disabledStopChargeItems.Add(i);
                    }
                }
                _battery.SetChargeStartPercentage(pct);
            }
            //if (chargeStopThresholdComboBox.SelectedIndex < 0) return;

            //string val_str = chargeStopThresholdComboBox.GetItemText(chargeStopThresholdComboBox.Items[chargeStopThresholdComboBox.SelectedIndex]);
            //byte val = Convert.ToByte(val_str.Replace("%", ""));
            //_battery.SetChargeThreshold(val);
        }

        private void ChargeStartControlChangedEventHandler(object sender , EventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (!cb.Focused) return;

            _battery.EnableChargeStart(cb.Checked);
            chargeStartThresholdComboBox.Enabled = cb.Checked;
        }

        private void ChargeStopControlChangedEventHandler(object sender, EventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (!cb.Focused) return;

            _battery.EnableChargeStop(cb.Checked);
            chargeStopThresholdComboBox.Enabled = cb.Checked;
        }

        private void ChargeStartThresholdComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1) return;

            if (_disabledStartChargeItems.IndexOf(e.Index) >= 0 ) //We are disabling item based on Index, you can have your logic here
            {
                e.Graphics.DrawString(chargeStartThresholdComboBox.Items[e.Index].ToString(), e.Font, Brushes.LightGray, e.Bounds);
            }
            else
            {
                e.DrawBackground();
                e.Graphics.DrawString(chargeStartThresholdComboBox.Items[e.Index].ToString(), e.Font, Brushes.Black, e.Bounds);
                e.DrawFocusRectangle();
            }
        }

        private void ChargeStopThresholdComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1) return;

            if (_disabledStopChargeItems.IndexOf(e.Index) >= 0) //We are disabling item based on Index, you can have your logic here
            {
                e.Graphics.DrawString(chargeStopThresholdComboBox.Items[e.Index].ToString(), e.Font, Brushes.LightGray, e.Bounds);
            }
            else
            { 
                e.DrawBackground();
                e.Graphics.DrawString(chargeStopThresholdComboBox.Items[e.Index].ToString(), e.Font, Brushes.Black, e.Bounds);
                e.DrawFocusRectangle();
            }
        }

        /// <summary>
        /// Called when the tray icon is clicked. Restores the window and makes it visible in the task bar.
        /// </summary>
        private void TrayIconOnClickEventHandler(object sender, EventArgs e)
        {
            Visible = true;
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            if (trayIconCheckBox.Checked)
                WindowState = FormWindowState.Minimized;
        }

        /// <summary>
        /// Called when the window is resized. If the window is minimized and the "tray icon" is visible, then the
        /// window is hidden in the taskbar.
        /// </summary>
        private void OnResizeEventHandler(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && trayIcon.Visible)
            {
                ShowInTaskbar = false;
                Visible = false;
            }
        }

        /// <summary>
        /// Check to see if the GUI consistency mode options text boxes match the currently stored configuration, and
        /// enable or disable the "apply changes" button accordingly.
        /// </summary>
        private void CheckConsistencyModeOptionsConsistency()
        {
            bool result = false;

            if (consistencyModeLowerTemperatureThresholdTextBox.Text != _core.LowerTemperatureThreshold.ToString() ||
                consistencyModeUpperTemperatureThresholdTextBox.Text != _core.UpperTemperatureThreshold.ToString() ||
                consistencyModeRpmThresholdTextBox.Text != _core.RpmThreshold.ToString() ||
                stopFanCheckbox.Checked != _core.EnableStopFan ||
                coolStopFanDelay.Text != _core.CpuCoolDelay.ToString())
            {
                // Configuration doesn't match.  Check for flip-flop.
                bool success = int.TryParse(consistencyModeLowerTemperatureThresholdTextBox.Text, out int lowerTemperatureThreshold);
                if (success)
                {
                    success = int.TryParse(consistencyModeUpperTemperatureThresholdTextBox.Text, out int upperTemperatureThreshold);
                    if (success)
                    {
                        if (upperTemperatureThreshold >= lowerTemperatureThreshold)
                        {
                            // Looks good, we can enable the button.
                            result = true;
                        }
                    }
                }

                success = int.TryParse(coolStopFanDelay.Text, out int cpuCoolDelay);
                if (!success) result = false;
            }

            bool enableStopFan = stopFanCheckbox.Checked;
            coolStopFanDelay.Enabled = enableStopFan;

            consistencyModeApplyChangesButton.Enabled = result;
        }

        /// <summary>
        /// Take the consistency mode configuration and save it to the core.
        /// </summary>
        private void WriteConsistencyModeConfiguration()
        {
            bool success = int.TryParse(consistencyModeLowerTemperatureThresholdTextBox.Text, out int lowerTemperatureThreshold);
            if (success)
            {
                success = int.TryParse(consistencyModeUpperTemperatureThresholdTextBox.Text, out int upperTemperatureThreshold);
                if (success)
                {
                    success = int.TryParse(consistencyModeRpmThresholdTextBox.Text, out int rpmThreshold);
                    if (success)
                    {
                        success = int.TryParse(coolStopFanDelay.Text, out int cpuCoolDelay);
                        bool enableStopFan = stopFanCheckbox.Checked;
                        if (success)
                        {
                            _core.WriteConsistencyModeConfiguration(lowerTemperatureThreshold, upperTemperatureThreshold, rpmThreshold, enableStopFan, cpuCoolDelay);

                            _configurationStore.SetOption(ConfigurationOption.ConsistencyModeLowerTemperatureThreshold, lowerTemperatureThreshold);
                            _configurationStore.SetOption(ConfigurationOption.ConsistencyModeUpperTemperatureThreshold, upperTemperatureThreshold);
                            _configurationStore.SetOption(ConfigurationOption.ConsistencyModeRpmThreshold, rpmThreshold);
                            _configurationStore.SetOption(ConfigurationOption.StopFanEnabled, enableStopFan ? 1 : 0);
                            _configurationStore.SetOption(ConfigurationOption.CpuCoolDelay, cpuCoolDelay);

                            CheckConsistencyModeOptionsConsistency();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load system tray icons.
        /// </summary>
        private void LoadTrayIcons()
        {
            int globalIndex = 0;

            foreach (string color in new string[] { "Grey", "Blue", "Red" })
            {
                for (int index = 1; index <= 16; index++)
                {
                    _trayIcons[globalIndex++] = new Icon(string.Format(@"Resources\Fan-{0}-{1}.ico", color, index));
                }
            }
        }

        /// <summary>
        /// Update the system tray icon.
        /// </summary>
        /// <param name="advance">Whether or not to advance a frame</param>
        private void UpdateTrayIcon(bool advance)
        {
            // Actually, hide tray icon if it is not enabled.
            trayIcon.Visible = trayIconCheckBox.Checked;

            if (trayIconCheckBox.Checked)
            {
                int offset = _core.TrayIconColor switch
                {
                    TrayIconColor.Gray => 0,
                    TrayIconColor.Blue => 16,
                    TrayIconColor.Red => 32,
                    _ => 0
                };

                if (animatedCheckBox.Checked)
                {
                    if (advance)
                    {
                        _trayIconIndex = (_trayIconIndex + 1) % (_trayIcons.Length / 3);
                    }
                }
                else
                {
                    _trayIconIndex = 0;
                }

                Icon newIcon = _trayIcons[_trayIconIndex + offset];
                if (trayIcon.Icon != newIcon)
                {
                    trayIcon.Icon = newIcon;
                }
            }
        }

        /// <summary>
        /// Update the system tray icon (advance one frame).
        /// </summary>
        private void UpdateTrayIcon()
        {
            UpdateTrayIcon(true);
        }

        /// <summary>
        /// Kicks off the thread that handles the tray icon animation.
        /// </summary>
        private void StartTrayIconThread()
        {
            new Thread(new ThreadStart(TrayIconThread)).Start();
        }

        /// <summary>
        /// Update the tray icon, changing speed with the fan RPM.
        /// </summary>
        private void TrayIconThread()
        {
            try
            {
                MethodInvoker updateInvoker = new(UpdateTrayIcon);

                while (!_formClosed)
                {
                    int waitTime = 1000; // One second.

                    if (trayIconCheckBox.Checked && animatedCheckBox.Checked)
                    {
                        // Grab state information that we need.
                        uint? averageRpm;
                        if (_state.Fan2Present)
                        {
                            averageRpm = (_state.Fan1Rpm + _state.Fan2Rpm) / 2;
                        }
                        else
                        {
                            averageRpm = _state.Fan1Rpm;
                        }

                        if (averageRpm > 250 && averageRpm < 10000)
                        {
                            try
                            {
                                BeginInvoke(updateInvoker);
                            }
                            catch (Exception)
                            {
                                // If the window handle is not here (not open yet, or closing), there could be an error.
                                // Silently ignore.
                            }

                            // Higher RPM = lower wait time = faster animation.
                            waitTime = 250000 / (int)averageRpm;
                        }
                    }

                    Thread.Sleep(Math.Min(waitTime, 1000));
                }
            }
            catch (Exception exception)
            {
                Log.Write(exception);
            }
        }

        /// <summary>
        /// Shows a disclaimer message to the user.
        /// </summary>
        private static void ShowDisclaimer()
        {
            //MessageBox.Show("Note: While every has been made to make this program safe to use, it does interact with the embedded controller and system BIOS using undocumented methods and may have adverse effects on your system.  Use at your own risk.  If you experience odd behavior, a full system shutdown should restore everything back to the original state.  This program is not created by or affiliated with Dell Inc. or Dell Technologies Inc.", "Dell Fan Management – Disclaimer");
            MessageBox.Show("注意：虽然已采取一切措施使该程序可以安全使用，但它确实使用未记录的方法与EC(嵌入式控制器)和系统 BIOS 进行交互，并且可能会对您的系统产生不利影响。 使用风险自负。 如果您遇到奇怪的行为，请关机或重启系统恢复到原始状态。 此程序并非由 Lenovo Inc. 或 Lenovo Technologies Inc. 创建或附属于 Lenovo Inc. 或 Lenovo Technologies Inc.”、“Lenovo Fan Management – 免责声明");
        }

        private void forceStopCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void DellFanManagementGuiForm_Load(object sender, EventArgs e)
        {

        }

        private void manualGroupBox_Enter(object sender, EventArgs e)
        {

        }

        private void animatedCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
