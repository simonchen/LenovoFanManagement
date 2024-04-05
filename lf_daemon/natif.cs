using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Securite.Win32
{
  class natif
  {
    public static void log(string msg)
    {
#if (DEBUG)
            string strAppPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), 
            Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location) +".log");
        FileStream fs = File.Open(strAppPath, FileMode.Append);
        fs.Write(System.Text.UTF8Encoding.UTF8.GetBytes(msg + "\r\n"));
        fs.Close();
#endif
    }
        
    public const string CreateToken = "SeCreateTokenPrivilege";
    public const string AssignPrimaryToken = "SeAssignPrimaryTokenPrivilege";
    public const string LockMemory = "SeLockMemoryPrivilege";
    public const string IncreaseQuota = "SeIncreaseQuotaPrivilege";
    public const string UnsolicitedInput = "SeUnsolicitedInputPrivilege";
    public const string MachineAccount = "SeMachineAccountPrivilege";
    public const string TrustedComputingBase = "SeTcbPrivilege";
    public const string Security = "SeSecurityPrivilege";
    public const string TakeOwnership = "SeTakeOwnershipPrivilege";
    public const string LoadDriver = "SeLoadDriverPrivilege";
    public const string SystemProfile = "SeSystemProfilePrivilege";
    public const string SystemTime = "SeSystemtimePrivilege";
    public const string ProfileSingleProcess = "SeProfileSingleProcessPrivilege";
    public const string IncreaseBasePriority = "SeIncreaseBasePriorityPrivilege";
    public const string CreatePageFile = "SeCreatePagefilePrivilege";
    public const string CreatePermanent = "SeCreatePermanentPrivilege";
    public const string Backup = "SeBackupPrivilege";
    public const string Restore = "SeRestorePrivilege";
    public const string Shutdown = "SeShutdownPrivilege";
    public const string Debug = "SeDebugPrivilege";
    public const string Audit = "SeAuditPrivilege";
    public const string SystemEnvironment = "SeSystemEnvironmentPrivilege";
    public const string ChangeNotify = "SeChangeNotifyPrivilege";
    public const string RemoteShutdown = "SeRemoteShutdownPrivilege";
    public const string Undock = "SeUndockPrivilege";
    public const string SyncAgent = "SeSyncAgentPrivilege";
    public const string EnableDelegation = "SeEnableDelegationPrivilege";
    public const string ManageVolume = "SeManageVolumePrivilege";
    public const string Impersonate = "SeImpersonatePrivilege";
    public const string CreateGlobal = "SeCreateGlobalPrivilege";
    public const string TrustedCredentialManagerAccess = "SeTrustedCredManAccessPrivilege";
    public const string ReserveProcessor = "SeReserveProcessorPrivilege";

    public static uint ERROR_SUCCESS = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
      public Int32 lowPart;
      public Int32 highPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
     public LUID Luid;
     public Int32 Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES User;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXPLICIT_ACCESS
    {
        public uint grfAccessPermissions;
        public GRANT_ACCESS grfAccessMode;
        public uint grfInheritance;
        public TRUSTEE Trustee;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TRUSTEE
    {
        public IntPtr pMultipleTrustee;
        public TRUSTEE_FORM TrusteeForm;
        public TRUSTEE_TYPE TrusteeType;
        public string ptstrName;
    }
    public enum GRANT_ACCESS
    {
        NO_ACCESS = 0,
        GRANT_ACCESS = 1,
        SET_ACCESS = 2,
        DENY_ACCESS = 3,
        REVOKE_ACCESS = 4,
        SET_AUDIT_SUCCESS = 5,
        SET_AUDIT_FAILURE = 6
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES 
    {
      public Int32 PrivilegeCount;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
      public LUID_AND_ATTRIBUTES [] Privileges;
    }

    // Newly added 
    [Flags]
    public enum ProcessAccessFlags : uint
    {
        QueryInformation = 0x0400,
        VMRead = 0x0010
    }
    public enum SECURITY_INFORMATION : uint
    {
        DACL_SECURITY_INFORMATION = 0x00000004
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ACL
    {
        public byte AclRevision;
        public byte Sbz1;
        public ushort AclSize;
        public ushort AceCount;
        public ushort Sbz2;
    }
    // 安全描述符结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_DESCRIPTOR
    {
        public byte Revision;
        public byte Sbz1;
        public short Control;
        public IntPtr Owner;
        public IntPtr Group;
        public IntPtr Sacl;
        public IntPtr Dacl;
    }

    // 安全属性结构体
    [StructLayout(LayoutKind.Sequential)]
    public class SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    // TOKEN_INFORMATION_CLASS 枚举
    public enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenOrigin,
        TokenElevationType,
        TokenLinkedToken,
        TokenElevation,
        TokenHasRestrictions,
        TokenAccessInformation,
        TokenVirtualizationAllowed,
        TokenVirtualizationEnabled,
        TokenIntegrityLevel,
        TokenUIAccess,
        TokenMandatoryPolicy,
        TokenLogonSid,
        TokenIsAppContainer,
        TokenCapabilities,
        TokenAppContainerSid,
        TokenAppContainerCheckOnly,
        TokenSid,
        TokenIntegrityLevelPtr,
        TokenIntegrityLevelMax
    }

    // TOKEN_TYPE 枚举
    public enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    // SECURITY_IMPERSONATION_LEVEL 枚举
    public enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    // STARTUPINFO 结构体
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public STARTF dwFlags;
        public ShowWindowCommand wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    // STARTF 枚举
    [Flags]
    public enum STARTF : uint
    {
        STARTF_USESHOWWINDOW = 0x00000001,
        STARTF_USESIZE = 0x00000002,
        STARTF_USEPOSITION = 0x00000004,
        STARTF_USECOUNTCHARS = 0x00000008,
        STARTF_USEFILLATTRIBUTE = 0x00000010,
        STARTF_RUNFULLSCREEN = 0x00000020,
        STARTF_FORCEONFEEDBACK = 0x00000040,
        STARTF_FORCEOFFFEEDBACK = 0x00000080,
        STARTF_USESTDHANDLES = 0x00000100
    }

    // ShowWindowCommand 枚举
    public enum ShowWindowCommand : short
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_NORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_SHOWMAXIMIZED = 3,
        SW_MAXIMIZE = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
        SW_FORCEMINIMIZE = 11
    }

    // PROCESS_INFORMATION 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    // Token 访问权限枚举
    [Flags]
    public enum TokenAccessLevels : uint
    {
        DELETE = 0x00010000,
        READ_CONTROL = 0x00020000,
        SYNCHRONIZE = 0x00100000,
        WRITE_DAC = 0x00040000,
        WRITE_OWNER = 0x00080000,
        STANDARD_RIGHTS_REQUIRED = 0x000F0000,
        STANDARD_RIGHTS_READ = 0x00020000,
        TOKEN_ASSIGN_PRIMARY = 0x0001,
        TOKEN_DUPLICATE = 0x0002,
        TOKEN_IMPERSONATE = 0x0004,
        TOKEN_QUERY = 0x0008,
        TOKEN_QUERY_SOURCE = 0x0010,
        TOKEN_ADJUST_PRIVILEGES = 0x0020,
        TOKEN_ADJUST_GROUPS = 0x0040,
        TOKEN_ADJUST_DEFAULT = 0x0080,
        TOKEN_ADJUST_SESSIONID = 0x0100,
        TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY),
        TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE | TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID)
    }

    public enum ACCESS_MODE
    {
        NOT_USED_ACCESS,
        GRANT_ACCESS,
        SET_ACCESS,
        DENY_ACCESS,
        REVOKE_ACCESS,
        SET_AUDIT_SUCCESS,
        SET_AUDIT_FAILURE
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TRUSTEE_W
    {
        public IntPtr pMultipleTrustee;
        public int MultipleTrusteeOperation;
        public TRUSTEE_FORM TrusteeForm;
        public TRUSTEE_TYPE TrusteeType;
        public IntPtr ptstrName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXPLICIT_ACCESS_W
    {
        public uint grfAccessPermissions;
        public ACCESS_MODE grfAccessMode;
        public uint grfInheritance;
        public TRUSTEE_W Trustee;
    }

    public enum TRUSTEE_FORM
    {
        TRUSTEE_IS_SID,
        TRUSTEE_IS_NAME,
        TRUSTEE_BAD_FORM,
        TRUSTEE_IS_OBJECTS_AND_SID,
        TRUSTEE_IS_OBJECTS_AND_NAME
    }

    public enum TRUSTEE_TYPE
    {
        TRUSTEE_IS_UNKNOWN,
        TRUSTEE_IS_USER,
        TRUSTEE_IS_GROUP,
        TRUSTEE_IS_DOMAIN,
        TRUSTEE_IS_ALIAS,
        TRUSTEE_IS_WELL_KNOWN_GROUP,
        TRUSTEE_IS_DELETED,
        TRUSTEE_IS_INVALID,
        TRUSTEE_IS_COMPUTER
    }


    [Flags]
    public enum PrivilegeAttributes
    {
      /// <summary>Privilège est désactivé.</summary>
      Disabled = 0,

      /// <summary>Privilège activé par défaut.</summary>
      EnabledByDefault = 1,

      /// <summary>Privilège est activé.</summary>
      Enabled = 2,

      /// <summary>Privilège est supprimé.</summary>
      Removed = 4,

      /// <summary>Privilège utilisé pour accéder à un objet ou un service.</summary>
      UsedForAccess = -2147483648
    }


    [Flags]
    public enum TokenAccessRights
    {
      /// <summary>Right to attach a primary token to a process.</summary>
      AssignPrimary = 0,

      /// <summary>Right to duplicate an access token.</summary>
      Duplicate = 1,

      /// <summary>Right to attach an impersonation access token to a process.</summary>
      Impersonate = 4,

      /// <summary>Right to query an access token.</summary>
      Query = 8,

      /// <summary>Right to query the source of an access token.</summary>
      QuerySource = 16,

      /// <summary>Right to enable or disable the privileges in an access token.</summary>
      AdjustPrivileges = 32,

      /// <summary>Right to adjust the attributes of the groups in an access token.</summary>
      AdjustGroups = 64,

      /// <summary>Right to change the default owner, primary group, or DACL of an access token.</summary>
      AdjustDefault = 128,

      /// <summary>Right to adjust the session ID of an access token.</summary>
      AdjustSessionId = 256,

      /// <summary>Combines all possible access rights for a token.</summary>
      AllAccess = AccessTypeMasks.StandardRightsRequired |
          AssignPrimary |
          Duplicate |
          Impersonate |
          Query |
          QuerySource |
          AdjustPrivileges |
          AdjustGroups |
          AdjustDefault |
          AdjustSessionId,

      /// <summary>Combines the standard rights required to read with <see cref="Query"/>.</summary>
      Read = AccessTypeMasks.StandardRightsRead |
          Query,

      /// <summary>Combines the standard rights required to write with <see cref="AdjustDefault"/>, <see cref="AdjustGroups"/> and <see cref="AdjustPrivileges"/>.</summary>
      Write = AccessTypeMasks.StandardRightsWrite |
          AdjustPrivileges |
          AdjustGroups |
          AdjustDefault,

      /// <summary>Combines the standard rights required to execute with <see cref="Impersonate"/>.</summary>
      Execute = AccessTypeMasks.StandardRightsExecute |
          Impersonate
    }

    [Flags]
    internal enum AccessTypeMasks
    {
      Delete = 65536,

      ReadControl = 131072,

      WriteDAC = 262144,

      WriteOwner = 524288,

      Synchronize = 1048576,

      StandardRightsRequired = 983040,

      StandardRightsRead = ReadControl,

      StandardRightsWrite = ReadControl,

      StandardRightsExecute = ReadControl,

      StandardRightsAll = 2031616,

      SpecificRightsAll = 65535
    }



    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AdjustTokenPrivileges(
        [In] IntPtr accessTokenHandle,
        [In, MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        [In] ref TOKEN_PRIVILEGES newState,
        [In] int bufferLength,
        [In, Out] ref TOKEN_PRIVILEGES previousState,
        [In, Out] ref int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(
        [In] IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);


    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeName(
       [In] string systemName,
       [In] ref LUID luid,
       [In, Out] StringBuilder name,
       [In, Out] ref int nameLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeValue(
        [In] string systemName,
        [In] string name,
        [In, Out] ref LUID luid);


    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(
        [In] IntPtr processHandle,
        [In] TokenAccessRights desiredAccess,
        [In, Out] ref IntPtr tokenHandle);

    [DllImport("advapi32.DLL")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImpersonateLoggedOnUser(IntPtr hToken); //handle to token for logged-on user

    // 声明 MakeAbsoluteSD 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool MakeAbsoluteSD(IntPtr pSelfRelativeSD, IntPtr pAbsoluteSD, ref uint lpdwAbsoluteSDSize, IntPtr pDacl, ref uint lpdwDaclSize, IntPtr pSacl, ref uint lpdwSaclSize, IntPtr pOwner, ref uint lpdwOwnerSize, IntPtr pPrimaryGroup, ref uint lpdwPrimaryGroupSize);

    // 声明 SetSecurityDescriptorDacl 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool SetSecurityDescriptorDacl(ref SECURITY_DESCRIPTOR pSecurityDescriptor, bool bDaclPresent, IntPtr pDacl, bool bDaclDefaulted);

    // 声明 SetKernelObjectSecurity 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool SetKernelObjectSecurity(IntPtr Handle, int SecurityInformation, [In] IntPtr pSecurityDescriptor);

    // 声明 OpenProcessToken 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, TokenAccessLevels DesiredAccess, out IntPtr TokenHandle);

    // 声明 DuplicateTokenEx 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(IntPtr hExistingToken, TokenAccessLevels dwDesiredAccess, IntPtr lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL ImpersonationLevel, TOKEN_TYPE TokenType, out IntPtr phNewToken);

    // 声明 CreateProcessAsUser 函数
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, SECURITY_ATTRIBUTES lpProcessAttributes, SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    // 声明 OpenProcess 函数
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    // 声明 BuildExplicitAccessWithName 函数
    // https://stackoverflow.com/questions/8500535/using-setentriesinacl-in-c-sharp-error-1332
    //  It works if you use Unicode version SetEntriesInAclW or if you explicitely set CharSet property.
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern void BuildExplicitAccessWithName(ref EXPLICIT_ACCESS_W pExplicitAccess, string pTrusteeName, uint AccessPermissions, ACCESS_MODE AccessMode, uint Inheritance);

    // 声明 GetKernelObjectSecurity 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetKernelObjectSecurity(IntPtr Handle, int RequestedInformation, IntPtr pSecurityDescriptor,
                                uint nLength, out uint lpnLengthNeeded);

    // 声明 HeapAlloc 函数
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, uint dwBytes);

    // 声明 HeapFree 函数
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

    // 声明 GetSecurityDescriptorDacl 函数
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetSecurityDescriptorDacl(IntPtr pSecurityDescriptor, out bool bDaclPresent, out IntPtr pDacl, out bool bDaclDefaulted);

    // 声明 SetEntriesInAcl 函数
    // https://stackoverflow.com/questions/8500535/using-setentriesinacl-in-c-sharp-error-1332
    //  It works if you use Unicode version SetEntriesInAclW or if you explicitely set CharSet property.
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint SetEntriesInAcl(uint cCountOfExplicitEntries, ref EXPLICIT_ACCESS_W pListOfExplicitEntries, IntPtr OldAcl, out IntPtr NewAcl);

    // 声明 ZeroMemory 函数
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void ZeroMemory(IntPtr addr, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern Int32 GetLastError();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
        /**********************************************************************/
        /*                        Fonction  MySetPrivilege                    */
        /**********************************************************************/
        public static bool MySetPrivilege (string sPrivilege, bool enablePrivilege)
        {
          bool blRc;
          TOKEN_PRIVILEGES newTP = new TOKEN_PRIVILEGES();
          TOKEN_PRIVILEGES oldTP = new TOKEN_PRIVILEGES();
          LUID luid = new LUID();
          int retrunLength = 0;
          IntPtr processToken= IntPtr.Zero;

          /* Récupération du token du processus local
          */
          blRc = OpenProcessToken(GetCurrentProcess(), TokenAccessRights.AllAccess, ref processToken);
          if (blRc == false)
            return false;

          /* Récupère le Local Unique Identificateur du privilège
          */
          blRc = LookupPrivilegeValue (null, sPrivilege, ref luid);
          if (blRc == false)
            return false;

          /* Etabli ou enlève le privilège
          */
          newTP.PrivilegeCount = 1;
          newTP.Privileges = new LUID_AND_ATTRIBUTES[64];
          newTP.Privileges[0].Luid = luid;

          if (enablePrivilege)
            newTP.Privileges[0].Attributes = (Int32)PrivilegeAttributes.Enabled;
           else
            newTP.Privileges[0].Attributes = (Int32)PrivilegeAttributes.Disabled;

          oldTP.PrivilegeCount = 64;
          oldTP.Privileges = new LUID_AND_ATTRIBUTES[64];
          blRc = AdjustTokenPrivileges(processToken,
                                        false,
                                        ref newTP, 
                                        16,
                                        ref oldTP,
                                        ref retrunLength);
          if (blRc == false)
          {
            Int32 iRc = GetLastError();
            return false;
          }
          return true;
        }

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        private static extern uint WTSQueryUserToken(uint SessionId, out IntPtr phToken);

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

        private const uint INVALID_SESSION_ID = 0xFFFFFFFF;
        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
        /// <devdoc>
        ///   Gets the user token from the currently active session. Application must be running within the context of the LocalSystem Account.
        ///  </devdoc>
        private static bool GetSessionUserToken(ref IntPtr phUserToken, string user_filter = null)
        {
            var bResult = false;
            IntPtr hImpersonationToken = IntPtr.Zero;
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

                    if ((user_filter == null && si.State == WTS_CONNECTSTATE_CLASS.WTSActive) || (user == user_filter))
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
                bResult = DuplicateTokenEx(hImpersonationToken, 0, IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary,
                    out phUserToken);

                //CloseHandle(hImpersonationToken);
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
            }

            log(string.Format("hImpersonationToken > IsInvalid = {0}", (hImpersonationToken == IntPtr.Zero)));
            //blRc = Securite.Win32.natif.MySetPrivilege(Securite.Win32.natif.TrustedComputingBase, false);

            return bResult;
        }
        // Ajusts privilege according to our needs for operating 
        // the things of the system releated.
        // Note: if it was successful, it will return a token handle
        //		 otherwise, it returns NULL.
        //		 when there is no longer use the returned token handle, 
        //		 you should manually release it using ResetPrivilege.
        /*private static IntPtr AdjustDebugPrivilege(bool enablePrivilege)
        {
            TOKEN_PRIVILEGES newTP = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES oldTP = new TOKEN_PRIVILEGES();
            IntPtr hToken;
            LUID luid = new LUID();
            int retrunLength = 0;

            // 
            // enable debug privilege
            // 

            if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                                    TokenAccessLevels.TOKEN_ADJUST_PRIVILEGES,
                                    out hToken))
            {
                return IntPtr.Zero;
            }

            if (!LookupPrivilegeValue(null, Debug, ref luid))
            {
                return IntPtr.Zero;
            }

            newTP.PrivilegeCount = 1;
            newTP.Privileges = new LUID_AND_ATTRIBUTES[64];
            newTP.Privileges[0].Luid = luid;

            if (enablePrivilege)
                newTP.Privileges[0].Attributes = (Int32)PrivilegeAttributes.Enabled;
            else
                newTP.Privileges[0].Attributes = (Int32)PrivilegeAttributes.Disabled;

            oldTP.PrivilegeCount = 64;
            oldTP.Privileges = new LUID_AND_ATTRIBUTES[64];
            bool blRc = AdjustTokenPrivileges(hToken,
                                          false,
                                          ref newTP,
                                          16,
                                          ref oldTP,
                                          ref retrunLength);
            if (blRc == false)
            {
                Int32 iRc = GetLastError();
                return IntPtr.Zero;
            }

            return hToken;
        }
        */
        /*private static uint GetProcessIdByName(string pszProcName)
        {
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                if (string.Equals(process.ProcessName, pszProcName, StringComparison.OrdinalIgnoreCase))
                {
                    return (uint)process.Id;
                }
            }

            return 0;
        }*/
        public static uint GetProcessIdByName(string pszProcName)
        {
            const uint TH32CS_SNAPPROCESS = 2;
            PROCESSENTRY32 pe = new PROCESSENTRY32();
            pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            uint dwPid = 0;
            bool bFound = false;

            IntPtr hSP = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (hSP != IntPtr.Zero)
            {
                if (Process32First(hSP, ref pe))
                {
                    do
                    {
                        if (String.Compare(pszProcName, pe.szExeFile, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            dwPid = pe.th32ProcessID;
                            bFound = true;
                            break;
                        }
                    } while (Process32Next(hSP, ref pe) && !bFound);

                    CloseHandle(hSP);

                    if (bFound)
                    {
                        return dwPid;
                    }
                }
            }
            return 0;
        }

        public static bool CreateSystemProcess(string szProcessName, out int dwProcessId, out int dwExitCode)
        {
            return CreateExplorerProcess(szProcessName, out dwProcessId, out dwExitCode);
        }

        public static bool CreateExplorerProcess(string szProcessName, out int dwProcessId, out int dwExitCode)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hToken = IntPtr.Zero, hNewToken = IntPtr.Zero;
            uint dwPid;

            IntPtr pOldDAcl = IntPtr.Zero;
            IntPtr pNewDAcl = IntPtr.Zero;
            bool bDAcl;
            bool bDefDAcl;
            uint dwRet;

            IntPtr pSacl = IntPtr.Zero;
            IntPtr pSidOwner = IntPtr.Zero;
            IntPtr pSidPrimary = IntPtr.Zero;
            uint dwAclSize = 0;
            uint dwSaclSize = 0;
            uint dwSidOwnLen = 0;
            uint dwSidPrimLen = 0;

            uint dwSDLen;
            EXPLICIT_ACCESS_W ea = new EXPLICIT_ACCESS_W();

            IntPtr pOrigSd = IntPtr.Zero;
            IntPtr pNewSd = IntPtr.Zero;

            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            string strUserName;

            MySetPrivilege(Debug, true);
            //MySetPrivilege("SeAssignPrimaryTokenPrivilege", true);
            //IntPtr hDebugToken = AdjustDebugPrivilege(true);

            // 
            // 选择 WINLOGON 进程 
            // 
            if ((dwPid = GetProcessIdByName("winlogon.exe")) == 0)
            {
                //::WriteToLog( "GetProcessId() to failed!\n" );   

                dwProcessId = 0;
                dwExitCode = 0;
                return false;
            }

            hProcess = OpenProcess((uint)(ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VMRead), false, dwPid);
            if (hProcess == IntPtr.Zero)
            {
                //TCHAR szErr[MAX_PATH];
                //stprintf(szErr, "OpenProcess() = %d\n", GetLastError() );   
                //::WriteToLog(szErr);

                dwProcessId = 0;
                dwExitCode = 0;
                return false;
            }


            if (!OpenProcessToken(hProcess, TokenAccessLevels.READ_CONTROL | TokenAccessLevels.WRITE_DAC, out hToken))
            {
                //TCHAR szErr[MAX_PATH];
                //stprintf(szErr, "OpenProcessToken() = %d\n", GetLastError() ); 
                //::WriteToLog(szErr);

                dwProcessId = 0;
                dwExitCode = 0;
                return false;
            }

            // 
            // 设置 ACE 具有所有访问权限 
            // 
            //strUserName = //GetUserNameByProcessId(GetCurrentProcessId());
            BuildExplicitAccessWithName(ref ea,
                "CURRENT_USER",
                (uint)TokenAccessLevels.TOKEN_ALL_ACCESS,
                ACCESS_MODE.GRANT_ACCESS,
                0);

            if (!GetKernelObjectSecurity(hToken,
                (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                pOrigSd,
                0,
                out dwSDLen))
            {
                // 
                // 第一次调用给出的参数肯定返回这个错误，这样做的目的是 
                // 为了得到原安全描述符 pOrigSd 的长度 
                // 
                if (Marshal.GetLastWin32Error() == 122) // ERROR_INSUFFICIENT_BUFFER
                {
                    pOrigSd = Marshal.AllocHGlobal((int)dwSDLen);
                    if (pOrigSd == IntPtr.Zero)
                    {
                        //::WriteToLog( "Allocate pSd memory to failed!\n" ); 

                        dwProcessId = 0;
                        dwExitCode = 0;
                        return false;
                    }

                    // 
                    // 再次调用才正确得到安全描述符 pOrigSd 
                    // 
                    if (!GetKernelObjectSecurity(hToken,
                        (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                        pOrigSd,
                        dwSDLen,
                        out dwSDLen))
                    {
                        //printf( "GetKernelObjectSecurity() = %d\n", GetLastError() ); 
                        dwProcessId = 0;
                        dwExitCode = 0;
                        return false;
                    }
                }
                else
                {
                    //printf( "GetKernelObjectSecurity() = %d\n", GetLastError() ); 
                    dwProcessId = 0;
                    dwExitCode = 0;
                    return false;
                }
            }

            // 
            // 得到原安全描述符的访问控制列表 ACL 
            // 
            if (!GetSecurityDescriptorDacl(pOrigSd, out bDAcl, out pOldDAcl, out bDefDAcl))
            {
                //printf( "GetSecurityDescriptorDacl() = %d\n", GetLastError() ); 

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

            // 
            // 生成新 ACE 权限的访问控制列表 ACL 
            // 
            dwRet = SetEntriesInAcl(1, ref ea, pOldDAcl, out pNewDAcl);
            if (dwRet != ERROR_SUCCESS)
            {
                //printf( "SetEntriesInAcl() = %d\n", GetLastError() ); 
                pNewDAcl = IntPtr.Zero;

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

            if (!MakeAbsoluteSD(pOrigSd,
                pNewSd,
                ref dwSDLen,
                pOldDAcl,
                ref dwAclSize,
                pSacl,
                ref dwSaclSize,
                pSidOwner,
                ref dwSidOwnLen,
                pSidPrimary,
                ref dwSidPrimLen))
            {
                // 
                // 第一次调用给出的参数肯定返回这个错误，这样做的目的是 
                // 为了创建新的安全描述符 pNewSd 而得到各项的长度 
                // 
                if (Marshal.GetLastWin32Error() == 122) //ERROR_INSUFFICIENT_BUFFER)
                {
                    pOldDAcl = Marshal.AllocHGlobal((int)dwAclSize);
                    pSacl = Marshal.AllocHGlobal((int)dwSaclSize);
                    pSidOwner = Marshal.AllocHGlobal((int)dwSidOwnLen);
                    pSidPrimary = Marshal.AllocHGlobal((int)dwSidPrimLen);
                    pNewSd = Marshal.AllocHGlobal((int)dwSDLen);

                    if (pOldDAcl == IntPtr.Zero ||
                        pSacl == IntPtr.Zero ||
                        pSidOwner == IntPtr.Zero ||
                        pSidPrimary == IntPtr.Zero ||
                        pNewSd == IntPtr.Zero)
                    {
                        //printf( "Allocate SID or ACL to failed!\n" ); 

                        dwProcessId = 0;
                        dwExitCode = 0;
                        goto Cleanup;
                    }

                    // 
                    // 再次调用才可以成功创建新的安全描述符 pNewSd 
                    // 但新的安全描述符仍然是原访问控制列表 ACL 
                    // 
                    if (!MakeAbsoluteSD(pOrigSd,
                        pNewSd,
                        ref dwSDLen,
                        pOldDAcl,
                        ref dwAclSize,
                        pSacl,
                        ref dwSaclSize,
                        pSidOwner,
                        ref dwSidOwnLen,
                        pSidPrimary,
                        ref dwSidPrimLen))
                    {
                        //printf( "MakeAbsoluteSD() = %d\n", GetLastError() ); 

                        dwProcessId = 0;
                        dwExitCode = 0;
                        goto Cleanup;
                    }
                }
                else
                {
                    //printf( "MakeAbsoluteSD() = %d\n", GetLastError() ); 

                    dwProcessId = 0;
                    dwExitCode = 0;
                    goto Cleanup;
                }
            }

            // 
            // 将具有所有访问权限的访问控制列表 pNewDAcl 加入到新的 
            // 安全描述符 pNewSd 中 
            // 
            SECURITY_DESCRIPTOR NewSd = Marshal.PtrToStructure<SECURITY_DESCRIPTOR>(pNewSd);
            if (!SetSecurityDescriptorDacl(ref NewSd, bDAcl, pNewDAcl, bDefDAcl))
            {
                //printf( "SetSecurityDescriptorDacl() = %d\n", GetLastError() ); 

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

            // 
            // 将新的安全描述符加到 TOKEN 中 
            // 
            if (!SetKernelObjectSecurity(hToken, (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, pNewSd))
            {
                //printf( "SetKernelObjectSecurity() = %d\n", GetLastError() ); 

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

            // 
            // 再次打开 WINLOGON 进程的 TOKEN，这时已经具有所有访问权限 
            // 
            if (!OpenProcessToken(hProcess, TokenAccessLevels.TOKEN_ALL_ACCESS, out hToken))
            {
                //printf( "OpenProcessToken() = %d\n", GetLastError() );   

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

            // 
            // 复制一份具有相同访问权限的 TOKEN 
            // 
            if (!DuplicateTokenEx(hToken,
                TokenAccessLevels.TOKEN_ALL_ACCESS,
                IntPtr.Zero,
                SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                TOKEN_TYPE.TokenPrimary,
                out hNewToken))
            {
                //printf( "DuplicateTokenEx() = %d\n", GetLastError() );   

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }


            si.cb = Marshal.SizeOf(si);

            // 
            // 不虚拟登陆用户的话，创建新进程会提示 
            // 1314 客户没有所需的特权错误 
            // 
            ImpersonateLoggedOnUser(hNewToken);


            // 
            // 我们仅仅是需要建立高权限进程，不用切换用户 
            // 所以也无需设置相关桌面，有了新 TOKEN 足够 
            // 


            // 
            // 利用具有所有权限的 TOKEN，创建高权限进程 
            // 
            if (!CreateProcessAsUser(hNewToken,
                null,
                szProcessName,
                null,
                null,
                true,
                (uint)(0x08000000 | 0x00000040), //CREATE_NO_WINDOW | IDLE_PRIORITY_CLASS, 
                IntPtr.Zero,
                null,
                si,
                out pi))
            {
                //CString strMsg;
                //TCHAR szErr[MAX_PATH];
                //stprintf(szErr, "CreateProcessAsUser() = %d\n", GetLastError() ); 
                //::WriteToLog(szErr);
                //printf( "CreateProcessAsUser() = %d\n", GetLastError() ); 
                //AfxMessageBox(strMsg);

                dwProcessId = 0;
                dwExitCode = 0;
                goto Cleanup;
            }

        Cleanup:
            if (pOrigSd != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pOrigSd);
            }
            if (pNewSd != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pNewSd);
            }
            if (pSidPrimary != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSidPrimary);
            }
            if (pSidOwner != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSidOwner);
            }
            if (pSacl != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pSacl);
            }
            if (pOldDAcl != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pOldDAcl);
            }

            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            CloseHandle(hToken);
            CloseHandle(hNewToken);
            CloseHandle(hProcess);

            //AdjustDebugPrivilege(false);
            MySetPrivilege(Debug, false);

            dwProcessId = pi.dwProcessId;
            dwExitCode = 0;

            return true;
        }
    }
}
