namespace DellFanManagement.DellSmbiozBzhLib
{
    public enum SmbiosCommand : byte
    {
        /// Lenovo EC register to control fan speed:
        LenovoFanReg = 0x2f,

        /// <summary>
        /// Get RPM of a fan; takes fan index. (reading word data)
        /// </summary>
        GetFanRpm = 0x84,

        /// <summary>
        ///  Change fan speed that write value in LenovoFanReg
        ///  Total 5 levels as following
        /// </summary>
        /*
        0x2F = 0x40 / 64 (0x85=0x16, 0x84=0xA8 , 0x16A8 = 5800RPM)
        0x2F = 0x07 / 7 (4400RPM）
        0x2F = 0x06 / 6 (4000RPM)
        0x2F = 0x05 / 5 (3700RPM)
        0x2F = 0x04 / 4 (3400RPM)
        0x2F = 0x03 / 3 (3100RPM)
        0x2F = 0x02 / 2 (2800RPM)
        0x2F = 0x01 / 1 (2500RPM)
        0x2F = 0x20 /32 , 0x2F = 0x08 / 8, 0x2F = 0x00 / 0 (0RPM)
        */
        

        /// <summary>
        /// Disable embedded controller automatic fan control.
        /// </summary>
        DisableEcFanControl = 0x80, // Keep auto-mode

        /// <summary>
        /// Enable embedded controller automatic fan control.
        /// </summary>
        EnableEcFanControl = 0x80, // Keep auto-mode
    }

    /// <summary>
    /// Identifiers for the different SMBIOS commands.
    /// </summary>
    /// <seealso cref="https://lkml.org/lkml/2008/12/18/44"/>
    public enum SmbiosCommand_DELL : uint
    {
        /// <summary>
        /// Get the current speed indicator of a fan; takes fan index.
        /// </summary>
        GetFanLevel = 0x00A3,

        /// <summary>
        /// Set speed of a fan; takes fan index and speed (shift left 8 bits).
        /// </summary>
        SetFanLevel = 0x01A3,

        /// <summary>
        /// Get RPM of a fan; takes fan index.
        /// </summary>
        GetFanRpm = 0x02A3,

        /// <summary>
        /// Unknown function; takes one byte.
        /// </summary>
        Unknown1 = 0x03A3,

        /// <summary>
        /// Gets nominal fan speed; takes two parameters.
        /// </summary>
        GetNominalFanSpeed = 0x04A3,

        /// <summary>
        /// Gets fan tolerance speed; takes two parameters.
        /// </summary>
        GetFanToleranceSpeed = 0x05A3,

        /// <summary>
        /// Gets temperature of a sensor; takes sensor index.
        /// </summary>
        GetSensorTemperature = 0x10A3,

        /// <summary>
        /// Unknown function.
        /// </summary>
        Unknown2 = 0x11A3,

        /// <summary>
        /// NBSVC function depending on parameter:
        ///   0x0000 = NBSVC clear
        ///   0x0003 = NBSVC query
        ///   0x0100 = NBSVC stop trend
        ///   0x0122 = NBSVC start trend
        ///   0x02?? = NBSVC read
        /// </summary>
        Nbsvc = 0x12A3,

        /// <summary>
        /// Unknown function; takes 1 byte (oder 0x16) + 1 byte.
        /// </summary>
        Unknown3 = 0x21A3,

        /// <summary>
        /// Get charger information; takes one paramater.
        /// </summary>
        GetChargerInfo = 0x22A3,

        /// <summary>
        /// Unknown function; takes four parameters: two bytes, one word, and one double word.
        /// </summary>
        Unknown4 = 0x23A3,

        /// <summary>
        /// Get adapter info status; takes one paramter, oder 0x03.
        /// </summary>
        GetAdapterInfoStatus = 0x24A3,

        /// <summary>
        /// Disable embedded controller automatic fan control.
        /// </summary>
        DisableEcFanControl = 0x30A3,

        /// <summary>
        /// Enable embedded controller automatic fan control.
        /// </summary>
        EnableEcFanControl = 0x31A3,

        /// <summary>
        /// Unknown function; no parameters.
        /// </summary>
        Unknown7 = 0x32A3,

        /// <summary>
        /// Unknown function; no parameters.
        /// </summary>
        Unknown8 = 0x33A3,

        /// <summary>
        /// Disable embedded controller automatic fan control (alternate method).
        /// </summary>
        DisableEcFanControlAlternate = 0x34A3,

        /// <summary>
        /// Enable embedded controller automatic fan control (alternate method).
        /// </summary>
        EnableEcFanControlAlternate = 0x31A3,

        /// <summary>
        /// Get hotkey scancode list; unknown parameters.
        /// </summary>
        GetHotkeyScancodeList = 0x36A3,

        /// <summary>
        /// Unknown function; no parameters.
        /// </summary>
        Unknown9 = 0x37A3,

        /// <summary>
        /// Gets docking state; no parameters.
        /// </summary>
        GetDockingState = 0x40A3,

        /// <summary>
        /// Unknown function; takes two parameters.
        /// </summary>
        Unknown10 = 0xF0A3,

        /// <summary>
        /// Gets SMBIOS version; takes one parameter.
        /// </summary>
        GetSmbiosVersion = 0xFEA3,

        /// <summary>
        /// Check SMBIOS interface; should return "DELLDIAG".
        /// </summary>
        CheckSmbiosInterface = 0xFFA3
    }
}
