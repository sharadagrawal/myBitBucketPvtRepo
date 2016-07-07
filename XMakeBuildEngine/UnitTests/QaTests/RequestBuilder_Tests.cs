﻿using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Steps to write the tests
    /// 1) Create a TestProjectDefinition object for each project file or a build request that you will submit
    /// 2) Call Build() on the object to submit the build request
    /// 3) Call ValidateResults() on the object to wait till the build completes and the results sent were what we expected
    /// NOTE: It is not valid to submit multiple build requests simultinously without waiting for the previous one to complete
    /// </summary>
    [TestClass]
    [Ignore] // "Test infrastructure is out of sync with the BuildRequestEngine/Scheduler."
    public class RequestBuilder_Tests
    {
        #region Data members

        private Common_Tests commonTests;
        private ResultsCache resultsCache;
        private string tempPath;

        #endregion

        #region Constructor

        /// <summary>
        /// Setup the object
        /// </summary>
        public RequestBuilder_Tests()
        {
            this.commonTests = new Common_Tests(this.GetComponent, true);
            this.resultsCache = null;
            this.tempPath = System.IO.Path.GetTempPath();
        }

        #endregion

        #region Common

        /// <summary>
        /// Delegate to common test setup
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            this.resultsCache = new ResultsCache();
            this.commonTests.Setup();
        }

        /// <summary>
        /// Delegate to common test teardown
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            this.commonTests.TearDown();
            this.resultsCache = null;

        }

        #endregion

        #region GetComponent delegate

        /// <summary>
        /// Provides the components required by the tests
        /// </summary>
        internal IBuildComponent GetComponent(BuildComponentType type)
        {
            switch (type)
            {
                case BuildComponentType.RequestBuilder:
                    RequestBuilder requestBuilder = new RequestBuilder();
                    return (IBuildComponent)requestBuilder;

                case BuildComponentType.TaskBuilder:
                    TaskBuilder taskBuilder = new TaskBuilder();
                    return (IBuildComponent)taskBuilder;

                case BuildComponentType.TargetBuilder:
                    QAMockTargetBuilder targetBuilder = new QAMockTargetBuilder();
                    return (IBuildComponent)targetBuilder;

                case BuildComponentType.ResultsCache:
                    return (IBuildComponent)this.resultsCache;

                default:
                    throw new ArgumentException("Unexpected type requested. Type = " + type.ToString());
            }
        }

        #endregion

        #region Project build

        /// <summary>
        /// Build 1 project containing a single target
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1Target()
        {
            RequestDefinition p1 = CreateRequestDefinition("1.proj", null, null, 0);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Build 1 project containing a 4 targets
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith4Targets()
        {
            RequestDefinition p1 = CreateRequestDefinition("1.proj", new string[4] { "target1", "target2", "target3", "target4" }, null, 0);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Build 4 project containing a single target
        /// </summary>
        [TestMethod]
        public void Build4ProjectWith1Target()
        {
            RequestDefinition p1 = CreateRequestDefinition("1.proj", null, null, 0);
            RequestDefinition p2 = CreateRequestDefinition("2.proj", null, null, 0);
            RequestDefinition p3 = CreateRequestDefinition("3.proj", null, null, 0);
            RequestDefinition p4 = CreateRequestDefinition("4.proj", null, null, 0);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();

            p4.SubmitBuildRequest();
            p4.ValidateBuildResult();
        }

        /// <summary>
        /// Build 4 project containing a 4 target each
        /// </summary>
        [TestMethod]
        public void Build4ProjectWith4Targets()
        {
            RequestDefinition p1 = CreateRequestDefinition("1.proj", new string[4] { "target1", "target2", "target3", "target4" }, null, 0);
            RequestDefinition p2 = CreateRequestDefinition("2.proj", new string[4] { "target1", "target2", "target3", "target4" }, null, 0);
            RequestDefinition p3 = CreateRequestDefinition("3.proj", new string[4] { "target1", "target2", "target3", "target4" }, null, 0);
            RequestDefinition p4 = CreateRequestDefinition("4.proj", new string[4] { "target1", "target2", "target3", "target4" }, null, 0);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();

            p4.SubmitBuildRequest();
            p4.ValidateBuildResult();
        }

        /// <summary>
        /// Build a single project with a target that takes time to execute. Send a cancel by shutting down the engine
        /// </summary>
        [TestMethod]
        public void TestCancellingRequest()
        {
            RequestDefinition p1 = CreateRequestDefinition("1.proj", null, null, 0);
            p1.WaitForCancel = true;
            p1.SubmitBuildRequest();

            this.commonTests.Host.AbortBuild();

            p1.ValidateBuildAbortedResult();
        }

        #endregion

        #region Common Tests

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProject()
        {
            this.commonTests.BuildOneProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build4Projects()
        {
            this.commonTests.Build4DifferentProjects();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildingTheSameProjectTwiceWithDifferentToolsVersion()
        {
            this.commonTests.BuildingTheSameProjectTwiceWithDifferentToolsVersion();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildingTheSameProjectTwiceWithDifferentToolsGlobalProperties()
        {
            this.commonTests.BuildingTheSameProjectTwiceWithDifferentGlobalProperties();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNode()
        {
            this.commonTests.ReferenceAProjectAlreadyBuiltInTheNode();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentToolsVersion()
        {
            this.commonTests.ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentToolsVersion();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentGlobalProperties()
        {
            this.commonTests.ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentGlobalProperties();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith1Reference()
        {
            this.commonTests.BuildOneProjectWith1Reference();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith3Reference()
        {
            this.commonTests.BuildOneProjectWith3Reference();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith3ReferenceWhere2AreTheSame()
        {
            this.commonTests.BuildOneProjectWith3ReferenceWhere2AreTheSame();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithMiddleProjectHavingReferenceToANewProject()
        {
            this.commonTests.BuildMultipleProjectsWithMiddleProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithTheFirstProjectHavingReferenceToANewProject()
        {
            this.commonTests.BuildMultipleProjectsWithTheFirstProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithTheLastProjectHavingReferenceToANewProject()
        {
            this.commonTests.BuildMultipleProjectsWithTheLastProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithEachReferencingANewProject()
        {
            this.commonTests.BuildMultipleProjectsWithEachReferencingANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWhereFirstReferencesMultipleNewProjects()
        {
            this.commonTests.BuildMultipleProjectsWhereFirstReferencesMultipleNewProjects();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWhereFirstAndLastReferencesMultipleNewProjects()
        {
            this.commonTests.BuildMultipleProjectsWhereFirstAndLastReferencesMultipleNewProjects();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithReferencesWhereSomeReferencesAreAlreadyBuilt()
        {
            this.commonTests.BuildMultipleProjectsWithReferencesWhereSomeReferencesAreAlreadyBuilt();
        }

        [TestMethod]
        public void BuildMultipleProjectsWithReferencesAndDifferentGlobalProperties()
        {
            this.commonTests.BuildMultipleProjectsWithReferencesAndDifferentGlobalProperties();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithReferencesAndDifferentToolsVersion()
        {
            this.commonTests.BuildMultipleProjectsWithReferencesAndDifferentToolsVersion();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere1HasAReferenceTo3()
        {
            this.commonTests.Build3ProjectsWhere1HasAReferenceTo3();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere2HasAReferenceTo3()
        {
            this.commonTests.Build3ProjectsWhere2HasAReferenceTo3();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere3HasAReferenceTo1()
        {
            this.commonTests.Build3ProjectsWhere3HasAReferenceTo1();
        }

        #endregion

        /// <summary>
        /// Returns a new Request definition object created using the parameters passed in
        /// </summary>
        /// <param name="filename">Name of the project file in the request. This needs to be a rooted path</param>
        /// <param name="targets">Targets to build in that project file</param>
        /// <param name="toolsVersion">Tools version for that project file</param>
        /// <param name="executionTime">Simulated time the request should take to complete</param>
        private RequestDefinition CreateRequestDefinition(string filename, string[] targets, string toolsVersion, int executionTime)
        {
            if (targets == null)
            {
                targets = new string[1] { RequestDefinition.defaultTargetName };
            }

            if (toolsVersion == null)
            {
                toolsVersion = "2.0";
            }

            filename = System.IO.Path.Combine(tempPath, filename);
            RequestDefinition request = new RequestDefinition(filename, toolsVersion, targets, null, executionTime, null, (IBuildComponentHost)this.commonTests.Host);
            request.CreateMSBuildProject = true;

            return request;
        }
    }
}