namespace DellFanManagement.DellSmbiozBzhLib
{
    /// <summary>
    /// Used to specify which fan level to set.
    /// </summary>
    public enum BzhFanLevel : uint
    {
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
        /// Fan off.
        /// </summary>
        Level0 = 0,

        /// <summary>
        /// Medium fan speed.
        /// </summary>
        Level1 = 1,

        /// <summary>
        /// High fan speed.
        /// </summary>
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        Level6 = 6,
        Level7 = 7,
        Level8 = 64
    }
}
