﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using BuildUtilities = Microsoft.Build.Utilities;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ProcessorArchitectureTests
    {
        internal static string ProcessorArchitectureIntToString(NativeMethodsShared.SYSTEM_INFO systemInfo)
        {
            switch (systemInfo.wProcessorArchitecture)
            {
                case NativeMethodsShared.PROCESSOR_ARCHITECTURE_INTEL:
                    return BuildUtilities.ProcessorArchitecture.X86;

                case NativeMethodsShared.PROCESSOR_ARCHITECTURE_AMD64:
                    return BuildUtilities.ProcessorArchitecture.AMD64;

                case NativeMethodsShared.PROCESSOR_ARCHITECTURE_IA64:
                    return BuildUtilities.ProcessorArchitecture.IA64;

                case NativeMethodsShared.PROCESSOR_ARCHITECTURE_ARM:
                    return BuildUtilities.ProcessorArchitecture.ARM;

                // unknown architecture? return null
                default:
                    return null;
            }
        }

        [TestMethod]
        public void ValidateProcessorArchitectureStrings()
        {
            // Make sure changes to BuildUtilities.ProcessorArchitecture.cs source don't accidentally get mangle ProcessorArchitecture
            Assert.AreEqual("x86", BuildUtilities.ProcessorArchitecture.X86, "x86 ProcessorArchitecture isn't correct");
            Assert.AreEqual("IA64", BuildUtilities.ProcessorArchitecture.IA64, "IA64 ProcessorArchitecture isn't correct");
            Assert.AreEqual("AMD64", BuildUtilities.ProcessorArchitecture.AMD64, "AMD64 ProcessorArchitecture isn't correct");
            Assert.AreEqual("MSIL", BuildUtilities.ProcessorArchitecture.MSIL, "MSIL ProcessorArchitecture isn't correct");
            Assert.AreEqual("ARM", BuildUtilities.ProcessorArchitecture.ARM, "ARM ProcessorArchitecture isn't correct");
        }

        [TestMethod]
        public void ValidateCurrentProcessorArchitectureCall()
        {
            NativeMethodsShared.SYSTEM_INFO systemInfo = new NativeMethodsShared.SYSTEM_INFO();
            NativeMethodsShared.GetSystemInfo(ref systemInfo);
            Assert.AreEqual(ProcessorArchitectureIntToString(systemInfo), BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture, "BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture returned an invalid match");
        }

        [TestMethod]
        public void ValidateConvertDotNetFrameworkArchitectureToProcessorArchitecture()
        {
            Console.WriteLine("BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture is: {0}", BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture);
            string procArchitecture;
            switch (BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture)
            {
                case BuildUtilities.ProcessorArchitecture.ARM:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.ARM, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.IsNull(procArchitecture, "We should not have any Bitness64 Processor architecture returned in arm");
                    break;

                case BuildUtilities.ProcessorArchitecture.X86:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);

                    //We should also allow NULL if the machine is true x86 only.
                    bool isValidResult = procArchitecture == null ? true : procArchitecture.Equals(BuildUtilities.ProcessorArchitecture.AMD64) || procArchitecture.Equals(BuildUtilities.ProcessorArchitecture.IA64);

                    Assert.IsTrue(isValidResult);
                    break;

                case BuildUtilities.ProcessorArchitecture.AMD64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.AMD64, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);
                    break;

                case BuildUtilities.ProcessorArchitecture.IA64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.IA64, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.AreEqual(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);
                    break;

                case BuildUtilities.ProcessorArchitecture.MSIL:
                    Assert.Fail("We should never hit ProcessorArchitecture.MSIL");
                    break;

                default:
                    Assert.Fail("Untested or new ProcessorArchitecture type");
                    break;
            }
        }
    }
}
