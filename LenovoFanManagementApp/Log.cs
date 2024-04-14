using System;
using System.IO;
using System.Threading;

namespace DellFanManagement.App
{
    static class Log
    {
        /// <summary>
        /// Write a message to the log.
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void Write(string message)
        {
            // TODO: Support Windows event log.
            Console.WriteLine(string.Format("{0}: {1}", DateTime.Now, message));
        }

        /// <summary>
        /// Write details about an exception to the log.
        /// </summary>
        /// <param name="exception">Exception to log</param>
        public static void Write(Exception exception)
        {
            Write(string.Format("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace));
        }

        private readonly static Semaphore _semaphore = new(1, 1);
        public static bool AllowingLogWriteToFile { get; set; }
        public static void WriteToFile(string msg, bool bOverwite = false)
        {
            //#if (DEBUG)
            if (!AllowingLogWriteToFile) return;

            _semaphore.WaitOne();
            string strAppPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
            Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location) + ".log");
            FileStream fs = File.Open(strAppPath, bOverwite ? FileMode.Create : FileMode.Append);
            fs.Write(System.Text.UTF8Encoding.UTF8.GetBytes(string.Format("[{0}]\r\n{1}\r\n\r\n", DateTime.Now.ToString(), msg)));
            fs.Close();
            _semaphore.Release();
            //#endif
        }
    }
}
