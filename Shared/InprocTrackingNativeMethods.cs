﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Interop methods for the FileTracker.dll interop APIs.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Methods that are invoked on FileTracker.dll in order to handle inproc tracking
    /// </summary>
    /// <comments>
    /// We want to P/Invoke to the FileTracker methods, but FileTracker.dll is not guaranteed to be on PATH (since it's 
    /// in the .NET Framework directory), and there is no DefaultDllImportSearchPath that explicitly points out at the 
    /// .NET Framework directory.  Thus, we are sneaking around P/Invoke by manually acquiring the method pointers and 
    /// calling them ourselves. The vast majority of this code was lifted from ndp\fx\src\CLRCompression\ZLibNative.cs, 
    /// which does the same thing for that assembly. 
    /// </comments>
    internal static class InprocTrackingNativeMethods
    {
        #region Delegates for the tracking functions

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int StartTrackingContextDelegate([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string taskName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int StartTrackingContextWithRootDelegate([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string taskName, [In, MarshalAs(UnmanagedType.LPWStr)] string rootMarker);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int EndTrackingContextDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int StopTrackingAndCleanupDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int SuspendTrackingDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int ResumeTrackingDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int WriteAllTLogsDelegate([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string tlogRootName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int WriteContextTLogsDelegate([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string tlogRootName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [SuppressUnmanagedCodeSecurity]
        private delegate int SetThreadCountDelegate(int threadCount);

        #endregion // Delegates for the tracking functions

        #region Public API

        internal static void StartTrackingContext([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string taskName)
        {
            int hresult = FileTrackerDllStub.startTrackingContextDelegate(intermediateDirectory, taskName);
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void StartTrackingContextWithRoot([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string taskName, [In, MarshalAs(UnmanagedType.LPWStr)] string rootMarker)
        {
            int hresult = FileTrackerDllStub.startTrackingContextWithRootDelegate(intermediateDirectory, taskName, rootMarker);
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void EndTrackingContext()
        {
            int hresult = FileTrackerDllStub.endTrackingContextDelegate();
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void StopTrackingAndCleanup()
        {
            int hresult = FileTrackerDllStub.stopTrackingAndCleanupDelegate();
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void SuspendTracking()
        {
            int hresult = FileTrackerDllStub.suspendTrackingDelegate();
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void ResumeTracking()
        {
            int hresult = FileTrackerDllStub.resumeTrackingDelegate();
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void WriteAllTLogs([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string tlogRootName)
        {
            int hresult = FileTrackerDllStub.writeAllTLogsDelegate(intermediateDirectory, tlogRootName);
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void WriteContextTLogs([In, MarshalAs(UnmanagedType.LPWStr)] string intermediateDirectory, [In, MarshalAs(UnmanagedType.LPWStr)] string tlogRootName)
        {
            int hresult = FileTrackerDllStub.writeContextTLogsDelegate(intermediateDirectory, tlogRootName);
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        internal static void SetThreadCount(int threadCount)
        {
            int hresult = FileTrackerDllStub.setThreadCountDelegate(threadCount);
            Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
        }

        #endregion // Public API

        private static class FileTrackerDllStub
        {
            private const string fileTrackerDllName = "FileTracker.dll";

            // Handle for FileTracker.dll itself
            [SecurityCritical]
            private static SafeHandle s_fileTrackerDllHandle;

            #region Function pointers to native functions

            internal static StartTrackingContextDelegate startTrackingContextDelegate;

            internal static StartTrackingContextWithRootDelegate startTrackingContextWithRootDelegate;

            internal static EndTrackingContextDelegate endTrackingContextDelegate;

            internal static StopTrackingAndCleanupDelegate stopTrackingAndCleanupDelegate;

            internal static SuspendTrackingDelegate suspendTrackingDelegate;

            internal static ResumeTrackingDelegate resumeTrackingDelegate;

            internal static WriteAllTLogsDelegate writeAllTLogsDelegate;

            internal static WriteContextTLogsDelegate writeContextTLogsDelegate;

            internal static SetThreadCountDelegate setThreadCountDelegate;

            #endregion  // Function pointers to native functions

            #region Declarations of Windows API needed to load the native library

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)]
            [ResourceExposure(ResourceScope.Process)]
            [SecurityCritical]
            private static extern IntPtr GetProcAddress(SafeHandle moduleHandle, String procName);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [ResourceExposure(ResourceScope.Machine)]
            [SecurityCritical]
            private static extern SafeLibraryHandle LoadLibrary(String libPath);

            #endregion // Declarations of Windows API needed to load the native library

            #region Initialization code

            /// <summary>
            /// Loads FileTracker.dll into a handle that we can use subsequently to grab the exported methods we're interested in. 
            /// </summary>
            private static void LoadFileTrackerDll()
            {
                // Get the FileTracker in the framework directory that matches the currently running process
                string frameworkDir = RuntimeEnvironment.GetRuntimeDirectory();
                string buildToolsPath = FrameworkLocationHelper.GeneratePathToBuildToolsForToolsVersion(MSBuildConstants.CurrentToolsVersion, DotNetFrameworkArchitecture.Current);
                string fileTrackerPath = Path.Combine(buildToolsPath, fileTrackerDllName);

                if (String.IsNullOrEmpty(fileTrackerPath) || !File.Exists(fileTrackerPath))
                {
                    fileTrackerPath = Path.Combine(frameworkDir, fileTrackerDllName);
                }

                if (!File.Exists(fileTrackerPath))
                {
                    throw new DllNotFoundException(fileTrackerDllName);
                }

                SafeLibraryHandle handle = LoadLibrary(fileTrackerPath);

                if (handle.IsInvalid)
                {
                    Int32 hresult = Marshal.GetHRForLastWin32Error();
                    Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));

                    // If Marshal.ThrowExceptionForHR did not throw, we still need to make sure to throw:
                    throw new InvalidOperationException();
                }

                s_fileTrackerDllHandle = handle;
            }

            /// <summary>
            /// Generic code to grab the function pointer for a function exported by FileTracker.dll, given 
            /// that function's name, and transform that function pointer into a callable delegate. 
            /// </summary>
            [SecurityCritical]
            private static DT CreateDelegate<DT>(String entryPointName)
            {
                IntPtr entryPoint = GetProcAddress(s_fileTrackerDllHandle, entryPointName);

                if (IntPtr.Zero == entryPoint)
                    throw new EntryPointNotFoundException(fileTrackerDllName + "!" + entryPointName);

                return (DT)(Object)Marshal.GetDelegateForFunctionPointer(entryPoint, typeof(DT));
            }

            /// <summary>
            /// Actually generate all of the delegates that will be called by our public (or rather, internal) surface area methods.  
            /// </summary>
            private static void InitDelegates()
            {
                ErrorUtilities.VerifyThrow(s_fileTrackerDllHandle != null, "fileTrackerDllHandle should not be null");
                ErrorUtilities.VerifyThrow(!s_fileTrackerDllHandle.IsInvalid, "Handle for FileTracker.dll should not be invalid");

                startTrackingContextDelegate = CreateDelegate<StartTrackingContextDelegate>("StartTrackingContext");
                startTrackingContextWithRootDelegate = CreateDelegate<StartTrackingContextWithRootDelegate>("StartTrackingContextWithRoot");
                endTrackingContextDelegate = CreateDelegate<EndTrackingContextDelegate>("EndTrackingContext");
                stopTrackingAndCleanupDelegate = CreateDelegate<StopTrackingAndCleanupDelegate>("StopTrackingAndCleanup");
                suspendTrackingDelegate = CreateDelegate<SuspendTrackingDelegate>("SuspendTracking");
                resumeTrackingDelegate = CreateDelegate<ResumeTrackingDelegate>("ResumeTracking");
                writeAllTLogsDelegate = CreateDelegate<WriteAllTLogsDelegate>("WriteAllTLogs");
                writeContextTLogsDelegate = CreateDelegate<WriteContextTLogsDelegate>("WriteContextTLogs");
                setThreadCountDelegate = CreateDelegate<SetThreadCountDelegate>("SetThreadCount");
            }

            /// <summary>
            /// Static constructor -- generates the delegates for all of the export methods from
            /// FileTracker.dll that we care about. 
            /// </summary>
            [SecuritySafeCritical]
            static FileTrackerDllStub()
            {
                LoadFileTrackerDll();
                InitDelegates();
            }

            #endregion  // Initialization code

            // Specialized handle to make sure we free FileTracker.dll 
            [SecurityCritical]
            private class SafeLibraryHandle : SafeHandle
            {
                internal SafeLibraryHandle()
                    : base(IntPtr.Zero, true)
                {
                }

                public override bool IsInvalid
                {
                    [SecurityCritical]
                    get
                    { return IntPtr.Zero == handle; }
                }

                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
                [SecurityCritical]
                protected override bool ReleaseHandle()
                {
                    // FileTracker expects to continue to exist even through ExitProcess -- if we forcibly unload it now, 
                    // bad things can happen when the CLR attempts to call the (still detoured?) ExitProcess.  
                    return true;
                }
            }  // private class SafeLibraryHandle
        }
    }
}