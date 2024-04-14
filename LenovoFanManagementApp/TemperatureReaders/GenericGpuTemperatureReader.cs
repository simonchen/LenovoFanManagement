using LibreHardwareMonitor.Hardware;

namespace DellFanManagement.App.TemperatureReaders
{
    class GenericGpuTemperatureReader : LibreHardwareMonitorTemperatureReader
    {
        /// <summary>
        /// Constructor.  Initialize the computer object for reading the CPU temperature.
        /// </summary>
        public GenericGpuTemperatureReader()
        {
            _computer = new Computer
            {
                IsGpuEnabled = true
            };

            _computer.Open();
            Log.WriteToFile(string.Format("Generic Gpu report:\r\n{0}", _computer.GetReport()));
        }
    }
}
