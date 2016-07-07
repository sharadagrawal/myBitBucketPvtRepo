﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for preprocessor</summary>
//-----------------------------------------------------------------------

using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using System.IO;

namespace Microsoft.Build.UnitTests.Preprocessor
{
    /// <summary>
    /// Tests mainly for project preprocessing
    /// </summary>
    [TestClass]
    public class Preprocessor_Tests
    {
        /// <summary>
        /// Clear out the cache
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Clear out the cache
        /// </summary>
        [TestCleanup]
        public void Teardown()
        {
            Setup();
        }

        /// <summary>
        /// Basic project
        /// </summary>
        [TestMethod]
        public void Single()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// InitialTargets are concatenated, outermost to innermost
        /// </summary>
        [TestMethod]
        public void InitialTargetsOuterAndInner()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.InitialTargets = "i1";
            xml1.AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.InitialTargets = "i2";

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" InitialTargets=""i1;i2"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// InitialTargets are concatenated, outermost to innermost
        /// </summary>
        [TestMethod]
        public void InitialTargetsInnerOnly()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.InitialTargets = "i2";

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" InitialTargets=""i2"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// InitialTargets are concatenated, outermost to innermost
        /// </summary>
        [TestMethod]
        public void InitialTargetsOuterOnly()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.InitialTargets = "i1";
            xml1.AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" InitialTargets=""i1"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic empty project importing another
        /// </summary>
        [TestMethod]
        public void TwoFirstEmpty()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.AddProperty("p", "v2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// False import should not be followed
        /// </summary>
        [TestMethod]
        public void FalseImport()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddProperty("p", "v1");
            xml1.AddImport("p2").Condition = "false";
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.AddProperty("p", "v2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--<Import Project=""p2"" Condition=""false"" />-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic project importing another empty one
        /// </summary>
        [TestMethod]
        public void TwoSecondEmpty()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddProperty("p", "v");
            xml1.AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic project importing another
        /// </summary>
        [TestMethod]
        public void TwoWithContent()
        {
            string one = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v0</p>
  </PropertyGroup>
  <Import Project=""p2""/>
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            string two = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
</Project>");
            ProjectRootElement twoXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(two)));
            twoXml.FullPath = "p2";

            Project project = new Project(XmlTextReader.Create(new StringReader(one)));

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v0</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>


============================================================================================================================================
-->
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic project importing another one via an ImportGroup
        /// </summary>
        [TestMethod]
        public void ImportGroup()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddProperty("p", "v1");
            xml1.AddImportGroup().AddImport("p2");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.AddProperty("p", "v2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--<ImportGroup>-->
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
  <!--</ImportGroup>-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic project importing another one via an ImportGroup with two imports inside it, and a condition on it
        /// </summary>
        [TestMethod]
        public void ImportGroupDoubleChildPlusCondition()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddProperty("p", "v1");
            ProjectImportGroupElement group = xml1.AddImportGroup();
            group.Condition = "true";
            group.AddImport("p2");
            group.AddImport("p3");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.AddProperty("p", "v2");
            ProjectRootElement xml3 = ProjectRootElement.Create("p3");
            xml3.AddProperty("p", "v3");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--<ImportGroup Condition=""true"">-->
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  <Import Project=""p3"">

" + Directory.GetCurrentDirectory() + @"\p3
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v3</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
  <!--</ImportGroup>-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// First DefaultTargets encountered is used
        /// </summary>
        [TestMethod]
        public void DefaultTargetsOuterAndInner()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddImport("p2");
            xml1.AddImport("p3");
            xml1.DefaultTargets = "d1";
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.DefaultTargets = "d2";
            ProjectRootElement xml3 = ProjectRootElement.Create("p3");
            xml3.DefaultTargets = "d3";

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
     @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" DefaultTargets=""d1"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  <Import Project=""p3"">

" + Directory.GetCurrentDirectory() + @"\p3
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// First DefaultTargets encountered is used
        /// </summary>
        [TestMethod]
        public void DefaultTargetsInnerOnly()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddImport("p2");
            xml1.AddImport("p3");
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.DefaultTargets = "d2";
            ProjectRootElement xml3 = ProjectRootElement.Create("p3");
            xml3.DefaultTargets = "d3";

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
     @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" DefaultTargets=""d2"">
  <!--
============================================================================================================================================
  <Import Project=""p2"">

" + Directory.GetCurrentDirectory() + @"\p2
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  <Import Project=""p3"">

" + Directory.GetCurrentDirectory() + @"\p3
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Basic project importing another one via an ImportGroup, but the ImportGroup condition is false
        /// </summary>
        [TestMethod]
        public void ImportGroupFalseCondition()
        {
            ProjectRootElement xml1 = ProjectRootElement.Create("p1");
            xml1.AddProperty("p", "v1");
            xml1.AddImportGroup().AddImport("p2");
            xml1.LastChild.Condition = "false";
            ProjectRootElement xml2 = ProjectRootElement.Create("p2");
            xml2.AddProperty("p", "v2");

            Project project = new Project(xml1);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--<ImportGroup Condition=""false"">-->
  <!--<Import Project=""p2"" />-->
  <!--</ImportGroup>-->
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Import has a wildcard expression
        /// </summary>
        [TestMethod]
        public void ImportWildcard()
        {
            string directory = null;
            ProjectRootElement xml0, xml1 = null, xml2 = null, xml3 = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(directory);

                xml0 = ProjectRootElement.Create("p1");
                xml0.AddImport(directory + "\\*.targets");

                xml1 = ProjectRootElement.Create(directory + "\\1.targets");
                xml1.AddProperty("p", "v1");
                xml1.Save();

                xml2 = ProjectRootElement.Create(directory + "\\2.targets");
                xml2.AddProperty("p", "v2");
                xml2.Save();

                xml3 = ProjectRootElement.Create(directory + "\\3.xxxxxx");
                xml3.AddProperty("p", "v3");
                xml3.Save();

                Project project = new Project(xml0);

                StringWriter writer = new StringWriter();

                project.SaveLogicalProject(writer);

                string expected = ObjectModelHelpers.CleanupFileContents(
        @"<?xml version=""1.0"" encoding=""utf-16""?>
<!--
============================================================================================================================================
" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <!--
============================================================================================================================================
  <Import Project=""" + Path.Combine(directory, "*.targets") + @""">

" + Path.Combine(directory, "1.targets") + @"
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>
============================================================================================================================================
-->
  <!--
============================================================================================================================================
  <Import Project=""" + Path.Combine(directory, "*.targets") + @""">

" + Path.Combine(directory, "2.targets") + @"
============================================================================================================================================
-->
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
  <!--
============================================================================================================================================
  </Import>

" + Directory.GetCurrentDirectory() + @"\p1
============================================================================================================================================
-->
</Project>");

                Helpers.VerifyAssertLineByLine(expected, writer.ToString());
            }
            finally
            {
                File.Delete(xml1.FullPath);
                File.Delete(xml2.FullPath);
                File.Delete(xml3.FullPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// CDATA node type cloned correctly
        /// </summary>
        [TestMethod]
        public void CData()
        {
            Project project = new Project();
            project.SetProperty("p", "<![CDATA[<sender>John Smith</sender>]]>");
            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p><![CDATA[<sender>John Smith</sender>]]></p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }

        /// <summary>
        /// Metadata named "Project" should not confuse it..
        /// </summary>
        [TestMethod]
        public void ProjectMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='Build' ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
  <ItemGroup>
    <ProjectReference Include='..\CLREXE\CLREXE.vcxproj'>
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(xml);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <ProjectReference Include=""..\CLREXE\CLREXE.vcxproj"">
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertLineByLine(expected, writer.ToString());
        }
    }
}