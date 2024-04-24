using DellFanManagement.DellSmbiozBzhLib;
using Gma.System.MouseKeyHook;
using System.Windows.Forms;
using System.Threading;

namespace DellFanManagement.App
{
    class KeyboardLightSettings
    {
        private IKeyboardMouseEvents _globalHook;
        private static readonly byte _reg = 0x0D;
        private static bool _NeedDelayToggle = false;
        private static bool _AutoOff = false;
        private static int _DelayInterval = 10;
        private static int _DelaySeconds = 0;
        private static BackLightLevel _RestoreState = BackLightLevel.Off;
        private static BackLightLevel _CurState = BackLightLevel.Off;

        private readonly object lighting = new object();

        public enum BackLightLevel
        {
            Low,
            High,
            Off
        }

        public KeyboardLightSettings(int delayInterval=10)
        {
            ResetDelay(delayInterval);

            if (_globalHook == null)
            {
                // Note: for the application hook, use the Hook.AppEvents() instead
                _globalHook = Hook.GlobalEvents();
            }
            _globalHook.KeyDown += GlobalHookOnKeyDown;
        }

        public void Dispose()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyDown -= GlobalHookOnKeyDown;
                _globalHook = null;
            }
        }

        public void ResetDelay(int delayInterval = 10)
        {
            _DelayInterval = delayInterval;
            if (DellSmbiosBzh.Initialize())
            {
                _RestoreState = GetBackLightState();
                _CurState = _RestoreState;
            }

            if (_RestoreState != BackLightLevel.Off)
            {
                _NeedDelayToggle = true;
            }

            _AutoOff = false;
            _DelaySeconds = 0;
        }

        private byte ReadBackLightRegValue()
        {
            byte val = 0;
            for (int i = 0; i < 3; i++) // max. 3 times on retry
            {
                val = DellSmbiosBzh.readByte(_reg);
                if (val != 0)
                    break;
            }

            return val;
        }

        protected BackLightLevel GetBackLightState()
        {
            BackLightLevel level = BackLightLevel.Off;
            byte val = ReadBackLightRegValue();

            if ((val & 0x40) == 0x40)
            {
                level = BackLightLevel.Low;
            }
            if ((val & 0x80) == 0x80)
            {
                level = BackLightLevel.High;
            }
            return level;
        }

        protected void ToggleBackLight(BackLightLevel level)
        {
            byte val = ReadBackLightRegValue();
            if (level == BackLightLevel.Low)
                val |= 0x40;
            if (level == BackLightLevel.High)
                val |= 0x80;
            if (level == BackLightLevel.Off)
                val &= 0x3F;
            DellSmbiosBzh.writeByte(_reg, val);
        }

        protected void CloseBackLight()
        {
            byte val = ReadBackLightRegValue();
            val &= 0x3F;
            DellSmbiosBzh.writeByte(_reg, val);
        }

        private void GlobalHookOnKeyDown(object sender, KeyEventArgs e)
        {
            // Detects if user itself turn backlight off manually.
            // if (_CurState != GetBackLightState())
            //{
            // Manually trigger
            //    _NeedDelayToggle = false;
            //}

            // Check if backlight should be toggled.
            lock (lighting)
            {
                _DelaySeconds = 0;
                if (e.KeyCode != Keys.None && _NeedDelayToggle && _RestoreState != BackLightLevel.Off)
                {
                    ToggleBackLight(_RestoreState);
                    _CurState = _RestoreState;
                    _AutoOff = false;
                    _DelaySeconds = 0;
                }
            }
        }
         
        public void Update()
        {
            lock (lighting)
            {
                _DelaySeconds += 1; // caller is a timer procedure that makes the count +1
                                    // timer is up, see if backlight should be off
                if (_DelaySeconds >= _DelayInterval)
                {          
                    _DelaySeconds = 0;
                    if (_NeedDelayToggle)
                    {
                        CloseBackLight(); // later on, Backlight would be auto-turn on.
                        _CurState = BackLightLevel.Off;
                        _AutoOff = true;
                    }
                }
                else
                {
                    // always check if that delay toggle is needed.
                    _CurState = GetBackLightState();
                    if (_CurState != _RestoreState)
                    {
                        _NeedDelayToggle = ((_CurState == BackLightLevel.Off && _AutoOff) || (_CurState != BackLightLevel.Off)) ? true : false;
                        if (!(_CurState == BackLightLevel.Off && _AutoOff))
                        {
                            _RestoreState = _CurState;
                            _AutoOff = false;
                            _DelaySeconds = 0;
                        }
                    }
                }
            }
        }
    }
}
