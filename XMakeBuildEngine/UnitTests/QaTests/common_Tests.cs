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

using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Delegate the GetComponent call back to the test
    /// </summary>
    internal delegate IBuildComponent GetComponentFromTestDelegate(BuildComponentType type);

    /// <summary>
    /// Steps to write the tests
    /// 1) Create a TestProjectDefinition object for each project file or a build request that you will submit
    /// 2) Call Build() on the object to submit the build request
    /// 3) Call ValidateResults() on the object to wait till the build completes and the results sent were what we expected
    /// NOTE: It is not valid to submit multiple build requests simultinously without waiting for the previous one to complete
    /// </summary>
    internal class Common_Tests
    {
        #region Data members

        private QAMockHost host;
        private ConfigCache configCache;
        private TestDataProvider testDataProvider;
        private BuildRequestEngine requestEngine;
        private GetComponentFromTestDelegate getComponent;
        private bool createMSBuildProject;
        private string tempPath;


        #endregion

        #region constructor

        /// <summary>
        /// Setup the delegate for GetComponent request which is delegated from the MockHost
        /// </summary>
        public Common_Tests(GetComponentFromTestDelegate getComponent, bool createMSBuildProject)
        {
            this.getComponent = getComponent;
            this.configCache = null;
            this.host = null;
            this.requestEngine = null;
            this.testDataProvider = null;
            this.createMSBuildProject = createMSBuildProject;
            this.tempPath = System.IO.Path.GetTempPath();
        }

        #endregion

        #region Public method

        /// <summary>
        /// Delegate the types we cannot handle to the test
        /// </summary>
        internal IBuildComponent GetComponent(BuildComponentType type)
        {
            switch (type)
            {
                case BuildComponentType.ConfigCache:
                    return (IBuildComponent)this.configCache;

                case BuildComponentType.TestDataProvider:
                    return (IBuildComponent)this.testDataProvider;

                case BuildComponentType.RequestEngine:
                    return (IBuildComponent)this.requestEngine;
                default:
                    return this.getComponent(type);
            }
        }

        /// <summary>
        /// QA Mock host implementation
        /// </summary>
        public QAMockHost Host
        {
            get
            {
                return this.host;
            }
        }

        #endregion

        #region Common

        /// <summary>
        /// Setup for each tests
        /// </summary>
        public void Setup()
        {
            this.host = new QAMockHost(this.GetComponent);
            this.testDataProvider = new TestDataProvider();
            this.requestEngine = new BuildRequestEngine();
            this.requestEngine.InitializeComponent(this.host);
            this.requestEngine.InitializeForBuild(new NodeLoggingContext(host.LoggingService, 0, false));
            this.configCache = new ConfigCache();
        }

        /// <summary>
        /// cleanup for each tests
        /// </summary>
        public void TearDown()
        {
            this.host.ShutdownComponent();
            this.host = null;
            this.configCache = null;
            this.requestEngine.CleanupForBuild();
            this.requestEngine.ShutdownComponent();
            this.requestEngine = null;
            this.testDataProvider = null;

        }

        #endregion

        #region Simple Build Scenarios

        /// <summary>
        /// Send a build request for a project with 1 target and validate the results. The results validated are the events
        /// raised by the build request engine, the results that was generated by the mock request builder and the cache contents
        /// </summary>
        public void BuildOneProject()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Sends multiple build request and validate the results
        /// </summary>
        public void Build4DifferentProjects()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();

            p4.SubmitBuildRequest();
            p4.ValidateBuildResult();
        }

        #endregion

        #region Caching Scenarios - New requests

        /// <summary>
        /// Build the same project twice with different tools version
        /// </summary>
        public void BuildingTheSameProjectTwiceWithDifferentToolsVersion()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0");
            RequestDefinition p2 = CreateNewRequest("1.proj", "3.5");
 
            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();
        }

        /// <summary>
        /// Build the same project twice with different global properties
        /// </summary>
        public void BuildingTheSameProjectTwiceWithDifferentGlobalProperties()
        {
            ProjectPropertyInstance prop1 = ProjectPropertyInstance.Create("prop1", "Value1");
            ProjectPropertyInstance prop2 = ProjectPropertyInstance.Create("prop2", "Value2");
            PropertyDictionary<ProjectPropertyInstance> group1 = new PropertyDictionary<ProjectPropertyInstance>();
            group1.Set(prop1);
            PropertyDictionary<ProjectPropertyInstance> group2 = new PropertyDictionary<ProjectPropertyInstance>();
            group2.Set(prop2);

            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0", null, group1);
            RequestDefinition p2 = CreateNewRequest("1.proj", "3.0", null, group2);
  
            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();
        }

        /// <summary>
        /// A new build request from the node of a project which was already previously built in that node
        /// </summary>
        public void ReferenceAProjectAlreadyBuiltInTheNode()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            p2.AddChildDefinition(p1);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();
        }

        #endregion

        #region Caching Scenarios - Internal requests

        /// <summary>
        /// A new build request from the node for a project which was already previously built in that node but for a different tools version
        /// </summary>
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentToolsVersion()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0", new string[1] { "t1" }, null);
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("1.proj", "3.5", new string[1] { "t1" }, null);
            p2.AddChildDefinition(p3);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();
        }

        /// <summary>
        /// A new build request from the node for a project which was already previously built in that node but for a different global properties
        /// </summary>
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentGlobalProperties()
        {
            ProjectPropertyInstance prop1 = ProjectPropertyInstance.Create("prop1", "Value1");
            ProjectPropertyInstance prop2 = ProjectPropertyInstance.Create("prop2", "Value2");
            PropertyDictionary<ProjectPropertyInstance> group1 = new PropertyDictionary<ProjectPropertyInstance>();
            group1.Set(prop1);
            PropertyDictionary<ProjectPropertyInstance> group2 = new PropertyDictionary<ProjectPropertyInstance>();
            group2.Set(prop2);

            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0", new string[1] { "t1" }, group1);
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("1.proj", "2.0", new string[1] { "t1" }, group2);
            p2.AddChildDefinition(p3);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();
        }

        #endregion

        #region Callback scenarios

        /// <summary>
        /// Submit 1 build request which has a single reference
        /// </summary>
        public void BuildOneProjectWith1Reference()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            p1.AddChildDefinition(p2);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 1 build request which has 3 reference
        /// </summary>
        public void BuildOneProjectWith3Reference()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            p1.AddChildDefinition(p2);
            p1.AddChildDefinition(p3);
            p1.AddChildDefinition(p4);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 1 build request which has 3 reference where 2 are the same
        /// UNDONE: This test will fail due to a bug where 3.proj is added 2 times in the unresolvedConfigurations list thus causing a hang
        /// </summary>
        public void BuildOneProjectWith3ReferenceWhere2AreTheSame()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("3.proj");
            p1.AddChildDefinition(p2);
            p1.AddChildDefinition(p3);
            p1.AddChildDefinition(p4);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where the second one has a project reference
        /// </summary>
        public void BuildMultipleProjectsWithMiddleProjectHavingReferenceToANewProject()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            p2.AddChildDefinition(p4);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }


        /// <summary>
        /// Submit 3 build request where the first one has a project reference
        /// </summary>
        public void BuildMultipleProjectsWithTheFirstProjectHavingReferenceToANewProject()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            p1.AddChildDefinition(p4);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where the last one has a project reference
        /// </summary>
        public void BuildMultipleProjectsWithTheLastProjectHavingReferenceToANewProject()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            p3.AddChildDefinition(p4);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where all of them have a single reference
        /// </summary>
        public void BuildMultipleProjectsWithEachReferencingANewProject()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            RequestDefinition p5 = CreateNewRequest("5.proj");
            RequestDefinition p6 = CreateNewRequest("6.proj");
            p1.AddChildDefinition(p4);
            p2.AddChildDefinition(p5);
            p3.AddChildDefinition(p6);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();
            
            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where first one has multiple references
        /// </summary>
        public void BuildMultipleProjectsWhereFirstReferencesMultipleNewProjects()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            RequestDefinition p5 = CreateNewRequest("5.proj");
            RequestDefinition p6 = CreateNewRequest("6.proj");
            p1.AddChildDefinition(p4);
            p1.AddChildDefinition(p5);
            p1.AddChildDefinition(p6);

            p1.SubmitBuildRequest(); 
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where first one has multiple references and last has multiple references
        /// </summary>
        public void BuildMultipleProjectsWhereFirstAndLastReferencesMultipleNewProjects()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            RequestDefinition p5 = CreateNewRequest("5.proj");
            RequestDefinition p6 = CreateNewRequest("6.proj");
            RequestDefinition p7 = CreateNewRequest("7.proj");
            p1.AddChildDefinition(p4);
            p1.AddChildDefinition(p5);
            p3.AddChildDefinition(p6);
            p3.AddChildDefinition(p7);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where first one has multiple references and last has multiple references. Some of the references are already built
        /// </summary>
        public void BuildMultipleProjectsWithReferencesWhereSomeReferencesAreAlreadyBuilt()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            RequestDefinition p5 = CreateNewRequest("2.proj");
            RequestDefinition p6 = CreateNewRequest("6.proj");
            RequestDefinition p7 = CreateNewRequest("2.proj");
            p1.AddChildDefinition(p4);
            p1.AddChildDefinition(p5);
            p3.AddChildDefinition(p6);
            p3.AddChildDefinition(p7);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where first one has multiple references and last has multiple references. Some of the references are built with different global properties
        /// </summary>
        public void BuildMultipleProjectsWithReferencesAndDifferentGlobalProperties()
        {
            ProjectPropertyInstance prop1 = ProjectPropertyInstance.Create("prop1", "Value1");
            ProjectPropertyInstance prop2 = ProjectPropertyInstance.Create("prop2", "Value2");
            PropertyDictionary<ProjectPropertyInstance> group1 = new PropertyDictionary<ProjectPropertyInstance>();
            group1.Set(prop1);
            PropertyDictionary<ProjectPropertyInstance> group2 = new PropertyDictionary<ProjectPropertyInstance>();
            group2.Set(prop2);

            RequestDefinition p1 = CreateNewRequest("1.proj");
            RequestDefinition p2 = CreateNewRequest("2.proj");
            RequestDefinition p3 = CreateNewRequest("3.proj");
            RequestDefinition p4 = CreateNewRequest("4.proj");
            RequestDefinition p5 = CreateNewRequest("2.proj", group1);
            RequestDefinition p6 = CreateNewRequest("6.proj");
            RequestDefinition p7 = CreateNewRequest("2.proj", group2);
            p1.AddChildDefinition(p4);
            p1.AddChildDefinition(p5);
            p3.AddChildDefinition(p6);
            p3.AddChildDefinition(p7);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where first one has multiple references and last has multiple references. Some of the references are built with different tools version
        /// </summary>
        public void BuildMultipleProjectsWithReferencesAndDifferentToolsVersion()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0");
            RequestDefinition p2 = CreateNewRequest("2.proj", "2.0");
            RequestDefinition p3 = CreateNewRequest("3.proj", "2.0");
            RequestDefinition p4 = CreateNewRequest("4.proj", "2.0");
            RequestDefinition p5 = CreateNewRequest("2.proj", "3.5");
            RequestDefinition p6 = CreateNewRequest("6.proj", "2.0");
            RequestDefinition p7 = CreateNewRequest("3.proj", "3.5");
            p1.AddChildDefinition(p4);
            p1.AddChildDefinition(p5);
            p3.AddChildDefinition(p6);
            p3.AddChildDefinition(p7);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where project 1 has a reference to Project 3
        /// </summary>
        public void Build3ProjectsWhere1HasAReferenceTo3()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0");
            RequestDefinition p2 = CreateNewRequest("2.proj", "2.0");
            RequestDefinition p3 = CreateNewRequest("3.proj", "2.0");
            p1.AddChildDefinition(p3);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where project 2 has a reference to Project 3
        /// </summary>
        public void Build3ProjectsWhere2HasAReferenceTo3()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0");
            RequestDefinition p2 = CreateNewRequest("2.proj", "2.0");
            RequestDefinition p3 = CreateNewRequest("3.proj", "2.0");
            p2.AddChildDefinition(p3);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        /// <summary>
        /// Submit 3 build request where project 3 has a reference to Project 1
        /// </summary>
        public void Build3ProjectsWhere3HasAReferenceTo1()
        {
            RequestDefinition p1 = CreateNewRequest("1.proj", "2.0");
            RequestDefinition p2 = CreateNewRequest("2.proj", "2.0");
            RequestDefinition p3 = CreateNewRequest("3.proj", "2.0");
            p3.AddChildDefinition(p1);

            p1.SubmitBuildRequest();
            p1.ValidateBuildResult();

            p2.SubmitBuildRequest();
            p2.ValidateBuildResult();

            p3.SubmitBuildRequest();
            p3.ValidateBuildResult();
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Helper method to create a new request definition given a filename. 
        /// </summary>
        private RequestDefinition CreateNewRequest(string projectFile)
        {
            return CreateNewRequest(projectFile, null, null, null);
        }

        /// <summary>
        /// Helper method to create a new request definition given a filename and tools version
        /// </summary>
        private RequestDefinition CreateNewRequest(string projectFile, string toolsversion)
        {
            return CreateNewRequest(projectFile, toolsversion, null, null);
        }

        /// <summary>
        /// Helper method to create a new request definition given a filename and global properties
        /// </summary>
        private RequestDefinition CreateNewRequest(string projectFile, PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            return CreateNewRequest(projectFile, null, null, globalProperties);
        }

        /// <summary>
        /// Helper method to create a new request definition given a filename, tools version, targets and global properties
        /// </summary>
        private RequestDefinition CreateNewRequest(string projectFile, string toolsversion, string[] targetsToBuild, PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            if (targetsToBuild == null)
            {
                targetsToBuild = new string[1] { RequestDefinition.defaultTargetName };
            }

            return InternalCreateNewRequest(projectFile, toolsversion, targetsToBuild, globalProperties);
        }

        /// <summary>
        /// Helper method to create the request definition and setting the request definition will have an actual project instance or not
        /// </summary>
        private RequestDefinition InternalCreateNewRequest(string projectFile, string toolsversion, string[] targetsToBuild, PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            // Make sure that the path is rooted. This is particularly important when testing implementation of RequestBuilder. The RequestBuild adds default path if the project file path 
            // is not rooted. This will cause us to not be able to locate the approprate RequestDefinition as the file name is also used as a comparing mechinasim
            projectFile = System.IO.Path.Combine(this.tempPath, projectFile);
            RequestDefinition p1 = new RequestDefinition(projectFile, toolsversion, targetsToBuild, globalProperties, 0, null, (IBuildComponentHost)this.host);

            // If a project object is to be created then we will need to add all targets that we will be building to the project XML
            if (this.createMSBuildProject)
            {
                p1.CreateMSBuildProject = true;
                ProjectDefinition p = p1.ProjectDefinition;
                foreach (string target in targetsToBuild)
                {
                    TargetDefinition t = new TargetDefinition(target, p.ProjectXmlDocument);
                    p1.ProjectDefinition.AddTarget(t);
                }
            }
            
            return p1;
        }

        #endregion
    }
}
