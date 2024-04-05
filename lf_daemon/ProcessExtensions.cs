using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;

// Refers to https://gist.github.com/heri16/8f69aa919ee1d87f3bb53255ef78f188
namespace heri16
{

    /// <summary>
    ///   Static class to help Start a GUI/Console Windows Process as any user that is logged-in to an Interactive Terminal-Session (e.g. RDP).
    /// </summary>
    /// <devdoc>
    ///   Console-type processes when created with a new console, don't always write to the redirected stdOutput and stdError.
    ///   To fix this, the application executed should always detach from its current console (if any), and
    ///   call AttachConsole(-1) to attach to the console of the parent process.
    ///
    ///   <para>
    ///     [DllImport("kernel32.dll")]
    ///     static extern bool FreeConsole();
    ///
    ///     [DllImport("kernel32.dll")]
    ///     static extern bool AttachConsole(uint dwProcessID);
    ///   <para>
    /// </devdoc>
    public static class ProcessExtensions
    {
        #region Win32 Constants

        private const uint INVALID_SESSION_ID = 0xFFFFFFFF;
        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

        #endregion

        #region DllImports

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, SafeHandle hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        private static extern uint WTSQueryUserToken(uint SessionId, out SafeUserTokenHandle phToken);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern int WTSEnumerateSessions(
            IntPtr hServer,
            int Reserved,
            int Version,
            out IntPtr ppSessionInfo,
            out int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(
            System.IntPtr hServer,
            uint sessionId,
            WTS_INFO_CLASS wtsInfoClass,
            out System.IntPtr ppBuffer,
            out uint pBytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        #endregion

        #region Win32 Structs

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public readonly UInt32 SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public readonly String pWinStationName;

            public readonly WTS_CONNECTSTATE_CLASS State;
        }

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType
        }

        #endregion

        public static void log(string msg)
        {
            string strAppPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "lt_daemon.log");
            FileStream fs = File.Open(strAppPath, FileMode.Append);
            fs.Write(System.Text.UTF8Encoding.UTF8.GetBytes(msg+"\r\n"));
            fs.Close();
        }
        /// <devdoc>
        ///   Gets the user token from the currently active session. Application must be running within the context of the LocalSystem Account.
        ///  </devdoc>
        private static bool GetSessionUserToken(ref SafeUserTokenHandle phUserToken, string user_filter = null)
        {
            var bResult = false;
            SafeUserTokenHandle hImpersonationToken = new SafeUserTokenHandle();
            var activeSessionId = INVALID_SESSION_ID;
            var pSessionInfo = IntPtr.Zero;
            var sessionCount = 0;

            IntPtr userPtr = IntPtr.Zero;
            IntPtr domainPtr = IntPtr.Zero;
            uint bytes = 0;

            // Get a handle to the user access token for the current active session.
            if (WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out pSessionInfo, out sessionCount) != 0)
            {
                var arrayElementSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                var current = pSessionInfo;

                for (var i = 0; i < sessionCount; i++)
                {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure((IntPtr)current, typeof(WTS_SESSION_INFO));
                    current += arrayElementSize;

                    WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, si.SessionID, WTS_INFO_CLASS.WTSUserName, out userPtr, out bytes);
                    WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, si.SessionID, WTS_INFO_CLASS.WTSDomainName, out domainPtr, out bytes);

                    var user = Marshal.PtrToStringAnsi(userPtr);
                    var domain = Marshal.PtrToStringAnsi(domainPtr);

                    WTSFreeMemory(userPtr); 
                    WTSFreeMemory(domainPtr);

                    if ((user_filter == null && si.State == WTS_CONNECTSTATE_CLASS.WTSActive) || (user == user_filter) )
                    {
                        activeSessionId = si.SessionID;
                    }

                }
            }

            // If enumerating did not work, fall back to the old method
            if (activeSessionId == INVALID_SESSION_ID)
            {
                activeSessionId = WTSGetActiveConsoleSessionId();
            }

            log("activeSessionId=" + activeSessionId.ToString());

            // Simon [2024-3-27]
            // To call WTSQueryUserToken successfully,
            // the calling application must be running within the context of the LocalSystem account
            // and have the SE_TCB_NAME privilege.
            /* Add the TakeOwnership Privilege
             */
            bool blRc = Securite.Win32.natif.MySetPrivilege(Securite.Win32.natif.TrustedComputingBase, true);
            if (!blRc)
                return bResult;

            if (WTSQueryUserToken(activeSessionId, out hImpersonationToken) != 0)
            {
                // Convert the impersonation token to a primary token
                bResult = SafeUserTokenHandle.DuplicateTokenEx(hImpersonationToken, 0, null,
                    NativeMethods.IMPERSONATION_LEVEL_SecurityImpersonation, NativeMethods.TOKEN_TYPE_TokenPrimary,
                    out phUserToken);

                //CloseHandle(hImpersonationToken);
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
            }

            log(string.Format("hImpersonationToken > IsInvalid = {0}", hImpersonationToken.IsInvalid));
            //blRc = Securite.Win32.natif.MySetPrivilege(Securite.Win32.natif.TrustedComputingBase, false);

            return bResult;
        }        

        /// <devdoc>
        ///   Starts a Process as the last logged-in user that is currently active.
        ///
        ///   <para>
        ///     Example:
        ///     psexec -ids powershell.exe
        ///     Add-Type -Path .\src\ProcessExtensions.cs
        ///     [murrayju.ProcessExtensions]::StartProcessAsCurrentUser("C:\Windows\System32\cmd.exe", "cmd.exe /K echo running");
        ///   </para>
        /// </devdoc>
        public static bool StartProcessAsCurrentUser(string appPath, string cmdLine = null, string workDir = null, bool visible = true)
        {
            return StartProcessAsUser(null, appPath, cmdLine, workDir, visible);
        }

        /// <devdoc>
        ///   Starts a Process as any logged-in user with an active or disconnected session.
        ///
        ///   <para>
        ///     Example:
        ///     psexec -ids powershell.exe
        ///     Add-Type -Path .\src\ProcessExtensions.cs
        ///     [murrayju.ProcessExtensions]::StartProcessAsUser("Mailin", "D:\RENE\XmlImport\ReneXmlImport.exe", "ReneXmlImport.exe D:\RENE\Data\Import\Adj_Selling_Price_3001.xml");
        ///   </para>
        /// </devdoc>
        public static bool StartProcessAsUser(string user, string appPath, string cmdLine = null, string workDir = null, bool visible = true)
        {
            SafeUserTokenHandle hUserToken = null;
            var startupInfo = new NativeMethods.STARTUPINFO();
            var processInfo = new SafeNativeMethods.PROCESS_INFORMATION();
            //var procSH = new SafeProcessHandle();
            //var threadSH = new SafeThreadHandle();

            var environmentPtr = IntPtr.Zero;
            int iResultOfCreateProcessAsUser;

            //SafeFileHandle standardInputWritePipeHandle = null;
            SafeFileHandle standardOutputReadPipeHandle = null;
            SafeFileHandle standardErrorReadPipeHandle = null;

            try
            {
                if (!GetSessionUserToken(ref hUserToken, user))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Exception("StartProcessAsCurrentUser: GetSessionUserToken failed.");
                }

                int creationFlags = NativeMethods.CREATE_UNICODE_ENVIRONMENT | (visible ? NativeMethods.CREATE_NEW_CONSOLE : NativeMethods.CREATE_NO_WINDOW);
                startupInfo.wShowWindow = (short)(visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
                startupInfo.lpDesktop = "winsta0\\default";

                CreatePipe(out standardOutputReadPipeHandle, out startupInfo.hStdOutput, false);
                CreatePipe(out standardErrorReadPipeHandle, out startupInfo.hStdError, false);
                startupInfo.dwFlags = NativeMethods.STARTF_USESTDHANDLES;

                if (!CreateEnvironmentBlock(out environmentPtr, hUserToken, false))
                {
                    throw new Exception("StartProcessAsCurrentUser: CreateEnvironmentBlock failed.");
                }

                if (String.IsNullOrEmpty(workDir)) { workDir = Environment.CurrentDirectory; }

                Securite.Win32.natif.ImpersonateLoggedOnUser(hUserToken.DangerousGetHandle());

                if (!NativeMethods.CreateProcessAsUser(hUserToken,
                    appPath, // Application Name
                    cmdLine, // Command Line
                    null,
                    null,
                    true, // Terminal Services:  You cannot inherit handles across sessions
                    creationFlags,
                    new HandleRef(null, environmentPtr),
                    workDir, // Working directory
                    startupInfo,
                    processInfo))
                {
                    iResultOfCreateProcessAsUser = Marshal.GetLastWin32Error();
                    throw new Exception("StartProcessAsCurrentUser: CreateProcessAsUser failed due to Error " + iResultOfCreateProcessAsUser.ToString() + ".\n");
                }

            }
            finally
            {
                //CloseHandle(hUserToken);

                if (environmentPtr != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(environmentPtr);
                }
                startupInfo.Dispose();

                UnsafeNativeMethods.CloseHandle(processInfo.hThread);
                UnsafeNativeMethods.CloseHandle(processInfo.hProcess);
            }


            StreamReader standardOutput = new StreamReader(new FileStream(standardOutputReadPipeHandle, FileAccess.Read, 0x1000, false), Console.OutputEncoding, true, 0x1000);
            StreamReader standardError = new StreamReader(new FileStream(standardErrorReadPipeHandle, FileAccess.Read, 0x1000, false), Console.OutputEncoding, true, 0x1000);

            while (!standardOutput.EndOfStream)
            {
               string line = standardOutput.ReadLine();
               if (line.Length>0) Console.WriteLine("stdOutput: " + line);
            }

            return true;
        }

        /// <devdoc>
        ///   Implementation from: http://referencesource.microsoft.com/#System/services/monitoring/system/diagnosticts/Process.cs,64d2d72d3ee2e6f9
        /// </devdoc>
        private static void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
        {
            NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            lpPipeAttributes.bInheritHandle = true;

            SafeFileHandle hWritePipe = null;
            try
            {
                if (parentInputs)
                    CreatePipeWithSecurityAttributes(out childHandle, out hWritePipe, lpPipeAttributes, 0);
                else
                    CreatePipeWithSecurityAttributes(out hWritePipe, out childHandle, lpPipeAttributes, 0);
                if (!NativeMethods.DuplicateHandle(new HandleRef(null, NativeMethods.GetCurrentProcess()), hWritePipe, new HandleRef(null, NativeMethods.GetCurrentProcess()), out parentHandle, 0, false, NativeMethods.DUPLICATE_SAME_ACCESS))
                    throw new Exception();
            }
            finally
            {
                if ((hWritePipe != null) && !hWritePipe.IsInvalid)
                {
                    hWritePipe.Close();
                }
            }
        }

        /// <devdoc>
        ///   Implementation from: http://referencesource.microsoft.com/#System/services/monitoring/system/diagnosticts/Process.cs,9136e8bd1abc4d01
        /// </devdoc>
        private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe,
            NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
        {
            bool ret = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
            if ((!ret || hReadPipe.IsInvalid) || hWritePipe.IsInvalid)
                throw new Exception();
        }

    }

    /// <devdoc>
    ///   Implementation from: http://referencesource.microsoft.com/#System/compmod/microsoft/win32/NativeMethods.cs
    /// </devdoc>
    internal static class NativeMethods
    {
        public const int STARTF_USESTDHANDLES = 0x00000100;
        public const int DUPLICATE_SAME_ACCESS = 2;

        public const int CREATE_NO_WINDOW = 0x08000000;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int CREATE_NEW_CONSOLE = 0x00000010;

        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_NORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_MAXIMIZE = 3;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_SHOWMINNOACTIVE = 7;
        public const int SW_SHOWNA = 8;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWDEFAULT = 10;
        public const int SW_MAX = 10;

        public const int IMPERSONATION_LEVEL_SecurityAnonymous = 0;
        public const int IMPERSONATION_LEVEL_SecurityIdentification = 1;
        public const int IMPERSONATION_LEVEL_SecurityImpersonation = 2;
        public const int IMPERSONATION_LEVEL_SecurityDelegation = 3;

        public const int TOKEN_TYPE_TokenPrimary = 1;
        public const int TOKEN_TYPE_TokenImpersonation = 2;

        [StructLayout(LayoutKind.Sequential)]
        public class SECURITY_ATTRIBUTES {
            public int nLength = 12;
            public IntPtr lpSecurityDescriptor = IntPtr.Zero;
            public bool bInheritHandle = false;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class STARTUPINFO {
            public int cb;
            public IntPtr lpReserved = IntPtr.Zero;
            //public IntPtr lpDesktop = IntPtr.Zero;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpDesktop = String.Empty;
            public IntPtr lpTitle = IntPtr.Zero;
            public int dwX = 0;
            public int dwY = 0;
            public int dwXSize = 0;
            public int dwYSize = 0;
            public int dwXCountChars = 0;
            public int dwYCountChars = 0;
            public int dwFillAttribute = 0;
            public int dwFlags = 0;
            public short wShowWindow = 0;
            public short cbReserved2 = 0;
            public IntPtr lpReserved2 = IntPtr.Zero;
            public SafeFileHandle hStdInput = new SafeFileHandle(IntPtr.Zero, false);
            public SafeFileHandle hStdOutput = new SafeFileHandle(IntPtr.Zero, false);
            public SafeFileHandle hStdError = new SafeFileHandle(IntPtr.Zero, false);
 
            public STARTUPINFO() {
                cb = Marshal.SizeOf(this);
            } 
            
            public void Dispose() {                
                // close the handles created for child process
                if(hStdInput != null && !hStdInput.IsInvalid) {
                    hStdInput.Close();
                    hStdInput = null;
                }
 
                if(hStdOutput != null && !hStdOutput.IsInvalid) {
                    hStdOutput.Close();
                    hStdOutput = null;
                }
 
                if(hStdError != null && !hStdError.IsInvalid) {
                    hStdError.Close();
                    hStdError = null;
                }                
            }
        }

        [DllImport(ExternDll.Advapi32, CharSet=System.Runtime.InteropServices.CharSet.Auto, SetLastError=true, BestFitMapping=false)]
        [System.Security.SuppressUnmanagedCodeSecurityAttribute()]
        public extern static bool CreateProcessAsUser(
            SafeHandle hToken,
            string lpApplicationName,
            string lpCommandLine,
            SECURITY_ATTRIBUTES lpProcessAttributes,
            SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            HandleRef lpEnvironment,
            string lpCurrentDirectory,
            STARTUPINFO lpStartupInfo,
            SafeNativeMethods.PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport(ExternDll.Kernel32, CharSet=System.Runtime.InteropServices.CharSet.Auto, SetLastError=true)]
        public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

        [DllImport(ExternDll.Kernel32, CharSet=System.Runtime.InteropServices.CharSet.Ansi, SetLastError=true, BestFitMapping=false)]    
        public static extern bool DuplicateHandle(
            HandleRef hSourceProcessHandle,
            SafeHandle hSourceHandle,
            HandleRef hTargetProcess,
            out SafeFileHandle targetHandle,
            int dwDesiredAccess,
            bool bInheritHandle,
            int dwOptions
        );

        [DllImport(ExternDll.Kernel32, CharSet=System.Runtime.InteropServices.CharSet.Ansi, SetLastError=true)]
        public static extern IntPtr GetCurrentProcess();
    }

    /// <devdoc>
    ///   Implementation from: http://referencesource.microsoft.com/#System/compmod/microsoft/win32/SafeNativeMethods.cs
    /// <devdoc>
    internal static class SafeNativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal class PROCESS_INFORMATION {
            // The handles in PROCESS_INFORMATION are initialized in unmanaged functions.
            // We can't use SafeHandle here because Interop doesn't support [out] SafeHandles in structures/classes yet.            
            public IntPtr               hProcess = IntPtr.Zero;
            public IntPtr               hThread = IntPtr.Zero;
            public int                  dwProcessId = 0;
            public int                  dwThreadId = 0;
 
            // Note this class makes no attempt to free the handles
            // Use InitialSetHandle to copy to handles into SafeHandles
 
        }
    }

    /// <devdoc>
    ///   Implementation from: http://referencesource.microsoft.com/#System/compmod/microsoft/win32/UnsafeNativeMethods.cs
    /// <devdoc>
    internal static class UnsafeNativeMethods
    {

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

    }

    /// <devdoc>
    ///   Implementation from: http://referencesource.microsoft.com/#System/compmod/microsoft/win32/safehandles/SafeUserTokenHandle.cs
    /// <devdoc>
    internal sealed class SafeUserTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // Note that OpenProcess returns 0 on failure.
        internal SafeUserTokenHandle() : base (true) {}
 
        internal SafeUserTokenHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle) {
            SetHandle(existingHandle);
        }
 
        [DllImport(ExternDll.Advapi32, CharSet=System.Runtime.InteropServices.CharSet.Auto, SetLastError=true, BestFitMapping=false)]
        internal extern static bool DuplicateTokenEx(SafeHandle hToken, int access, NativeMethods.SECURITY_ATTRIBUTES tokenAttributes, int impersonationLevel, int tokenType, out SafeUserTokenHandle hNewToken);
 
        [DllImport(ExternDll.Kernel32, ExactSpelling=true, SetLastError=true)]
        private static extern bool CloseHandle(IntPtr handle);            
 
        override protected bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    /// <devdoc>
    ///   Implementation from: http://referencesource.microsoft.com/#System/misc/externdll.cs
    /// <devdoc>
    internal static class ExternDll
    {
        public const string Advapi32 = "advapi32.dll";
        public const string Kernel32 = "kernel32.dll";
        public const string Wtsapi32 = "wtsapi32.dll";
        public const string Userenv = "userenv.dll"; 
    }
}