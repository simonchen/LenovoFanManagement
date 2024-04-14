using LibreHardwareMonitor.Hardware;
using System;
using System.Windows.Forms;

namespace DellFanManagement.App.TemperatureReaders
{
    /// <summary>
    /// Handles reading system CPU temperatures.
    /// </summary>
    class CpuTemperatureReader : LibreHardwareMonitorTemperatureReader
    {
        /// <summary>
        /// Constructor.  Initialize the computer object for reading the CPU temperature.
        /// </summary>
        public CpuTemperatureReader()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true
            };

            _computer.Open();
            //string report = _computer.GetReport();
            Log.WriteToFile(string.Format("CPU report\r\n:{0}", _computer.GetReport()));
        }
    }
}
