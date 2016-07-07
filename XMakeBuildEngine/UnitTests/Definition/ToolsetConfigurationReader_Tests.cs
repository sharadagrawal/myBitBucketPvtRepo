﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;

using ToolsetConfigurationSection = Microsoft.Build.Evaluation.ToolsetConfigurationSection;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Unit tests for ToolsetConfigurationReader class
    /// </summary>
    [TestClass]
    public class ToolsetConfigurationReaderTests
    {
        private static string s_msbuildToolsets = "msbuildToolsets";

        [TestInitialize]
        public void Setup()
        {
        }

        [TestCleanup]
        public void Teardown()
        {
            ToolsetConfigurationReaderTestHelper.CleanUp();
        }

        #region "msbuildToolsets element tests"

        /// <summary>
        ///  msbuildToolsets element is empty
        /// </summary>
        [TestMethod]
        public void MSBuildToolsetsTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets />
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.MSBuildOverrideTasksPath, null);
            Assert.IsNotNull(msbuildToolsetSection);
            Assert.AreEqual(null, msbuildToolsetSection.Default);
            Assert.IsNotNull(msbuildToolsetSection.Toolsets);
            Assert.AreEqual(0, msbuildToolsetSection.Toolsets.Count);
        }

        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [TestMethod]
        public void MSBuildToolsetsTest_Basic()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.MSBuildOverrideTasksPath, null);
            Assert.AreEqual(msbuildToolsetSection.Default, "2.0");
            Assert.AreEqual(1, msbuildToolsetSection.Toolsets.Count);

            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "2.0");
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count, 1);
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
        }

        /// <summary>
        ///  Tests if ToolsetConfigurationReaderTests is successfully initialized from the config file when msbuildOVerrideTasksPath is set.
        ///  Also verify the msbuildOverrideTasksPath is properly read in.
        /// </summary>
        [TestMethod]
        public void MSBuildToolsetsTest_Basic2()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath=""c:\foo"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.MSBuildOverrideTasksPath, "c:\\foo");
        }

        /// <summary>
        ///  Tests if ToolsetConfigurationReaderTests is successfully initialized from the config file and that msbuildOVerrideTasksPath 
        ///  is correctly read in when the value is empty.
        /// </summary>
        [TestMethod]
        public void MSBuildToolsetsTest_Basic3()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath="""">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.MSBuildOverrideTasksPath, null);
        }

        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [TestMethod]
        public void MSBuildToolsetsTest_BasicWithOtherConfigEntries()
        {
            // NOTE: for some reason, <configSections> MUST be the first element under <configuration>
            // for the API to read it. The docs don't make this clear.

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                    <startup>
                     <supportedRuntime imageVersion=""v2.0.60510"" version=""v2.0.x86chk""/>
                     <requiredRuntime imageVersion=""v2.0.60510"" version=""v2.0.x86chk"" safemode=""true""/>
                   </startup>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                   <runtime>
                     <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
                       <dependentAssembly>
                          <assemblyIdentity name=""Microsoft.Build.Framework"" publicKeyToken=""b03f5f7f11d50a3a"" culture=""neutral""/>
                          <bindingRedirect oldVersion=""0.0.0.0-99.9.9.9"" newVersion=""2.0.0.0""/>
                       </dependentAssembly>
                     </assemblyBinding>
                   </runtime>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.Default, "2.0");
            Assert.AreEqual(1, msbuildToolsetSection.Toolsets.Count);

            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "2.0");
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count, 1);
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
        }
        #endregion

        #region "toolsVersion element tests"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        /// name attribute is missing from toolset element 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_NameNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        ///  More than 1 toolset element with the same name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_MultipleElementsWithSameName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// empty toolset element 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset />
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        #endregion

        #region "Valid cases (No exception expected)"

        /// <summary>
        /// only 1 toolset is specified
        /// </summary>
        [TestMethod]
        public void ToolsVersionTest_SingleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.Default, "4.0");
            Assert.AreEqual(1, msbuildToolsetSection.Toolsets.Count);
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "4.0");
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("4.0").PropertyElements.Count, 1);
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("4.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v3.5.x86ret\");
        }
        #endregion
        #endregion

        #region "Property"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        ///  name attribute is missing
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_NameNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// value attribute is missing
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_ValueNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset name=""4.0"">
                       <property name=""MSBuildBinPath"" />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// more than 1 property element with the same name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_MultipleElementsWithSameName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset ToolsVersion=""msbuilddefaulttoolsversion"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        ///  property element is an empty element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;
        }
        #endregion

        #region "Valid cases"

        /// <summary>
        /// more than 1 property element specified
        /// </summary>
        [TestMethod]
        public void PropertyTest_MultipleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.AreEqual(msbuildToolsetSection.Default, "2.0");
            Assert.AreEqual(1, msbuildToolsetSection.Toolsets.Count);
            Assert.AreEqual(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);

            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
            Assert.AreEqual(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value,
                                   @"SomeOtherPropertyValue");
        }

        /// <summary>
        /// tests GetElement(string name) function in propertycollection class
        /// </summary>
        [TestMethod]
        public void PropertyTest_GetValueByName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            // Verifications
            Assert.AreEqual(msbuildToolsetSection.Default, "2.0");
            Assert.AreEqual(1, msbuildToolsetSection.Toolsets.Count);
            Assert.AreEqual(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);
            Assert.AreEqual(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);
            Assert.AreEqual(@"SomeOtherPropertyValue",
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value);
        }

        #endregion
        #endregion
    }
}
