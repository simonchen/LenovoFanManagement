using System;
using System.Diagnostics;
using System.Threading;
using System.IO;

// Nuget examples for reference System.ServiceProcess
//https://stackoverflow.com/questions/7764088/net-console-application-as-windows-service
//https://stackoverflow.com/questions/12201365/programmatically-remove-a-service-using-c-sharp
//using System.ServiceProcess;
//using System.ComponentModel;
//using System.Configuration.Install;

// Microsoft official .net core 5.0 with windows service
// https://devblogs.microsoft.com/ifdef-windows/creating-a-windows-service-with-c-net5/
//https://stackoverflow.com/questions/68942692/where-is-serviceprocessinstaller-on-net5
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.EventLog;
using System.Threading.Tasks;

namespace lf_daemon
{
    public class CommandLineOptions
    {
        [Value(index: 0, Required = true, HelpText = "Application name to watch")]
        public string Path { get; set; }
    }
    public class AppWatcher : BackgroundService
    {
        private readonly CommandLineOptions _options;

        public AppWatcher(CommandLineOptions options)
        {
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //var tcs = new TaskCompletionSource<bool>();
            //stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            //await tcs.Task;
            heri16.ProcessExtensions.log("Service started");
            Program._daemon_exe_name = _options.Path;
            string fullAppPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), Program._daemon_exe_name);
            if (!File.Exists(fullAppPath))
            {
                heri16.ProcessExtensions.log("Error: "+fullAppPath+" does not exist.");
                return;
            }
            // https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.task.run?view=net-8.0#system-threading-tasks-task-run-1(system-func((system-threading-tasks-task((-0))))-system-threading-cancellationtoken)
            await Task.Run(() =>
            {
                while (true)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    Program.callback(null);
                    Thread.Sleep(2000);
                }
                Timer t = new Timer(Program.callback, null, 0, 2000);
                Thread.Sleep(System.Threading.Timeout.Infinite);
            }, stoppingToken);

            heri16.ProcessExtensions.log("Service stopped");
        }

        class Program
        {
            public const string ServiceName = "LenovoFanDaemon";
            public static string _daemon_exe_name = "";
            // This method's signature must match the TimerCallback delegate
            public static void callback(Object state)
            {
                try
                {
                    //bool isAppRunning = IsProcessOpen(_daemon_exe_name);
                    //if (!isAppRunning)
                    if (Securite.Win32.natif.GetProcessIdByName(_daemon_exe_name) == 0)
                    {
                        string strAppPath;
                        strAppPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), _daemon_exe_name);
                        //Process p = System.Diagnostics.Process.Start(strAppPath);
                        heri16.ProcessExtensions.log(strAppPath);
                        //heri16.ProcessExtensions.StartProcessAsCurrentUser(strAppPath);
                        int dwProcessId, dwExitCode;
                        Securite.Win32.natif.CreateSystemProcess(strAppPath, out dwProcessId, out dwExitCode);
                    }
                }
                catch (Exception expt)
                {
                    heri16.ProcessExtensions.log("Failed to open " + _daemon_exe_name);
                    heri16.ProcessExtensions.log(expt.Message + "\r\n");
                }
            }

            public static bool IsProcessOpen(string name)
            {
                //here we're going to get a list of all running processes on
                //the computer
                foreach (Process p in Process.GetProcesses())
                {
                    // In case we get Access Denied
                    try
                    {
                        if (p.MainModule.FileName.ToLower().EndsWith(".\\" + name.ToLower()))
                        {
                            //if the process is found to be running then we
                            //return a true
                            return true;
                        }
                    }
                    catch
                    { }

                }
                //otherwise we return a false
                return false;
            }

            static async Task<int> Main(string[] args)
            {
                return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                    .MapResult(async (opts) =>
                    {
                        await CreateHostBuilder(args, opts).Build().RunAsync();
                        return 0;
                    },
                    errs => Task.FromResult(-1)); // Invalid arguments
            }

            public static IHostBuilder CreateHostBuilder(string[] args, CommandLineOptions opts) =>
                Host.CreateDefaultBuilder(args)
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddSingleton(opts);
                        services.AddHostedService<AppWatcher>();
                    }).UseWindowsService();
        }
            /*
        static void Main(string[] args)
        {
            if (args.Length < 1) return;
            _daemon_exe_name = args[0];
            bool createdNew;
            Mutex m = new Mutex(true, _daemon_exe_name, out createdNew);
            if (!createdNew)
            {
                return;
            }

                if (Environment.UserInteractive)
                {
                    Start(args);
                    Stop();
                }
        }
            */
        public static void Start(string[] args)
        {
            Timer t = new Timer(Program.callback, null, 0, 2000);
            Thread.Sleep(System.Threading.Timeout.Infinite);
        }

        public static void Stop()
        {
        }
    }

}
