﻿using DellFanManagement.App.ConsistencyModeHandlers;
using DellFanManagement.App.FanControllers;
using DellFanManagement.DellSmbiosSmiLib;
using System;
using System.Threading;
using System.Windows.Forms;

namespace DellFanManagement.App
{
    public class Core
    {
        /// <summary>
        /// How often to refresh the system state, in milliseconds.
        /// </summary>
        private static readonly int RefreshInterval = 1000;

        /// <summary>
        /// RPM values above this are most likely bogus.
        /// </summary>
        public static readonly ulong RpmSanityCheck = 6500;

        /// <summary>
        /// Shared object which contains the state of the application.
        /// </summary>
        private readonly State _state;

        /// <summary>
        /// Form object running the application.
        /// </summary>
        private readonly DellFanManagementGuiForm _form;

        /// <summary>
        /// Fan controller for making fan speed adjustments.
        /// </summary>
        private readonly FanController _fanController;

        /// <summary>
        /// Logic for handling "consistency mode".
        /// </summary>
        private readonly ConsistencyModeHandler _consistencyModeHandler;

        /// <summary>
        /// Used to play back sounds in the application.
        /// </summary>
        private SoundPlayer _soundPlayer;

        /// <summary>
        /// Block state change requests while a state update is in progress.
        /// </summary>
        private readonly Semaphore _requestSemaphore;

        /// <summary>
        /// Indicates whether or not the user has requested that EC fan control be enabled.
        /// </summary>
        private bool _ecFanControlRequested;

        /// <summary>
        /// User requested level for fan 1.
        /// </summary>
        private FanLevel? _fan1LevelRequested;

        /// <summary>
        /// User requested level for fan 2.
        /// </summary>
        private FanLevel? _fan2LevelRequested;

        public bool EnableStopFan;
        public int? CpuCoolDelay { get; private set; }

        /// <summary>
        /// Lower temperature threshold for consistency mode.
        /// </summary>
        public int? LowerTemperatureThreshold { get; private set; }

        /// <summary>
        /// Upper temperature threshold for consistency mode.
        /// </summary>
        public int? UpperTemperatureThreshold { get; private set; }

        /// <summary>
        /// Fan RPM threshold for consistency mode.
        /// </summary>
        public ulong? RpmThreshold { get; private set; }

        public TrayIconColor TrayIconColor { get; set; }

        /// <summary>
        /// Thermal setting that has been requested by the user but not yet applied.
        /// </summary>
        public ThermalSetting? RequestedThermalSetting { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="state">Shared state object</param>
        /// <param name="form">Form object (hosting the application)</param>
        public Core(State state, DellFanManagementGuiForm form)
        {
            _state = state;
            _form = form;
            _fanController = FanControllerFactory.GetFanFanController();
            _consistencyModeHandler = ConsistencyModeHandlerFactory.GetConsistencyModeHandler(this, _state, _fanController);
            _soundPlayer = null;
            _requestSemaphore = new(1, 1);

            RequestedThermalSetting = null;
            _ecFanControlRequested = true;
            _fan1LevelRequested = null;
            _fan2LevelRequested = null;

            LowerTemperatureThreshold = null;
            UpperTemperatureThreshold = null;
            RpmThreshold = null;
            EnableStopFan = false;
            CpuCoolDelay = 10;
            TrayIconColor = TrayIconColor.Gray;
        }

        /// <summary>
        /// Switch configuration to automatic mode.
        /// </summary>
        public void SetAutomaticMode()
        {
            _state.WaitOne();
            _state.OperationMode = OperationMode.Automatic;
            _state.ConsistencyModeStatus = " ";
            _state.Release();
            TrayIconColor = TrayIconColor.Gray;
        }

        /// <summary>
        /// Switch configuration to manual mode.
        /// </summary>
        public void SetManualMode()
        {
            _state.WaitOne();
            _state.OperationMode = OperationMode.Manual;
            _ecFanControlRequested = _state.EcFanControlEnabled;
            _state.ConsistencyModeStatus = " ";
            _state.Fan1Level = null;
            _state.Fan2Level = null;
            _fan1LevelRequested = null;
            _fan2LevelRequested = null;
            _state.Release();
            TrayIconColor = TrayIconColor.Gray;
        }

        /// <summary>
        /// Switch configuration to consistency mode.
        /// </summary>
        public void SetConsistencyMode()
        {
            _state.WaitOne();
            _state.OperationMode = OperationMode.Consistency;
            _state.Release();
        }

        /// <summary>
        /// Request that EC fan control be enabled or disabled.
        /// </summary>
        /// <param name="enabled">True to enable EC fan control, false to disable it</param>
        public void RequestEcFanControl(bool enabled)
        {
            _requestSemaphore.WaitOne();
            _ecFanControlRequested = enabled;
            _requestSemaphore.Release();
        }

        /// <summary>
        /// Requested a specific fan level for level 1.
        /// </summary>
        /// <param name="level">Fan level to set</param>
        public void RequestFan1Level(FanLevel? level)
        {
            _requestSemaphore.WaitOne();
            _fan1LevelRequested = level;
            _requestSemaphore.Release();
        }

        /// <summary>
        /// Requested a specific fan level for level 2.
        /// </summary>
        /// <param name="level">Fan level to set</param>
        public void RequestFan2Level(FanLevel? level)
        {
            _requestSemaphore.WaitOne();
            _fan2LevelRequested = level;
            _requestSemaphore.Release();
        }

        /// <summary>
        /// Request that the thermal setting be updated.
        /// </summary>
        /// <param name="requestedThermalSetting">Requested thermal setting</param>
        public void RequestThermalSetting(ThermalSetting requestedThermalSetting)
        {
            _requestSemaphore.WaitOne();

            if (requestedThermalSetting != _state.ThermalSetting)
            {
                RequestedThermalSetting = requestedThermalSetting;
            }

            _requestSemaphore.Release();
        }

        /// <summary>
        /// Set the state audio device for the audio keep-alive thread.
        /// </summary>
        /// <param name="device">Selected audio device</param>
        public void RequestAudioDevice(AudioDevice device)
        {
            bool activeAudioDeviceChanged = false;

            _state.WaitOne();

            if (device != null)
            {
                _state.BringBackAudioDevice = null;

                if (device != _state.SelectedAudioDevice && _state.AudioThreadRunning)
                {
                    activeAudioDeviceChanged = true;
                }
            }

            _state.SelectedAudioDevice = device;

            _state.Release();

            if (activeAudioDeviceChanged)
            {
                StopAudioThread();
            }
        }

        /// <summary>
        /// Start up the application "background thread", which monitors the system state.
        /// </summary>
        public void StartBackgroundThread()
        {
            new Thread(new ThreadStart(BackgroundThread)).Start();
        }

        /// <summary>
        /// The background thread runs in a loop.  It collects RPM and temperature data and handles the program's main
        /// background behavior.
        /// </summary>
        private void BackgroundThread()
        {
            _state.WaitOne();
            _state.BackgroundThreadRunning = true;
            _state.Release();

            bool releaseSemaphore = false;

            try
            {
                if (_state.EcFanControlEnabled && IsAutomaticFanControlDisableSupported)
                {
                    _fanController.EnableAutomaticFanControl();
                    Log.Write("Enabled EC fan control – startup");
                }

                while (_state.BackgroundThreadRunning)
                {
                    _state.WaitOne();
                    _requestSemaphore.WaitOne();
                    releaseSemaphore = true;

                    // Update state.
                    _state.Update();

                    // Take action based on configuration.
                    if (_state.OperationMode == OperationMode.Automatic)
                    {
                        if (!_state.EcFanControlEnabled && IsAutomaticFanControlDisableSupported)
                        {
                            _state.EcFanControlEnabled = true;
                            _fanController.EnableAutomaticFanControl();
                            Log.Write("Enabled EC fan control – automatic mode");
                        }
                    }
                    else if (_state.OperationMode == OperationMode.Manual && IsAutomaticFanControlDisableSupported && IsSpecificFanControlSupported)
                    {
                        // Check for EC control state changes that need to be applied.
                        if (_ecFanControlRequested && !_state.EcFanControlEnabled)
                        {
                            _state.EcFanControlEnabled = true;
                            _fanController.EnableAutomaticFanControl();
                            Log.Write("Enabled EC fan control – manual mode");

                            _state.Fan1Level = null;
                            _state.Fan2Level = null;
                            _fan1LevelRequested = null;
                            _fan2LevelRequested = null;
                        }
                        else if (!_ecFanControlRequested && _state.EcFanControlEnabled)
                        {
                            _state.EcFanControlEnabled = false;
                            _fanController.DisableAutomaticFanControl();
                            Log.Write("Disabled EC fan control – manual mode");
                        }

                        // Check for fan control state changes that need to be applied.
                        if (!_state.EcFanControlEnabled)
                        {
                            if (_state.Fan1Level != _fan1LevelRequested)
                            {
                                _state.Fan1Level = _fan1LevelRequested;
                                if (_fan1LevelRequested != null)
                                {
                                    _fanController.SetFanLevel((FanLevel)_fan1LevelRequested, IsIndividualFanControlSupported ? FanIndex.Fan1 : FanIndex.AllFans);
                                }
                            }

                            if (_state.Fan2Present && IsIndividualFanControlSupported && _state.Fan2Level != _fan2LevelRequested)
                            {
                                _state.Fan2Level = _fan2LevelRequested;
                                if (_fan2LevelRequested != null)
                                {
                                    _fanController.SetFanLevel((FanLevel)_fan2LevelRequested, FanIndex.Fan2);
                                }
                            }
                        }

                        // Warn if a fan is set to completely off.
                        if (!_state.EcFanControlEnabled && (_state.Fan1Level == FanLevel.Off || (_state.Fan2Present && _state.Fan2Level == FanLevel.Off)))
                        {
                            //_state.ConsistencyModeStatus = "Warning: Fans set to \"off\" will not turn on regardless of temperature or load on the system";
                            //_state.ConsistencyModeStatus = "危险: 风扇设置为关闭，将不会自动打开!(和系统的负载或温度无关)";
                        }
                        else
                        {
                            _state.ConsistencyModeStatus = " ";
                        }
                    }
                    else if (_state.OperationMode == OperationMode.Consistency)
                    {
                        // Consistency mode logic.
                        _consistencyModeHandler.RunConsistencyModeLogic();
                    }

                    // See if we need to update the BIOS thermal setting.
                    if (_state.ThermalSetting != ThermalSetting.Error && RequestedThermalSetting != null && RequestedThermalSetting != _state.ThermalSetting)
                    {
                        Log.Write(string.Format("Switching thermal setting to {0}", RequestedThermalSetting));
                        DellSmbiosSmi.SetThermalSetting((ThermalSetting)RequestedThermalSetting);
                        _state.UpdateThermalSetting();
                        RequestedThermalSetting = null;
                    }

                    // Check to see if the active audio device has disappeared.
                    if (_state.AudioThreadRunning && !_state.AudioDevices.Contains(_state.SelectedAudioDevice))
                    {
                        // Remember the audio device in case it reappears.
                        _state.BringBackAudioDevice = _state.SelectedAudioDevice;

                        // Terminate the audio thread.
                        _soundPlayer?.RequestTermination();
                    }

                    _requestSemaphore.Release();
                    _state.Release();
                    releaseSemaphore = false;

                    UpdateForm();

                    Thread.Sleep(Core.RefreshInterval);
                }

                // If we got out of the loop without error, the program is terminating.
                if (IsAutomaticFanControlDisableSupported)
                {
                    _fanController.EnableAutomaticFanControl();
                    Log.Write("Enabled EC fan control – shutdown");
                }

                // Clean up as the program terminates.
                _fanController.Shutdown();
            }
            catch (Exception exception)
            {
                if (releaseSemaphore)
                {
                    _state.Release();
                }

                _state.WaitOne();
                _state.Error = string.Format("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                _state.Release();

                Log.Write(_state.Error);
            }

            _state.WaitOne();
            _state.BackgroundThreadRunning = false;
            _state.Release();

            UpdateForm();
        }

        /// <summary>
        /// Start up the "audio thread", which streams silence to a particular audio device.
        /// </summary>
        public void StartAudioThread()
        {
            new Thread(new ThreadStart(AudioThread)).Start();
        }

        /// <summary>
        /// Request that the audio thread be terminated.
        /// </summary>
        public void StopAudioThread()
        {
            _soundPlayer?.RequestTermination();
        }

        /// <summary>
        /// The audio thread streams silence to a particular audio output device.
        /// </summary>
        private void AudioThread()
        {
            bool audioKeepAliveEnabled = false;
            AudioDevice selectedAudioDevice = null;

            try
            {
                _state.WaitOne();

                if (!_state.AudioThreadRunning)
                {
                    audioKeepAliveEnabled = true;
                    selectedAudioDevice = _state.SelectedAudioDevice;
                    _state.AudioThreadRunning = true;
                }
                else
                {
                    // Somehow, there was an attempt to start the audio thread when it was running already?
                }

                _state.Release();

                if (audioKeepAliveEnabled && selectedAudioDevice != null)
                {
                    _soundPlayer = new(selectedAudioDevice);
                    _soundPlayer.PlaySound(@"Resources\Silence.wav", true);
                }
            }
            catch (Exception exception)
            {
                // Take no action, just allow the thread to terminate without error.
                Log.Write(exception);
            }

            _soundPlayer = null;

            // Audio thread terminating.
            if (audioKeepAliveEnabled)
            {
                _state.WaitOne();
                _state.AudioThreadRunning = false;
                _state.Release();

                UpdateForm();
            }
        }

        /// <summary>
        /// Request that the GUI form update using current values from the state.
        /// </summary>
        private void UpdateForm()
        {
            MethodInvoker updateInvoker = new(_form.UpdateForm);

            if (!_state.FormClosed)
            {
                try
                {
                    _form.BeginInvoke(updateInvoker);
                }
                catch (Exception)
                {
                    // Take no action.
                    // (There could be an error if trying to update the form after it has been closed... let it slide.)
                }
            }
        }

        /// <summary>
        /// Write the consistency mode configuration.
        /// </summary>
        /// <param name="lowerTemperatureThreshold">Lower temperature threshold</param>
        /// <param name="upperTemperatureThreshold">Upper temperature threshold</param>
        /// <param name="rpmThreshold">Fan speed threshold</param>
        public void WriteConsistencyModeConfiguration(int lowerTemperatureThreshold, int upperTemperatureThreshold, int rpmThreshold, bool enableStopFan, int cpuCoolDelay)
        {
            LowerTemperatureThreshold = lowerTemperatureThreshold;
            UpperTemperatureThreshold = upperTemperatureThreshold;
            RpmThreshold = ulong.Parse(rpmThreshold.ToString());
            EnableStopFan = enableStopFan;
            CpuCoolDelay = cpuCoolDelay;
        }

        /// <summary>
        /// Whether or not the system's automatic fan control can be specifically engaged and disengaged.
        /// </summary>
        public bool IsAutomaticFanControlDisableSupported
        {
            get { return _fanController.IsAutomaticFanControlDisableSupported; }
        }

        /// <summary>
        /// Whether or not the system fans can be set to run at a specific level.
        /// </summary>
        public bool IsSpecificFanControlSupported
        {
            get { return _fanController.IsSpecificFanControlSupported; }
        }

        /// <summary>
        /// Whether or not the system fans may be individually controlled.
        /// </summary>
        public bool IsIndividualFanControlSupported
        {
            get { return _fanController.IsIndividualFanControlSupported; }
        }
    }
}
