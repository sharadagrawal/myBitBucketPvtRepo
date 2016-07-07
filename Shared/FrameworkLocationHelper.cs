﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Win32;

using PropertyElement = Microsoft.Build.Evaluation.ToolsetElement.PropertyElement;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Used to specify the targeted bitness of the .NET Framework for some methods of FrameworkLocationHelper
    /// </summary>
    internal enum DotNetFrameworkArchitecture
    {
        /// <summary>
        /// Indicates the .NET Framework that is currently being run under.  
        /// </summary>
        Current = 0,

        /// <summary>
        /// Indicates the 32-bit .NET Framework
        /// </summary>
        Bitness32 = 1,

        /// <summary>
        /// Indicates the 64-bit .NET Framework
        /// </summary>
        Bitness64 = 2
    }

    /// <summary>
    /// FrameworkLocationHelper provides utility methods for locating .NET Framework and .NET Framework SDK directories and files
    /// </summary>
    internal static class FrameworkLocationHelper
    {
        #region Constants

        internal const string dotNetFrameworkIdentifier = ".NETFramework";

        // .net versions.
        internal static readonly Version dotNetFrameworkVersion11 = new Version(1, 1);
        internal static readonly Version dotNetFrameworkVersion20 = new Version(2, 0);
        internal static readonly Version dotNetFrameworkVersion30 = new Version(3, 0);
        internal static readonly Version dotNetFrameworkVersion35 = new Version(3, 5);
        internal static readonly Version dotNetFrameworkVersion40 = new Version(4, 0);
        internal static readonly Version dotNetFrameworkVersion45 = new Version(4, 5);
        internal static readonly Version dotNetFrameworkVersion451 = new Version(4, 5, 1);
        internal static readonly Version dotNetFrameworkVersion46 = new Version(4, 6);

        // visual studio versions.
        internal static readonly Version visualStudioVersion100 = new Version(10, 0);
        internal static readonly Version visualStudioVersion110 = new Version(11, 0);
        internal static readonly Version visualStudioVersion120 = new Version(12, 0);
        internal static readonly Version visualStudioVersion140 = new Version(14, 0);

        // keep this up-to-date; always point to the latest visual studio version.
        internal static readonly Version visualStudioVersionLatest = visualStudioVersion140;

        private const string dotNetFrameworkRegistryPath = "SOFTWARE\\Microsoft\\.NETFramework";
        private const string dotNetFrameworkSetupRegistryPath = "SOFTWARE\\Microsoft\\NET Framework Setup\\NDP";
        private const string dotNetFrameworkSetupRegistryInstalledName = "Install";

        internal const string fullDotNetFrameworkRegistryKey = "HKEY_LOCAL_MACHINE\\" + dotNetFrameworkRegistryPath;
        private const string dotNetFrameworkAssemblyFoldersRegistryPath = dotNetFrameworkRegistryPath + "\\AssemblyFolders";
        private const string referenceAssembliesRegistryValueName = "All Assemblies In";

        internal const string dotNetFrameworkSdkInstallKeyValueV11 = "SDKInstallRootv1.1";
        internal const string dotNetFrameworkVersionFolderPrefixV11 = "v1.1"; // v1.1 is for Everett.
        private const string dotNetFrameworkVersionV11 = "v1.1.4322"; // full Everett version to pass to NativeMethodsShared.GetRequestedRuntimeInfo().
        private const string dotNetFrameworkRegistryKeyV11 = dotNetFrameworkSetupRegistryPath + "\\" + dotNetFrameworkVersionV11;

        internal const string dotNetFrameworkSdkInstallKeyValueV20 = "SDKInstallRootv2.0";
        internal const string dotNetFrameworkVersionFolderPrefixV20 = "v2.0"; // v2.0 is for Whidbey.
        private const string dotNetFrameworkVersionV20 = "v2.0.50727"; // full Whidbey version to pass to NativeMethodsShared.GetRequestedRuntimeInfo().
        private const string dotNetFrameworkRegistryKeyV20 = dotNetFrameworkSetupRegistryPath + "\\" + dotNetFrameworkVersionV20;

        internal const string dotNetFrameworkVersionFolderPrefixV30 = "v3.0"; // v3.0 is for WinFx.
        private const string dotNetFrameworkVersionV30 = "v3.0"; // full WinFx version to pass to NativeMethodsShared.GetRequestedRuntimeInfo().
        private const string dotNetFrameworkAssemblyFoldersRegistryKeyV30 = dotNetFrameworkAssemblyFoldersRegistryPath + "\\" + dotNetFrameworkVersionFolderPrefixV30;
        private const string dotNetFrameworkRegistryKeyV30 = dotNetFrameworkSetupRegistryPath + "\\" + dotNetFrameworkVersionFolderPrefixV30 + "\\Setup";

        private const string fallbackDotNetFrameworkSdkRegistryInstallPath = "SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows";
        internal const string fallbackDotNetFrameworkSdkInstallKeyValue = "CurrentInstallFolder";

        private const string dotNetFrameworkSdkRegistryPathForV35ToolsOnWinSDK70A = @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A\WinSDK-NetFx35Tools-x86";
        private const string fullDotNetFrameworkSdkRegistryPathForV35ToolsOnWinSDK70A = "HKEY_LOCAL_MACHINE\\" + dotNetFrameworkSdkRegistryPathForV35ToolsOnWinSDK70A;

        private const string dotNetFrameworkSdkRegistryPathForV35ToolsOnManagedToolsSDK80A = @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx35Tools-x86";
        private const string fullDotNetFrameworkSdkRegistryPathForV35ToolsOnManagedToolsSDK80A = "HKEY_LOCAL_MACHINE\\" + dotNetFrameworkSdkRegistryPathForV35ToolsOnManagedToolsSDK80A;

        private const string dotNetFrameworkRegistryKeyV35 = dotNetFrameworkSetupRegistryPath + "\\" + dotNetFrameworkVersionFolderPrefixV35;
        internal const string dotNetFrameworkVersionFolderPrefixV35 = "v3.5"; // v3.5 is for Orcas.

        internal const string fullDotNetFrameworkSdkRegistryKeyV35OnVS10 = fullDotNetFrameworkSdkRegistryPathForV35ToolsOnWinSDK70A;
        internal const string fullDotNetFrameworkSdkRegistryKeyV35OnVS11 = fullDotNetFrameworkSdkRegistryPathForV35ToolsOnManagedToolsSDK80A;

        internal const string dotNetFrameworkVersionFolderPrefixV40 = "v4.0";

        /// <summary>
        /// Path to the ToolsVersion definitions in the registry
        /// </summary>
        private const string ToolsVersionsRegistryPath = @"SOFTWARE\Microsoft\MSBuild\ToolsVersions";

        #endregion // Constants

        #region Delegates

        // This way, the methods that take these as parameters can also be overridden to do different things
        // in unit tests. 
        private static readonly GetDirectories s_getDirectories = new GetDirectories(Directory.GetDirectories);
        private static readonly DirectoryExists s_directoryExists = new DirectoryExists(Directory.Exists);

        #endregion // Delegates

        #region Static member variables

        /// <summary>
        /// By default when a root path is not specified we would like to use the program files directory \ reference assemblies\framework as the root location
        /// to generate the reference assembly paths from.
        /// </summary>
        internal static readonly string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        internal static readonly string programFiles32 = GenerateProgramFiles32();
        internal static readonly string programFiles64 = GenerateProgramFiles64();
        internal static readonly string programFilesReferenceAssemblyLocation = GenerateProgramFilesReferenceAssemblyRoot();

        private static string s_fallbackDotNetFrameworkSdkInstallPath;

        private static string s_pathToV35ToolsInFallbackDotNetFrameworkSdk;

        private static string s_pathToV4ToolsInFallbackDotNetFrameworkSdk;

        /// <summary>
        /// List the supported .net versions.
        /// </summary>
        private static readonly DotNetFrameworkSpec[] s_dotNetFrameworkSpecs =
        {
            // v1.1
            new DotNetFrameworkSpecLegacy(
                dotNetFrameworkVersion11,
                dotNetFrameworkRegistryKeyV11,
                dotNetFrameworkSetupRegistryInstalledName,
                dotNetFrameworkVersionFolderPrefixV11,
                dotNetFrameworkSdkInstallKeyValueV11,
                hasMSBuild: false),

            // v2.0
            new DotNetFrameworkSpecLegacy(
                dotNetFrameworkVersion20,
                dotNetFrameworkRegistryKeyV20,
                dotNetFrameworkSetupRegistryInstalledName,
                dotNetFrameworkVersionFolderPrefixV20,
                dotNetFrameworkSdkInstallKeyValueV20,
                hasMSBuild: true),

            // v3.0
            new DotNetFrameworkSpecV3(
                dotNetFrameworkVersion30,
                dotNetFrameworkRegistryKeyV30,
                "InstallSuccess",
                dotNetFrameworkVersionFolderPrefixV30,
                null,
                null,
                hasMSBuild: false),

            // v3.5
            new DotNetFrameworkSpecV3(
                dotNetFrameworkVersion35,
                dotNetFrameworkRegistryKeyV35,
                dotNetFrameworkSetupRegistryInstalledName,
                dotNetFrameworkVersionFolderPrefixV35,
                "WinSDK-NetFx35Tools-x86",
                "InstallationFolder",
                hasMSBuild: true),

            // v4.0
            CreateDotNetFrameworkSpecForV4(dotNetFrameworkVersion40, visualStudioVersion100),

            // v4.5
            CreateDotNetFrameworkSpecForV4(dotNetFrameworkVersion45, visualStudioVersion110),

            // v4.5.1
            CreateDotNetFrameworkSpecForV4(dotNetFrameworkVersion451, visualStudioVersion120),

            // v4.6
            CreateDotNetFrameworkSpecForV4(dotNetFrameworkVersion46, visualStudioVersion140),
        };

        /// <summary>
        /// List the supported visual studio versions.
        /// </summary>
        /// <remarks>
        /// The items must be ordered by the version, because some methods depend on that fact to find the previous visual studio version.
        /// </remarks>
        private static readonly VisualStudioSpec[] s_visualStudioSpecs =
        {
            // VS10
            new VisualStudioSpec(visualStudioVersion100, "Windows\\v7.0A", null, null, new []
            {
                dotNetFrameworkVersion11,
                dotNetFrameworkVersion20,
                dotNetFrameworkVersion35,
                dotNetFrameworkVersion40,
            }),

            // VS11
            new VisualStudioSpec(visualStudioVersion110, "Windows\\v8.0A", "v8.0", "InstallationFolder", new []
            {
                dotNetFrameworkVersion11,
                dotNetFrameworkVersion20,
                dotNetFrameworkVersion35,
                dotNetFrameworkVersion40,
                dotNetFrameworkVersion45,
            }),

            // VS12
            new VisualStudioSpec(visualStudioVersion120, "Windows\\v8.1A", "v8.1", "InstallationFolder", new []
            {
                dotNetFrameworkVersion11,
                dotNetFrameworkVersion20,
                dotNetFrameworkVersion35,
                dotNetFrameworkVersion40,
                dotNetFrameworkVersion45,
                dotNetFrameworkVersion451,
            }),

            // VS14
            new VisualStudioSpec(visualStudioVersion140, "NETFXSDK\\4.6", "v8.1", "InstallationFolder", new []
            {
                dotNetFrameworkVersion11,
                dotNetFrameworkVersion20,
                dotNetFrameworkVersion35,
                dotNetFrameworkVersion40,
                dotNetFrameworkVersion45,
                dotNetFrameworkVersion451,
                dotNetFrameworkVersion46,
            }),
        };

        /// <summary>
        /// Define explicit fallback rules for the request to get path of .net framework sdk tools folder.
        /// The default rule is fallback to previous VS. However, there are some special cases that need
        /// explicit rules, i.e. v4.5.1 on VS12 fallbacks to v4.5 on VS12.
        /// </summary>
        /// <remarks>
        /// The rules are maintained in a 2-dimensions array. Each row defines a rule. The first column
        /// defines the trigger condition. The second column defines the fallback .net and VS versions.
        /// </remarks>
        private static readonly Tuple<Version, Version>[,] s_explicitFallbackRulesForPathToDotNetFrameworkSdkTools =
        {
            // VS12
            { Tuple.Create(dotNetFrameworkVersion451, visualStudioVersion120), Tuple.Create(dotNetFrameworkVersion45, visualStudioVersion120) },

            // VS14
            { Tuple.Create(dotNetFrameworkVersion451, visualStudioVersion140), Tuple.Create(dotNetFrameworkVersion45, visualStudioVersion140) },
            { Tuple.Create(dotNetFrameworkVersion46, visualStudioVersion140), Tuple.Create(dotNetFrameworkVersion451, visualStudioVersion140) },
        };

        private static readonly IReadOnlyDictionary<Version, DotNetFrameworkSpec> s_dotNetFrameworkSpecDict;
        private static readonly IReadOnlyDictionary<Version, VisualStudioSpec> s_visualStudioSpecDict;

        #endregion // Static member variables

        static FrameworkLocationHelper()
        {
            s_dotNetFrameworkSpecDict = s_dotNetFrameworkSpecs.ToDictionary(spec => spec.Version);
            s_visualStudioSpecDict = s_visualStudioSpecs.ToDictionary(spec => spec.Version);
        }

        #region Static properties

        internal static string PathToDotNetFrameworkV11
        {
            get
            {
                return GetPathToDotNetFrameworkV11(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkV20
        {
            get
            {
                return GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkV30
        {
            get
            {
                return GetPathToDotNetFrameworkV30(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkV35
        {
            get
            {
                return GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkV40
        {
            get
            {
                return GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkV45
        {
            get
            {
                return GetPathToDotNetFrameworkV45(DotNetFrameworkArchitecture.Current);
            }
        }

        internal static string PathToDotNetFrameworkSdkV11
        {
            get
            {
                return GetPathToDotNetFrameworkSdkTools(dotNetFrameworkVersion11, visualStudioVersionLatest);
            }
        }

        internal static string PathToDotNetFrameworkSdkV20
        {
            get
            {
                return GetPathToDotNetFrameworkSdkTools(dotNetFrameworkVersion20, visualStudioVersionLatest);
            }
        }

        /// <summary>
        /// Because there is no longer a strong 1:1 mapping between FX versions and SDK
        /// versions, if we're unable to locate the desired SDK version, we will try to 
        /// use whichever SDK version is installed by looking at the key pointing to the
        /// "latest" version.
        ///
        /// This isn't ideal, but it will allow our tasks to function on any of several 
        /// related SDKs even if they don't have exactly the same versions.
        /// 
        /// NOTE:  This returns the path to the root of the fallback SDK
        /// </summary>
        private static string FallbackDotNetFrameworkSdkInstallPath
        {
            get
            {
                if (s_fallbackDotNetFrameworkSdkInstallPath == null)
                {
                    s_fallbackDotNetFrameworkSdkInstallPath = FindRegistryValueUnderKey(fallbackDotNetFrameworkSdkRegistryInstallPath, fallbackDotNetFrameworkSdkInstallKeyValue);

                    if (Environment.Is64BitProcess && s_fallbackDotNetFrameworkSdkInstallPath == null)
                    {
                        // Since we're 64-bit, what we just checked was the 64-bit fallback key -- so now let's 
                        // check the 32-bit one too, just in case. 
                        s_fallbackDotNetFrameworkSdkInstallPath = FindRegistryValueUnderKey(fallbackDotNetFrameworkSdkRegistryInstallPath, fallbackDotNetFrameworkSdkInstallKeyValue, RegistryView.Registry32);
                    }
                }

                return s_fallbackDotNetFrameworkSdkInstallPath;
            }
        }

        /// <summary>
        /// Because there is no longer a strong 1:1 mapping between FX versions and SDK
        /// versions, if we're unable to locate the desired SDK version, we will try to 
        /// use whichever SDK version is installed by looking at the key pointing to the
        /// "latest" version.
        ///
        /// This isn't ideal, but it will allow our tasks to function on any of several 
        /// related SDKs even if they don't have exactly the same versions.
        /// 
        /// NOTE:  This explicitly returns the path to the 3.5 tools (bin) under the fallback
        /// SDK, to match the data we're pulling from the registry now.  
        /// </summary>
        private static string PathToV35ToolsInFallbackDotNetFrameworkSdk
        {
            get
            {
                if (s_pathToV35ToolsInFallbackDotNetFrameworkSdk == null)
                {
                    if (FallbackDotNetFrameworkSdkInstallPath != null)
                    {
                        bool endsWithASlash = false;

                        if (FallbackDotNetFrameworkSdkInstallPath.EndsWith("\\", StringComparison.Ordinal))
                        {
                            endsWithASlash = true;
                        }

                        s_pathToV35ToolsInFallbackDotNetFrameworkSdk = Path.Combine(FallbackDotNetFrameworkSdkInstallPath, "bin");

                        // Path.Combine leaves no trailing slash, so if we had one before, be sure to add it back in
                        if (endsWithASlash)
                        {
                            s_pathToV35ToolsInFallbackDotNetFrameworkSdk = s_pathToV35ToolsInFallbackDotNetFrameworkSdk + "\\";
                        }
                    }
                }

                return s_pathToV35ToolsInFallbackDotNetFrameworkSdk;
            }
        }

        /// <summary>
        /// Because there is no longer a strong 1:1 mapping between FX versions and SDK
        /// versions, if we're unable to locate the desired SDK version, we will try to 
        /// use whichever SDK version is installed by looking at the key pointing to the
        /// "latest" version.
        ///
        /// This isn't ideal, but it will allow our tasks to function on any of several 
        /// related SDKs even if they don't have exactly the same versions.
        /// 
        /// NOTE:  This explicitly returns the path to the 4.X tools (bin\NetFX 4.0 Tools) 
        /// under the fallback SDK, to match the data we're pulling from the registry now.  
        /// </summary>
        private static string PathToV4ToolsInFallbackDotNetFrameworkSdk
        {
            get
            {
                if (s_pathToV4ToolsInFallbackDotNetFrameworkSdk == null)
                {
                    if (FallbackDotNetFrameworkSdkInstallPath != null)
                    {
                        bool endsWithASlash = false;

                        if (FallbackDotNetFrameworkSdkInstallPath.EndsWith("\\", StringComparison.Ordinal))
                        {
                            endsWithASlash = true;
                        }

                        s_pathToV4ToolsInFallbackDotNetFrameworkSdk = Path.Combine(FallbackDotNetFrameworkSdkInstallPath, "bin", "NetFX 4.0 Tools");

                        // Path.Combine leaves no trailing slash, so if we had one before, be sure to add it back in
                        if (endsWithASlash)
                        {
                            s_pathToV4ToolsInFallbackDotNetFrameworkSdk = s_pathToV4ToolsInFallbackDotNetFrameworkSdk + "\\";
                        }
                    }
                }

                return s_pathToV4ToolsInFallbackDotNetFrameworkSdk;
            }
        }

        #endregion // Static properties

        #region Internal methods

        internal static string GetDotNetFrameworkSdkRootRegistryKey(Version dotNetFrameworkVersion, Version visualStudioVersion)
        {
            RedirectVersionsIfNecessary(ref dotNetFrameworkVersion, ref visualStudioVersion);

            var dotNetFrameworkSpec = GetDotNetFrameworkSpec(dotNetFrameworkVersion);
            var visualStudioSpec = GetVisualStudioSpec(visualStudioVersion);
            ErrorUtilities.VerifyThrowArgument(visualStudioSpec.SupportedDotNetFrameworkVersions.Contains(dotNetFrameworkVersion), "FrameworkLocationHelper.UnsupportedFrameworkVersion", dotNetFrameworkVersion);
            return dotNetFrameworkSpec.GetDotNetFrameworkSdkRootRegistryKey(visualStudioSpec);
        }

        internal static string GetDotNetFrameworkSdkInstallKeyValue(Version dotNetFrameworkVersion, Version visualStudioVersion)
        {
            RedirectVersionsIfNecessary(ref dotNetFrameworkVersion, ref visualStudioVersion);

            var dotNetFrameworkSpec = GetDotNetFrameworkSpec(dotNetFrameworkVersion);
            var visualStudioSpec = GetVisualStudioSpec(visualStudioVersion);
            ErrorUtilities.VerifyThrowArgument(visualStudioSpec.SupportedDotNetFrameworkVersions.Contains(dotNetFrameworkVersion), "FrameworkLocationHelper.UnsupportedFrameworkVersion", dotNetFrameworkVersion);
            return dotNetFrameworkSpec.DotNetFrameworkSdkRegistryInstallationFolderName;
        }

        internal static string GetDotNetFrameworkVersionFolderPrefix(Version dotNetFrameworkVersion)
        {
            return GetDotNetFrameworkSpec(dotNetFrameworkVersion).DotNetFrameworkFolderPrefix;
        }

        internal static string GetPathToWindowsSdk(Version dotNetFrameworkVersion)
        {
            return GetDotNetFrameworkSpec(dotNetFrameworkVersion).GetPathToWindowsSdk();
        }

        internal static string GetPathToDotNetFrameworkReferenceAssemblies(Version dotNetFrameworkVersion)
        {
            return GetDotNetFrameworkSpec(dotNetFrameworkVersion).GetPathToDotNetFrameworkReferenceAssemblies();
        }

        internal static string GetPathToDotNetFrameworkSdkTools(Version dotNetFrameworkVersion, Version visualStudioVersion)
        {
            RedirectVersionsIfNecessary(ref dotNetFrameworkVersion, ref visualStudioVersion);

            var dotNetFrameworkSpec = GetDotNetFrameworkSpec(dotNetFrameworkVersion);
            var visualStudioSpec = GetVisualStudioSpec(visualStudioVersion);
            ErrorUtilities.VerifyThrowArgument(visualStudioSpec.SupportedDotNetFrameworkVersions.Contains(dotNetFrameworkVersion), "FrameworkLocationHelper.UnsupportedFrameworkVersion", dotNetFrameworkVersion);
            return dotNetFrameworkSpec.GetPathToDotNetFrameworkSdkTools(visualStudioSpec);
        }

        internal static string GetPathToDotNetFrameworkSdk(Version dotNetFrameworkVersion, Version visualStudioVersion)
        {
            RedirectVersionsIfNecessary(ref dotNetFrameworkVersion, ref visualStudioVersion);

            var dotNetFrameworkSpec = GetDotNetFrameworkSpec(dotNetFrameworkVersion);
            var visualStudioSpec = GetVisualStudioSpec(visualStudioVersion);
            ErrorUtilities.VerifyThrowArgument(visualStudioSpec.SupportedDotNetFrameworkVersions.Contains(dotNetFrameworkVersion), "FrameworkLocationHelper.UnsupportedFrameworkVersion", dotNetFrameworkVersion);
            return dotNetFrameworkSpec.GetPathToDotNetFrameworkSdk(visualStudioSpec);
        }

        internal static string GetPathToDotNetFrameworkV11(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion11, architecture);
        }

        internal static string GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion20, architecture);
        }

        internal static string GetPathToDotNetFrameworkV30(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion30, architecture);
        }

        internal static string GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion35, architecture);
        }

        internal static string GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion40, architecture);
        }

        internal static string GetPathToDotNetFrameworkV45(DotNetFrameworkArchitecture architecture)
        {
            return GetPathToDotNetFramework(dotNetFrameworkVersion45, architecture);
        }

        internal static string GetPathToDotNetFramework(Version version)
        {
            return GetPathToDotNetFramework(version, DotNetFrameworkArchitecture.Current);
        }

        internal static string GetPathToDotNetFramework(Version version, DotNetFrameworkArchitecture architecture)
        {
            return GetDotNetFrameworkSpec(version).GetPathToDotNetFramework(architecture);
        }

        /// <summary>
        /// Check the registry key and value to see if the .net Framework is installed on the machine.
        /// </summary>
        /// <param name="registryEntryToCheckInstall">Registry path to look for the value</param>
        /// <param name="registryValueToCheckInstall">Key to retrieve the value from</param>
        /// <returns>True if the registry key is 1 false if it is not there. This method also return true if the complus enviornment variables are set.</returns>
        private static bool CheckForFrameworkInstallation(string registryEntryToCheckInstall, string registryValueToCheckInstall)
        {
            // Get the complus install root and version
            string complusInstallRoot = Environment.GetEnvironmentVariable("COMPLUS_INSTALLROOT");
            string complusVersion = Environment.GetEnvironmentVariable("COMPLUS_VERSION");

            // Complus is not set we need to make sure the framework we are targeting is installed. Check the registry key before trying to find the directory.
            // If complus is set then we will return that directory as the framework directory, there is no need to check the registry value for the framework and it may not even be installed.
            if (String.IsNullOrEmpty(complusInstallRoot) && String.IsNullOrEmpty(complusVersion))
            {
                // If the registry entry is 1 then the framework is installed. Go ahead and find the directory. If it is not 1 then the framework is not installed, return null.
                return String.Compare("1", FindRegistryValueUnderKey(registryEntryToCheckInstall, registryValueToCheckInstall), StringComparison.OrdinalIgnoreCase) == 0;
            }

            return true;
        }

        /// <summary>
        /// Heuristic that first considers the current runtime path and then searches the base of that path for the given
        /// frameworks version.
        /// </summary>
        /// <param name="currentRuntimePath">The path to the runtime that is currently executing.</param>
        /// <param name="prefix">Should be something like 'v1.2' that indicates the runtime version we want.</param>
        /// <param name="frameworkVersion">Should be the full version number of the runtime version we want.</param>
        /// <param name="getDirectories">Delegate to method that can return filesystem entries.</param>
        /// <param name="useHeuristic">Whether we should fall back to a search heuristic if other searches fail.</param>
        /// <returns>Will return 'null' if there is no target frameworks on this machine.</returns>
        internal static string FindDotNetFrameworkPath
        (
            string currentRuntimePath,
            string prefix,
            DirectoryExists directoryExists,
            GetDirectories getDirectories,
            DotNetFrameworkArchitecture architecture
        )
        {
            // If the COMPLUS variables are set, they override everything -- that's the directory we want.  
            string complusInstallRoot = Environment.GetEnvironmentVariable("COMPLUS_INSTALLROOT");
            string complusVersion = Environment.GetEnvironmentVariable("COMPLUS_VERSION");

            if (!String.IsNullOrEmpty(complusInstallRoot) && !String.IsNullOrEmpty(complusVersion))
            {
                return Path.Combine(complusInstallRoot, complusVersion);
            }

            // If the current runtime starts with correct prefix, then this is the runtime we want to use.
            // However, only if we're requesting current architecture -- otherwise, the base path may be different, so we'll need to look it up. 
            string leaf = Path.GetFileName(currentRuntimePath);
            if (leaf.StartsWith(prefix, StringComparison.Ordinal) && architecture == DotNetFrameworkArchitecture.Current)
            {
                return currentRuntimePath;
            }

            // We haven't managed to use exact methods to locate the FX, so
            // search for the correct path with a heuristic.
            string baseLocation = Path.GetDirectoryName(currentRuntimePath);
            string searchPattern = prefix + "*";

            int indexOfFramework64 = baseLocation.IndexOf("Framework64", StringComparison.OrdinalIgnoreCase);

            if (indexOfFramework64 != -1 && architecture == DotNetFrameworkArchitecture.Bitness32)
            {
                // need to get rid of just the 64, but want to look up 'Framework64' rather than '64' to avoid the case where 
                // the path is something like 'C:\MyPath\64\Framework64'.  9 = length of 'Framework', to make the index match 
                // the location of the '64'. 
                int indexOf64 = indexOfFramework64 + 9;
                string tempLocation = baseLocation;
                baseLocation = tempLocation.Substring(0, indexOf64) + tempLocation.Substring(indexOf64 + 2, tempLocation.Length - indexOf64 - 2);
            }
            else if (indexOfFramework64 == -1 && architecture == DotNetFrameworkArchitecture.Bitness64)
            {
                // need to add 64 -- since this is a heuristic, we assume that we just need to append.  
                baseLocation = baseLocation + "64";
            }
            // we don't need to do anything if it's DotNetFrameworkArchitecture.Current.  

            string[] directories;

            if (directoryExists(baseLocation))
            {
                directories = getDirectories(baseLocation, searchPattern);
            }
            else
            {
                // If we can't even find the base path, might as well give up now. 
                return null;
            }

            if (directories.Length == 0)
            {
                // Couldn't find the path, return a null.
                return null;
            }

            // We don't care which one we choose, but we want to be predictible.
            // The intention here is to choose the alphabetical maximum.
            string max = directories[0];

            // the max.EndsWith condition: pre beta 2 versions of v3.5 have build number like v3.5.20111.  
            // This was removed in beta2
            // We should favor \v3.5 over \v3.5.xxxxx
            // versions previous to 2.0 have .xxxx version numbers.  3.0 and 3.5 do not.
            if (!max.EndsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i < directories.Length; ++i)
                {
                    if (directories[i].EndsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        max = directories[i];
                        break;
                    }
                    else if (String.Compare(directories[i], max, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        max = directories[i];
                    }
                }
            }

            return max;
        }

        /// <summary>
        /// Determine the 32 bit program files directory, this is used for finding where the reference assemblies live.
        /// </summary>
        internal static string GenerateProgramFiles32()
        {
            // On a 64 bit machine we always want to use the program files x86.  If we are running as a 64 bit process then this variable will be set correctly
            // If we are on a 32 bit machine or running as a 32 bit process then this variable will be null and the programFiles variable will be correct.
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (String.IsNullOrEmpty(programFilesX86))
            {
                // 32 bit box
                programFilesX86 = programFiles;
            }

            return programFilesX86;
        }

        /// <summary>
        /// Determine the 64-bit program files directory, used as the basis for MSBuildExtensionsPath64.
        /// Returns null if we're not on a 64-bit machine
        /// </summary>
        internal static string GenerateProgramFiles64()
        {
            string programFilesX64 = null;
            if (string.Equals(programFiles, programFiles32))
            {
                // either we're in a 32-bit window, or we're on a 32-bit machine.  
                // if we're on a 32-bit machine, ProgramW6432 won't exist
                // if we're on a 64-bit machine, ProgramW6432 will point to the correct Program Files. 
                programFilesX64 = Environment.GetEnvironmentVariable("ProgramW6432");
            }
            else
            {
                // 64-bit window on a 64-bit machine; %ProgramFiles% points to the 64-bit 
                // Program Files already. 
                programFilesX64 = programFiles;
            }

            return programFilesX64;
        }

        /// <summary>
        /// Generate the path to the program files reference assembly location by taking in the program files special folder and then 
        /// using that path to generate the path to the reference assemblies location.
        /// </summary>
        internal static string GenerateProgramFilesReferenceAssemblyRoot()
        {
            string combinedPath = Path.Combine(programFiles32, "Reference Assemblies\\Microsoft\\Framework");
            return Path.GetFullPath(combinedPath);
        }

        /// <summary>
        /// Given a ToolsVersion, find the path to the build tools folder for that ToolsVersion. 
        /// </summary>
        /// <param name="toolsVersion">The ToolsVersion to look up</param>
        /// <returns>The path to the build tools folder for that ToolsVersion, if it exists, or 
        /// null otherwise</returns>
        internal static string GeneratePathToBuildToolsForToolsVersion(string toolsVersion, DotNetFrameworkArchitecture architecture)
        {
            // Much like when reading toolsets, first check the .exe.config
            string toolsPath = GetPathToBuildToolsFromConfig(toolsVersion);

            if (String.IsNullOrEmpty(toolsPath))
            {
                // Or if it's not defined there, look it up in the registry
                toolsPath = GetPathToBuildToolsFromRegistry(toolsVersion, architecture);
            }

            return toolsPath;
        }

        /// <summary>
        /// Take the parts of the Target framework moniker and formulate the reference assembly path based on the the following pattern:
        /// For a framework and version:
        ///     $(TargetFrameworkRootPath)\$(TargetFrameworkIdentifier)\$(TargetFrameworkVersion)
        /// For a subtype:
        ///     $(TargetFrameworkRootPath)\$(TargetFrameworkIdentifier)\$(TargetFrameworkVersion)\SubType\$(TargetFrameworkSubType)
        /// e.g.NET Framework v4.0 would locate its reference assemblies in:
        ///     \Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0
        /// e.g.Silverlight v2.0 would locate its reference assemblies in:
        ///     \Program Files\Reference Assemblies\Microsoft\Framework\Silverlight\v2.0
        /// e.g.NET Compact Framework v3.5, subtype PocketPC would locate its reference assemblies in:
        ///     \Program Files\Reference Assemblies\Microsoft\Framework\.NETCompactFramework\v3.5\SubType\PocketPC
        /// </summary>
        /// <returns>The path to the reference assembly location</returns>
        internal static string GenerateReferenceAssemblyPath(string targetFrameworkRootPath, FrameworkName frameworkName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkRootPath, "targetFrameworkRootPath");
            ErrorUtilities.VerifyThrowArgumentNull(frameworkName, "frameworkName");

            try
            {
                string path = targetFrameworkRootPath;
                path = Path.Combine(path, frameworkName.Identifier);
                path = Path.Combine(path, "v" + frameworkName.Version.ToString());
                if (!String.IsNullOrEmpty(frameworkName.Profile))
                {
                    path = Path.Combine(path, "Profile");
                    path = Path.Combine(path, frameworkName.Profile);
                }

                path = Path.GetFullPath(path);
                return path;
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                {
                    throw;
                }

                ErrorUtilities.ThrowInvalidOperation("FrameworkLocationHelper.CouldNotGenerateReferenceAssemblyDirectory", targetFrameworkRootPath, frameworkName.ToString(), e.Message);
                // The compiler does not see the massage above an as exception;
                return null;
            }
        }

        /// <summary>
        /// Given a path, subtracts the requested number of directories and returns the result.
        /// </summary>
        /// <comments>
        /// Internal only so that I can have the unit tests use it too, instead of duplicating the same code
        /// </comments>
        internal static string RemoveDirectories(string path, int numberOfLevelsToRemove)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(numberOfLevelsToRemove > 0, "what are you doing passing a negative number to this function??");

            string fixedPath = null;
            if (path != null)
            {
                bool endedWithASlash = false;

                // Record whether we had a slash or not so that we can tack it back on if necessary
                if (path.EndsWith("\\", StringComparison.OrdinalIgnoreCase))
                {
                    endedWithASlash = true;
                }

                DirectoryInfo fixedPathInfo = new DirectoryInfo(path);
                for (int i = 0; i < numberOfLevelsToRemove; i++)
                {
                    if (fixedPathInfo != null && fixedPathInfo.Parent != null)
                    {
                        fixedPathInfo = fixedPathInfo.Parent;
                    }
                }

                if (fixedPathInfo != null)
                {
                    fixedPath = fixedPathInfo.FullName;
                }

                if (fixedPath != null && endedWithASlash)
                {
                    fixedPath = fixedPath + "\\";
                }
            }

            return fixedPath;
        }

        /// <summary>
        /// Look up the path to the build tools directory for the requested ToolsVersion in the .exe.config file of this executable 
        /// </summary>
        private static string GetPathToBuildToolsFromConfig(string toolsVersion)
        {
            string toolPath = null;

            if (ToolsetConfigurationReaderHelpers.ConfigurationFileMayHaveToolsets())
            {
                try
                {
                    Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    ToolsetConfigurationSection configurationSection = ToolsetConfigurationReaderHelpers.ReadToolsetConfigurationSection(configuration);

                    if (configurationSection != null)
                    {
                        ToolsetElement toolset = configurationSection.Toolsets.GetElement(toolsVersion);

                        if (toolset != null)
                        {
                            PropertyElement toolsPathFromConfiguration = toolset.PropertyElements.GetElement(MSBuildConstants.ToolsPath);

                            if (toolsPathFromConfiguration != null)
                            {
                                toolPath = toolsPathFromConfiguration.Value;

                                if (toolPath != null)
                                {
                                    if (!FileUtilities.IsRootedNoThrow(toolPath))
                                    {
                                        toolPath = FileUtilities.NormalizePath(Path.Combine(FileUtilities.CurrentExecutableDirectory, toolPath));
                                    }

                                    toolPath = FileUtilities.EnsureTrailingSlash(toolPath);
                                }
                            }
                        }
                    }
                }
                catch (ConfigurationException)
                {
                    // may happen if the .exe.config contains bad data.  Shouldn't ever happen in 
                    // practice since we'll long since have loaded all toolsets in the toolset loading 
                    // code and thrown errors to the user at that point if anything was invalid, but just 
                    // in case, just eat the exception here, so that we can go on to look in the registry
                    // to see if there is any valid data there.  
                }
            }

            return toolPath;
        }

        /// <summary>
        /// Look up the path to the build tools directory in the registry for the requested ToolsVersion and requested architecture  
        /// </summary>
        private static string GetPathToBuildToolsFromRegistry(string toolsVersion, DotNetFrameworkArchitecture architecture)
        {
            string toolsVersionSpecificKey = ToolsVersionsRegistryPath + "\\" + toolsVersion;

            RegistryView view = RegistryView.Default;

            switch (architecture)
            {
                case DotNetFrameworkArchitecture.Bitness32:
                    view = RegistryView.Registry32;
                    break;
                case DotNetFrameworkArchitecture.Bitness64:
                    view = RegistryView.Registry64;
                    break;
                case DotNetFrameworkArchitecture.Current:
                    view = RegistryView.Default;
                    break;
            }

            string toolsPath = FindRegistryValueUnderKey(toolsVersionSpecificKey, MSBuildConstants.ToolsPath, view);
            return toolsPath;
        }

        #endregion // Internal methods

        #region Private methods

        /// <summary>
        /// Will return the path to the dot net framework reference assemblies if they exist under the program files\reference assembies\microsoft\framework directory
        /// or null if the directory does not exist.
        /// </summary>
        private static string GenerateReferenceAssemblyDirectory(string versionPrefix)
        {
            string programFilesReferenceAssemblyDirectory = Path.Combine(programFilesReferenceAssemblyLocation, versionPrefix);
            string referenceAssemblyDirectory = null;

            if (Directory.Exists(programFilesReferenceAssemblyDirectory))
            {
                referenceAssemblyDirectory = programFilesReferenceAssemblyDirectory;
            }

            return referenceAssemblyDirectory;
        }

        /// <summary>
        /// Look for the given registry value under the given key.
        /// </summary>
        private static string FindRegistryValueUnderKey
        (
            string registryBaseKeyName,
            string registryKeyName
        )
        {
            return FindRegistryValueUnderKey(registryBaseKeyName, registryKeyName, RegistryView.Default);
        }

        /// <summary>
        /// Look for the given registry value under the given key.
        /// </summary>
        private static string FindRegistryValueUnderKey
        (
            string registryBaseKeyName,
            string registryKeyName,
            RegistryView registryView
        )
        {
            string keyValueAsString = String.Empty;

            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
            {
                using (RegistryKey subKey = baseKey.OpenSubKey(registryBaseKeyName))
                {
                    if (null == subKey)
                    {
                        keyValueAsString = null;
                    }
                    else
                    {
                        object keyValue = subKey.GetValue(registryKeyName);

                        if (null == keyValue)
                        {
                            keyValueAsString = null;
                        }
                        else
                        {
                            keyValueAsString = keyValue.ToString();
                        }
                    }
                }
            }

            return keyValueAsString;
        }

        private static VisualStudioSpec GetVisualStudioSpec(Version version)
        {
            ErrorUtilities.VerifyThrowArgument(s_visualStudioSpecDict.ContainsKey(version), "FrameworkLocationHelper.UnsupportedVisualStudioVersion", version);
            return s_visualStudioSpecDict[version];
        }

        private static DotNetFrameworkSpec GetDotNetFrameworkSpec(Version version)
        {
            ErrorUtilities.VerifyThrowArgument(s_dotNetFrameworkSpecDict.ContainsKey(version), "FrameworkLocationHelper.UnsupportedFrameworkVersion", version);
            return s_dotNetFrameworkSpecDict[version];
        }

        /// <summary>
        /// Helper method to create an instance of <see cref="DotNetFrameworkSpec"/> for .net v4.x,
        /// because most of attributes are the same for v4.x versions.
        /// </summary>
        /// <param name="version">.net framework version.</param>
        /// <returns></returns>
        private static DotNetFrameworkSpec CreateDotNetFrameworkSpecForV4(Version version, Version visualStudioVersion)
        {
            return new DotNetFrameworkSpec(
                version,
                dotNetFrameworkRegistryKey: dotNetFrameworkSetupRegistryPath + "\\v4\\Full",
                dotNetFrameworkSetupRegistryInstalledName: "Install",
                dotNetFrameworkVersionFolderPrefix: "v4.0",
                dotNetFrameworkSdkRegistryToolsKey: "WinSDK-NetFx40Tools-x86",
                dotNetFrameworkSdkRegistryInstallationFolderName: "InstallationFolder",
                hasMSBuild: true,
                visualStudioVersion: visualStudioVersion);
        }

        private static void RedirectVersionsIfNecessary(ref Version dotNetFrameworkVersion, ref Version visualStudioVersion)
        {
            if (dotNetFrameworkVersion == dotNetFrameworkVersion45 && visualStudioVersion == visualStudioVersion100)
            {
                // There is no VS10 equivalent -- so just return the VS11 version
                visualStudioVersion = visualStudioVersion110;
                return;
            }

            if (dotNetFrameworkVersion == dotNetFrameworkVersion35 && visualStudioVersion > visualStudioVersion110)
            {
                // Fall back to Dev11 location -- 3.5 tools MSI was reshipped unchanged, so there 
                // essentially are no 12-specific 3.5 tools. 
                visualStudioVersion = visualStudioVersion110;
                return;
            }
        }

        #endregion

        private class VisualStudioSpec
        {
            /// <summary>
            /// The version of this visual studio.
            /// </summary>
            private readonly Version _version;

            /// <summary>
            /// The key in registry to indicate the corresponding .net framework in this visual studio.
            /// i.e. 'v8.0A' for VS11.
            /// </summary>
            private readonly string _dotNetFrameworkSdkRegistryKey;

            /// <summary>
            /// The key in registry to indicate the corresponding windows sdk, i.e. "v8.0" for VS11.
            /// </summary>
            private readonly string _windowsSdkRegistryKey;

            /// <summary>
            /// The name in registry to indicate the sdk installation folder path, i.e. "InstallationFolder" for windows v8.0.
            /// </summary>
            private readonly string _windowsSdkRegistryInstallationFolderName;

            /// <summary>
            /// The list of supported .net framework versions in this visual studio.
            /// </summary>
            private readonly Version[] _supportedDotNetFrameworkVersions;

            public VisualStudioSpec(
                Version version,
                string dotNetFrameworkSdkRegistryKey,
                string windowsSdkRegistryKey,
                string windowsSdkRegistryInstallationFolderName,
                Version[] supportedDotNetFrameworkVersions)
            {
                _version = version;
                _dotNetFrameworkSdkRegistryKey = dotNetFrameworkSdkRegistryKey;
                _windowsSdkRegistryKey = windowsSdkRegistryKey;
                _windowsSdkRegistryInstallationFolderName = windowsSdkRegistryInstallationFolderName;
                _supportedDotNetFrameworkVersions = supportedDotNetFrameworkVersions;
            }

            /// <summary>
            /// The version of this visual studio.
            /// </summary>
            public Version Version
            {
                get { return _version; }
            }

            /// <summary>
            /// The key in registry to indicate the corresponding .net framework in this visual studio.
            /// i.e. 'v8.0A' for VS11.
            /// </summary>
            public string DotNetFrameworkSdkRegistryKey
            {
                get { return _dotNetFrameworkSdkRegistryKey; }
            }

            /// <summary>
            /// The list of supported .net framework versions in this visual studio.
            /// </summary>
            public Version[] SupportedDotNetFrameworkVersions
            {
                get { return _supportedDotNetFrameworkVersions; }
            }

            /// <summary>
            /// The key in registry to indicate the corresponding windows sdk, i.e. "v8.0" for VS11.
            /// </summary>
            public string WindowsSdkRegistryKey
            {
                get { return _windowsSdkRegistryKey; }
            }

            /// <summary>
            /// The name in registry to indicate the sdk installation folder path, i.e. "InstallationFolder" for windows v8.0.
            /// </summary>
            public string WindowsSdkRegistryInstallationFolderName
            {
                get { return _windowsSdkRegistryInstallationFolderName; }
            }
        }

        private class DotNetFrameworkSpec
        {
            private const string HKLM = "HKEY_LOCAL_MACHINE";
            private const string MicrosoftSDKsRegistryKey = @"SOFTWARE\Microsoft\Microsoft SDKs";

            /// <summary>
            /// The version of this .net framework.
            /// </summary>
            protected readonly Version version;

            /// <summary>
            /// The registry key of this .net framework, i.e. "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" for .net v4.5.
            /// </summary>
            protected readonly string dotNetFrameworkRegistryKey;

            /// <summary>
            /// The name in registry to indicate that this .net framework is installed, i.e. "Install" for .net v4.5.
            /// </summary>
            protected readonly string dotNetFrameworkSetupRegistryInstalledName;

            /// <summary>
            /// Folder prefix, i.e. v4.0 for .net v4.5.
            /// </summary>
            protected readonly string dotNetFrameworkFolderPrefix;

            /// <summary>
            /// The key in registry to indicate the sdk tools folder, i.e. "WinSDK-NetFx40Tools-x86" for .net v4.5.
            /// </summary>
            protected readonly string dotNetFrameworkSdkRegistryToolsKey;

            /// <summary>
            /// The name in registry to indicate the sdk installation folder path, i.e. "InstallationFolder" for .net v4.5.
            /// </summary>
            protected readonly string dotNetFrameworkSdkRegistryInstallationFolderName;

            /// <summary>
            /// The version of visual studio that shipped with this .net framework.
            /// </summary>
            protected readonly Version visualStudioVersion;

            /// <summary>
            /// Does this .net framework include MSBuild?
            /// </summary>
            protected readonly bool hasMSBuild;

            /// <summary>
            /// Cached paths of .net framework on different architecture.
            /// </summary>
            protected readonly ConcurrentDictionary<DotNetFrameworkArchitecture, string> pathsToDotNetFramework;

            /// <summary>
            /// Cached paths of .net framework sdk tools folder path on different visual studio version.
            /// </summary>
            protected readonly ConcurrentDictionary<Version, string> pathsToDotNetFrameworkSdkTools;

            /// <summary>
            /// Cached path of the corresponding windows sdk.
            /// </summary>
            protected string pathToWindowsSdk;

            /// <summary>
            /// Cached path of .net framework reference assemblies.
            /// </summary>
            protected string pathToDotNetFrameworkReferenceAssemblies;

            public DotNetFrameworkSpec(
                Version version,
                string dotNetFrameworkRegistryKey,
                string dotNetFrameworkSetupRegistryInstalledName,
                string dotNetFrameworkVersionFolderPrefix,
                string dotNetFrameworkSdkRegistryToolsKey,
                string dotNetFrameworkSdkRegistryInstallationFolderName,
                bool hasMSBuild = true,
                Version visualStudioVersion = null)
            {
                this.version = version;
                this.visualStudioVersion = visualStudioVersion;
                this.dotNetFrameworkRegistryKey = dotNetFrameworkRegistryKey;
                this.dotNetFrameworkSetupRegistryInstalledName = dotNetFrameworkSetupRegistryInstalledName;
                this.dotNetFrameworkFolderPrefix = dotNetFrameworkVersionFolderPrefix;
                this.dotNetFrameworkSdkRegistryToolsKey = dotNetFrameworkSdkRegistryToolsKey;
                this.dotNetFrameworkSdkRegistryInstallationFolderName = dotNetFrameworkSdkRegistryInstallationFolderName;
                this.hasMSBuild = hasMSBuild;
                this.pathsToDotNetFramework = new ConcurrentDictionary<DotNetFrameworkArchitecture, string>();
                this.pathsToDotNetFrameworkSdkTools = new ConcurrentDictionary<Version, string>();
            }

            /// <summary>
            /// The version of this .net framework.
            /// </summary>
            public Version Version
            {
                get { return this.version; }
            }

            /// <summary>
            /// The name in registry to indicate the sdk installation folder path, i.e. "InstallationFolder" for .net v4.5.
            /// </summary>
            public string DotNetFrameworkSdkRegistryInstallationFolderName
            {
                get { return this.dotNetFrameworkSdkRegistryInstallationFolderName; }
            }

            /// <summary>
            /// Folder prefix, i.e. v4.0 for .net v4.5.
            /// </summary>
            public string DotNetFrameworkFolderPrefix
            {
                get { return this.dotNetFrameworkFolderPrefix; }
            }

            private FrameworkName FrameworkName
            {
                get { return new FrameworkName(dotNetFrameworkIdentifier, this.version); }
            }

            /// <summary>
            /// Gets the full registry key of this .net framework Sdk for the given visual studio version.
            /// i.e. "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx40Tools-x86" for .net v4.5 on VS11.
            /// </summary>
            public virtual string GetDotNetFrameworkSdkRootRegistryKey(VisualStudioSpec visualStudioSpec)
            {
                return string.Join(@"\", HKLM, MicrosoftSDKsRegistryKey, visualStudioSpec.DotNetFrameworkSdkRegistryKey, this.dotNetFrameworkSdkRegistryToolsKey);
            }

            /// <summary>
            /// Gets the full path of .net framework for the given architecture.
            /// </summary>
            public virtual string GetPathToDotNetFramework(DotNetFrameworkArchitecture architecture)
            {
                string cachedPath;
                if (this.pathsToDotNetFramework.TryGetValue(architecture, out cachedPath))
                {
                    return cachedPath;
                }

                // Otherwise, check to see if we're even installed.  If not, return null -- no point in setting the static 
                // variables to null when that's what they are already.  
                if (!CheckForFrameworkInstallation(this.dotNetFrameworkRegistryKey, this.dotNetFrameworkSetupRegistryInstalledName))
                {
                    return null;
                }

                // We're installed and we haven't found this framework path yet -- so find it!
                string generatedPathToDotNetFramework =
                                FindDotNetFrameworkPath(
                                    Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName),
                                    this.dotNetFrameworkFolderPrefix,
                                    s_directoryExists,
                                    s_getDirectories,
                                    architecture
                                    );

                if (this.hasMSBuild &&
                    generatedPathToDotNetFramework != null &&
                    !File.Exists(Path.Combine(generatedPathToDotNetFramework, "msbuild.exe"))) // .net was improperly uninstalled: msbuild.exe isn't there
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(generatedPathToDotNetFramework))
                {
                    pathsToDotNetFramework[architecture] = generatedPathToDotNetFramework;
                }

                return generatedPathToDotNetFramework;
            }

            /// <summary>
            /// Gets the full path of .net framework sdk tools for the given visual studio version.
            /// i.e. "C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools\" for .net v4.5 on VS11.
            /// </summary>
            public virtual string GetPathToDotNetFrameworkSdkTools(VisualStudioSpec visualStudioSpec)
            {
                string cachedPath;
                if (this.pathsToDotNetFrameworkSdkTools.TryGetValue(visualStudioSpec.Version, out cachedPath))
                {
                    return cachedPath;
                }

                string registryPath = string.Join(@"\", MicrosoftSDKsRegistryKey, visualStudioSpec.DotNetFrameworkSdkRegistryKey, this.dotNetFrameworkSdkRegistryToolsKey);

                // For the Dev10 SDK, we check the registry that corresponds to the current process' bitness, rather than
                // always the 32-bit one the way we do for Dev11 and onward, since that's what we did in Dev10 as well.
                // As of Dev11, the SDK reg keys are installed in the 32-bit registry. 
                RegistryView registryView = visualStudioSpec.Version == visualStudioVersion100 ? RegistryView.Default : RegistryView.Registry32;

                string generatedPathToDotNetFrameworkSdkTools = FindRegistryValueUnderKey(
                    registryPath,
                    this.dotNetFrameworkSdkRegistryInstallationFolderName,
                    registryView);

                if (string.IsNullOrEmpty(generatedPathToDotNetFrameworkSdkTools))
                {
                    // Fallback mechanisms.

                    // Try to find explicit fallback rule.
                    // i.e. v4.5.1 on VS12 fallbacks to v4.5 on VS12.
                    bool foundExplicitRule = false;
                    for (int i = 0; i < s_explicitFallbackRulesForPathToDotNetFrameworkSdkTools.GetLength(0); ++i)
                    {
                        var trigger = s_explicitFallbackRulesForPathToDotNetFrameworkSdkTools[i, 0];
                        if (trigger.Item1 == this.version && trigger.Item2 == visualStudioSpec.Version)
                        {
                            foundExplicitRule = true;
                            var fallback = s_explicitFallbackRulesForPathToDotNetFrameworkSdkTools[i, 1];
                            generatedPathToDotNetFrameworkSdkTools = FallbackToPathToDotNetFrameworkSdkToolsInPreviousVersion(fallback.Item1, fallback.Item2);
                            break;
                        }
                    }

                    // Otherwise, fallback to previous VS.
                    // i.e. fallback to v110 if the current visual studio version is v120.
                    if (!foundExplicitRule)
                    {
                        int index = Array.IndexOf(s_visualStudioSpecs, visualStudioSpec);
                        if (index > 0)
                        {
                            // The items in the array "visualStudioSpecs" must be ordered by version. That would allow us to fallback to the previous visual studio version easily.
                            VisualStudioSpec fallbackVisualStudioSpec = s_visualStudioSpecs[index - 1];
                            generatedPathToDotNetFrameworkSdkTools = FallbackToPathToDotNetFrameworkSdkToolsInPreviousVersion(this.version, fallbackVisualStudioSpec.Version);
                        }
                    }
                }

                if (string.IsNullOrEmpty(generatedPathToDotNetFrameworkSdkTools))
                {
                    // Fallback to "default" ultimately.
                    generatedPathToDotNetFrameworkSdkTools = FallbackToDefaultPathToDotNetFrameworkSdkTools(this.version);
                }

                if (!string.IsNullOrEmpty(generatedPathToDotNetFrameworkSdkTools))
                {
                    this.pathsToDotNetFrameworkSdkTools[visualStudioSpec.Version] = generatedPathToDotNetFrameworkSdkTools;
                }

                return generatedPathToDotNetFrameworkSdkTools;
            }

            /// <summary>
            /// Gets the full path of .net framework sdk.
            /// i.e. "C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\" for .net v4.5 on VS11.
            /// </summary>
            public virtual string GetPathToDotNetFrameworkSdk(VisualStudioSpec visualStudioSpec)
            {
                string pathToBinRoot = this.GetPathToDotNetFrameworkSdkTools(visualStudioSpec);
                pathToBinRoot = RemoveDirectories(pathToBinRoot, 2);
                return pathToBinRoot;
            }

            /// <summary>
            /// Gets the full path of reference assemblies folder.
            /// i.e. "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\" for .net v4.5.
            /// </summary>
            public virtual string GetPathToDotNetFrameworkReferenceAssemblies()
            {
                if (this.pathToDotNetFrameworkReferenceAssemblies == null)
                {
                    // when a user requests the 40 reference assembly path we don't need to read the redist list because we will not be chaining so we may as well just
                    // generate the path and save us some time.
                    string referencePath = GenerateReferenceAssemblyPath(FrameworkLocationHelper.programFilesReferenceAssemblyLocation, this.FrameworkName);
                    if (Directory.Exists(referencePath))
                    {
                        this.pathToDotNetFrameworkReferenceAssemblies = FileUtilities.EnsureTrailingSlash(referencePath);
                    }
                }

                return this.pathToDotNetFrameworkReferenceAssemblies;
            }

            /// <summary>
            /// Gets the full path of the corresponding windows sdk shipped with this .net framework.
            /// i.e. "C:\Program Files (x86)\Windows Kits\8.0\" for v8.0 (shipped with .net v4.5 and VS11).
            /// </summary>
            public virtual string GetPathToWindowsSdk()
            {
                if (this.pathToWindowsSdk == null)
                {
                    ErrorUtilities.VerifyThrowArgument(this.visualStudioVersion != null, "FrameworkLocationHelper.UnsupportedFrameworkVersionForWindowsSdk", this.version);

                    var visualStudioSpec = GetVisualStudioSpec(this.visualStudioVersion);

                    if (string.IsNullOrEmpty(visualStudioSpec.WindowsSdkRegistryKey) || string.IsNullOrEmpty(visualStudioSpec.WindowsSdkRegistryInstallationFolderName))
                    {
                        ErrorUtilities.ThrowArgument("FrameworkLocationHelper.UnsupportedFrameworkVersionForWindowsSdk", this.version);
                    }

                    string registryPath = string.Join(@"\", MicrosoftSDKsRegistryKey, "Windows", visualStudioSpec.WindowsSdkRegistryKey);

                    // As of Dev11, the SDK reg keys are installed in the 32-bit registry. 
                    this.pathToWindowsSdk = FindRegistryValueUnderKey(
                        registryPath,
                        visualStudioSpec.WindowsSdkRegistryInstallationFolderName,
                        RegistryView.Registry32);
                }

                return this.pathToWindowsSdk;
            }

            protected static string FallbackToPathToDotNetFrameworkSdkToolsInPreviousVersion(Version dotNetFrameworkVersion, Version visualStudioVersion)
            {
                VisualStudioSpec visualStudioSpec;
                DotNetFrameworkSpec dotNetFrameworkSpec;
                if (s_visualStudioSpecDict.TryGetValue(visualStudioVersion, out visualStudioSpec)
                    && s_dotNetFrameworkSpecDict.TryGetValue(dotNetFrameworkVersion, out dotNetFrameworkSpec)
                    && visualStudioSpec.SupportedDotNetFrameworkVersions.Contains(dotNetFrameworkVersion))
                {
                    return dotNetFrameworkSpec.GetPathToDotNetFrameworkSdkTools(visualStudioSpec);
                }

                return null;
            }

            protected static string FallbackToDefaultPathToDotNetFrameworkSdkTools(Version dotNetFrameworkVersion)
            {
                if (dotNetFrameworkVersion.Major == 4)
                {
                    return FrameworkLocationHelper.PathToV4ToolsInFallbackDotNetFrameworkSdk;
                }

                if (dotNetFrameworkVersion == dotNetFrameworkVersion35)
                {
                    return FrameworkLocationHelper.PathToV35ToolsInFallbackDotNetFrameworkSdk;
                }

                return null;
            }
        }

        /// <summary>
        /// Specialized implementation for legacy .net framework v1.1 and v2.0.
        /// </summary>
        private class DotNetFrameworkSpecLegacy : DotNetFrameworkSpec
        {
            private string _pathToDotNetFrameworkSdkTools;

            public DotNetFrameworkSpecLegacy(
                Version version,
                string dotNetFrameworkRegistryKey,
                string dotNetFrameworkSetupRegistryInstalledName,
                string dotNetFrameworkVersionFolderPrefix,
                string dotNetFrameworkSdkRegistryInstallationFolderName,
                bool hasMSBuild)
                : base(version,
                      dotNetFrameworkRegistryKey,
                      dotNetFrameworkSetupRegistryInstalledName,
                      dotNetFrameworkVersionFolderPrefix,
                      null,
                      dotNetFrameworkSdkRegistryInstallationFolderName,
                      hasMSBuild)
            {
            }

            /// <summary>
            /// Gets the full registry key of this .net framework Sdk for the given visual studio version.
            /// i.e. "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework" for v1.1 and v2.0.
            /// </summary>
            public override string GetDotNetFrameworkSdkRootRegistryKey(VisualStudioSpec visualStudioSpec)
            {
                return FrameworkLocationHelper.fullDotNetFrameworkRegistryKey;
            }

            /// <summary>
            /// Gets the full path of .net framework sdk tools for the given visual studio version.
            /// </summary>
            public override string GetPathToDotNetFrameworkSdkTools(VisualStudioSpec visualStudioSpec)
            {
                if (_pathToDotNetFrameworkSdkTools == null)
                {
                    _pathToDotNetFrameworkSdkTools = FindRegistryValueUnderKey(
                        dotNetFrameworkRegistryPath,
                        this.dotNetFrameworkSdkRegistryInstallationFolderName);
                }

                return _pathToDotNetFrameworkSdkTools;
            }

            /// <summary>
            /// Gets the full path of .net framework sdk, which is the full path of .net framework sdk tools for v1.1 and v2.0.
            /// </summary>
            public override string GetPathToDotNetFrameworkSdk(VisualStudioSpec visualStudioSpec)
            {
                return this.GetPathToDotNetFrameworkSdkTools(visualStudioSpec);
            }

            /// <summary>
            /// Gets the full path of reference assemblies folder, which is the full path of .net framework for v1.1 and v2.0.
            /// </summary>
            public override string GetPathToDotNetFrameworkReferenceAssemblies()
            {
                return this.GetPathToDotNetFramework(DotNetFrameworkArchitecture.Current);
            }
        }

        /// <summary>
        /// Specialized implementation for legacy .net framework v3.0 and v3.5.
        /// </summary>
        private class DotNetFrameworkSpecV3 : DotNetFrameworkSpec
        {
            public DotNetFrameworkSpecV3(
                Version version,
                string dotNetFrameworkRegistryKey,
                string dotNetFrameworkSetupRegistryInstalledName,
                string dotNetFrameworkVersionFolderPrefix,
                string dotNetFrameworkSdkRegistryToolsKey,
                string dotNetFrameworkSdkRegistryInstallationFolderName,
                bool hasMSBuild)
                : base(version,
                      dotNetFrameworkRegistryKey,
                      dotNetFrameworkSetupRegistryInstalledName,
                      dotNetFrameworkVersionFolderPrefix,
                      dotNetFrameworkSdkRegistryToolsKey,
                      dotNetFrameworkSdkRegistryInstallationFolderName,
                      hasMSBuild)
            {
            }

            /// <summary>
            /// Gets the full path of .net framework sdk.
            /// i.e. "C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\" for .net v3.5 on VS11.
            /// </summary>
            public override string GetPathToDotNetFrameworkSdk(VisualStudioSpec visualStudioSpec)
            {
                string pathToBinRoot = this.GetPathToDotNetFrameworkSdkTools(visualStudioSpec);
                pathToBinRoot = RemoveDirectories(pathToBinRoot, 1);
                return pathToBinRoot;
            }

            /// <summary>
            /// Gets the full path of reference assemblies folder.
            /// i.e. "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\v3.5\" for v3.5.
            /// </summary>
            public override string GetPathToDotNetFrameworkReferenceAssemblies()
            {
                if (this.pathToDotNetFrameworkReferenceAssemblies == null)
                {
                    this.pathToDotNetFrameworkReferenceAssemblies = FindRegistryValueUnderKey(
                        dotNetFrameworkAssemblyFoldersRegistryPath + "\\" + this.dotNetFrameworkFolderPrefix,
                        referenceAssembliesRegistryValueName);

                    if (this.pathToDotNetFrameworkReferenceAssemblies == null)
                    {
                        this.pathToDotNetFrameworkReferenceAssemblies = GenerateReferenceAssemblyDirectory(this.dotNetFrameworkFolderPrefix);
                    }
                }

                return this.pathToDotNetFrameworkReferenceAssemblies;
            }
        }
    }
}
