﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for the task execution host object.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using System.Reflection;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Test the task registry
    /// </summary>
    [TestClass]
    public class TaskRegistry_Tests
    {
        /// <summary>
        /// Name of the test task built into the test 
        /// assembly at testTaskLocation.
        /// </summary>
        private const string TestTaskName = "TestTask";

        /// <summary>
        /// Location of the generated test task DLL.
        /// </summary>
        private static string s_testTaskLocation;

        /// <summary>
        /// Logging service to use in for the task registry
        /// </summary>
        private static ILoggingService s_loggingService;

        /// <summary>
        /// Mock logger which is attached to the logging service.
        /// </summary>
        private static MockLogger s_logger;

        /// <summary>
        /// Target logging context to use when logging. 
        /// </summary>
        private static TargetLoggingContext s_targetLoggingContext;

        /// <summary>
        /// Build event context to use when logging
        /// </summary>
        private static BuildEventContext s_loggerContext = new BuildEventContext(2, 2, 2, 2);

        /// <summary>
        /// Element location to use when logging 
        /// </summary>
        private static ElementLocation s_elementLocation = ElementLocation.Create("c:\\project.proj", 0, 0);

        /// <summary>
        /// Set up this test class -- generate the test task assembly used by 
        /// several of the tests. 
        /// </summary>
        [ClassInitialize]
        public static void SetupClass(TestContext context)
        {
            s_testTaskLocation = GetTestTaskAssemblyLocation();
        }

        /// <summary>
        /// Clean this test class up -- make sure the test task assembly we 
        /// generated has been deleted. 
        /// </summary>
        [ClassCleanup]
        public static void CleanupClass()
        {
            if (File.Exists(s_testTaskLocation))
            {
                FileUtilities.DeleteNoThrow(s_testTaskLocation);
            }
        }

        /// <summary>
        /// Setup some logging services so we can see what is goign on.
        /// </summary>
        [TestInitialize]
        public void SetUp()
        {
            s_loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1) as ILoggingService;
            s_logger = new MockLogger();
            s_targetLoggingContext = new TargetLoggingContext(s_loggingService, s_loggerContext);

            s_loggingService.RegisterLogger(s_logger);
        }

        #region UsingTaskTests
        /// <summary>
        /// Try and register a simple task
        /// Expect:
        ///     One task to be registered and that it has the correct assembly information registered.
        /// </summary>
        [TestMethod]
        public void RegisterTaskSimple()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("CustomTask", null, "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(1, registeredTaskCount, "Expected one registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (ProjectUsingTaskElement taskElement in elementList)
            {
                List<TaskRegistry.RegisteredTaskRecord> registrationRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(taskElement.TaskName, null)];
                Assert.IsNotNull(registrationRecords, "Task registrationrecord not found in TaskRegistry.TaskRegistrations!");
                Assert.AreEqual(1, registrationRecords.Count, "Expected only one record registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = registrationRecords[0].TaskFactoryAssemblyLoadInfo;
                string assemblyName = String.IsNullOrEmpty(taskElement.AssemblyName) ? null : taskElement.AssemblyName;
                string assemblyFile = String.IsNullOrEmpty(taskElement.AssemblyFile) ? null : taskElement.AssemblyFile;
                Assert.AreEqual(taskAssemblyLoadInfo, AssemblyLoadInfo.Create(assemblyName, assemblyFile), "Task record was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        /// <summary>
        /// Register many tasks with different names
        /// Expect:
        ///     Three tasks to be regisered
        ///     Expect only one task to be regisered under each task name
        ///     Expect the correct assembly information to be registered
        /// </summary>
        [TestMethod]
        public void RegisterMultipleTasksWithDifferentNames()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("CustomTask", null, "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", "bin\\Assemblies\\YetAnotherCustomTask.dll", null);
            elementList.Add(element);

            element = project.AddUsingTask("AnotherCustomTask", null, "AnotherCustomTask, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (ProjectUsingTaskElement taskElement in elementList)
            {
                List<TaskRegistry.RegisteredTaskRecord> registrationRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(taskElement.TaskName, null)];
                Assert.IsNotNull(registrationRecords, "Task registrationrecord not found in TaskRegistry.TaskRegistrations!");
                Assert.AreEqual(1, registrationRecords.Count, "Expected only one record registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = registrationRecords[0].TaskFactoryAssemblyLoadInfo;

                string assemblyName = String.IsNullOrEmpty(taskElement.AssemblyName) ? null : taskElement.AssemblyName;
                string assemblyFile = String.IsNullOrEmpty(taskElement.AssemblyFile) ? null : taskElement.AssemblyFile;

                Assert.AreEqual(taskAssemblyLoadInfo, AssemblyLoadInfo.Create(assemblyName, assemblyFile == null ? null : Path.GetFullPath(assemblyFile)), "Task record was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        /// <summary>
        /// Register the same task multiple times with the same name
        ///     Expect:
        ///         Three three tasks to be registered
        ///         Expect two of the tasks to be under the same task name bucket
        ///         Expect the correct assembly information to be registered for each of the tasks
        /// </summary>
        [TestMethod]
        public void RegisterMultipleTasksSomeWithSameName()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("CustomTask", null, "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", null, "YetAnotherCustomTask, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            element = project.AddUsingTask("CustomTask", null, "CustomTask, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            // First assert that there are two unique buckets
            Assert.AreEqual(2, registry.TaskRegistrations.Count, "Expected only two buckets since two of three tasks have the same name!");

            // Now let's look at the bucket with only one task
            List<TaskRegistry.RegisteredTaskRecord> singletonBucket = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(elementList[1].TaskName, null)];
            Assert.IsNotNull(singletonBucket, "Record not found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(1, singletonBucket.Count, "Expected only Record registered under this TaskName!");
            AssemblyLoadInfo singletonAssemblyLoadInfo = singletonBucket[0].TaskFactoryAssemblyLoadInfo;
            string assemblyName = String.IsNullOrEmpty(elementList[1].AssemblyName) ? null : elementList[1].AssemblyName;
            string assemblyFile = String.IsNullOrEmpty(elementList[1].AssemblyFile) ? null : elementList[1].AssemblyFile;
            Assert.AreEqual(singletonAssemblyLoadInfo, AssemblyLoadInfo.Create(assemblyName, assemblyFile), "Task record was not properly registered by TaskRegistry.RegisterTask!");

            // Now let's look at the bucket with two tasks
            List<TaskRegistry.RegisteredTaskRecord> duplicateBucket = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(elementList[0].TaskName, null)];
            Assert.IsNotNull(duplicateBucket, "Records not found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(2, duplicateBucket.Count, "Expected two Records registered under this TaskName!");

            bool foundFirstLoadInfo = false;
            bool foundSecondLoadInfo = false;
            foreach (TaskRegistry.RegisteredTaskRecord record in duplicateBucket)
            {
                assemblyName = String.IsNullOrEmpty(elementList[0].AssemblyName) ? null : elementList[0].AssemblyName;
                assemblyFile = String.IsNullOrEmpty(elementList[0].AssemblyFile) ? null : elementList[0].AssemblyFile;
                if (record.TaskFactoryAssemblyLoadInfo.Equals(AssemblyLoadInfo.Create(assemblyName, assemblyFile)))
                {
                    foundFirstLoadInfo = true;
                }

                assemblyName = String.IsNullOrEmpty(elementList[2].AssemblyName) ? null : elementList[2].AssemblyName;
                assemblyFile = String.IsNullOrEmpty(elementList[2].AssemblyFile) ? null : elementList[2].AssemblyFile;
                if (record.TaskFactoryAssemblyLoadInfo.Equals(AssemblyLoadInfo.Create(assemblyName, assemblyFile)))
                {
                    foundSecondLoadInfo = true;
                }
            }

            Assert.IsTrue(foundFirstLoadInfo, "Expected first task to be registered in this bucket!");
            Assert.IsTrue(foundSecondLoadInfo, "Expected second task to be registered in this bucket!");
        }

        /// <summary>
        /// Register multiple tasks with different names in the same assembly
        /// Expect:
        ///     Three tasks to be registered
        /// </summary>
        [TestMethod]
        public void RegisterMultipleTasksWithDifferentNamesFromSameAssembly()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("CustomTask", null, "CustomTasks, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", "bin\\Assemblies\\YetAnotherCustomTask.dll", null);
            elementList.Add(element);

            element = project.AddUsingTask("AnotherCustomTask", null, "CustomTasks, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (ProjectUsingTaskElement taskElement in elementList)
            {
                List<TaskRegistry.RegisteredTaskRecord> registrationRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(taskElement.TaskName, null)];
                Assert.IsNotNull(registrationRecords, "Task registrationrecord not found in TaskRegistry.TaskRegistrations!");
                Assert.AreEqual(1, registrationRecords.Count, "Expected only one record registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = registrationRecords[0].TaskFactoryAssemblyLoadInfo;
                string assemblyName = String.IsNullOrEmpty(taskElement.AssemblyName) ? null : taskElement.AssemblyName;
                string assemblyFile = String.IsNullOrEmpty(taskElement.AssemblyFile) ? null : taskElement.AssemblyFile;
                Assert.AreEqual(taskAssemblyLoadInfo, AssemblyLoadInfo.Create(assemblyName, assemblyFile == null ? null : Path.GetFullPath(assemblyFile)), "Task record was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        /// <summary>
        /// Register multiple tasks with the same name in the same assembly
        /// Expect:
        ///     Three tasks to be registered
        ///     Two of the tasks should be in the same name bucket
        /// </summary>
        [TestMethod]
        public void RegisterMultipleTasksWithSameNameAndSameAssembly()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("CustomTask", "Some\\Relative\\Path\\CustomTasks.dll", null);
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", null, "YetAnotherCustomTask, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null");
            elementList.Add(element);

            element = project.AddUsingTask("CustomTask", "Some\\Relative\\Path\\CustomTasks.dll", null);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // two unique buckets
            Assert.AreEqual(2, registry.TaskRegistrations.Count, "Expected only two buckets since two of three tasks have the same name!");
            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");
        }

        /// <summary>
        /// Validate registration of tasks with different combinations of task parameters.  
        /// Expected that an otherwise equivalent task will be recognized as a separate task if it has
        /// different task parameters set.
        /// </summary>
        [TestMethod]
        public void RegisterTasksWithFactoryParameters()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Task", "c:\\TaskLocation\\Tasks.dll", null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            element = project.AddUsingTask("Task", "c:\\TaskLocation\\Tasks.dll", null);
            element.Runtime = "CLR4";
            element.Architecture = "x64";
            elementList.Add(element);

            element = project.AddUsingTask("Task", "c:\\TaskLocation\\Tasks.dll", null);
            element.Runtime = "CLR4";
            element.Architecture = "*";
            elementList.Add(element);

            element = project.AddUsingTask("Task", "c:\\TaskLocation\\Tasks.dll", null);
            element.Runtime = "CLR4";
            element.Architecture = "x64";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            Assert.AreEqual(3, registry.TaskRegistrations.Count, "Should have three buckets, since two of the tasks are the same.");
            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(4, registeredTaskCount);
        }

        #region Cache read tests

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheTaskDoesNotExist_ExactMatch()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("UnrelatedTask", s_testTaskLocation, null);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Not in registry, so shouldn't match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // Still not in registry, so shouldn't match this time either -- and we should pull from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheTaskDoesNotExist_FuzzyMatch()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("UnrelatedTask", s_testTaskLocation, null);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Not in registry, so shouldn't match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // Still not in registry, so shouldn't match this time either -- and we should pull from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheMatchingTaskDoesNotExist_FuzzyMatch()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Not in registry, so shouldn't match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: "CLR2",
                    architecture: "*",
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // Still not in registry, so shouldn't match this time either -- and we should pull from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: "CLR2",
                    architecture: "*",
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheMatchingTaskDoesNotExistOnFirstCallButDoesOnSecond()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Not in registry, so shouldn't match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: "CLR2",
                    architecture: "*",
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // Still not in registry, so shouldn't match this time either -- and we should pull from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheMatchingExactParameters()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // no parameters - no match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // parameters that would be a successful fuzzy match - no match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // parameters that are a successful exact match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false
                );

            // parameters that do not match - should not retrieve
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr2,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // exact match #2 -- should get it from the cache this time
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: true,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters beyond just runtime and architecture.  Hint: it shouldn't 
        /// ever work, since we don't currently have a way to create a using task with 
        /// parameters other than those two. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheMatchingExactParameters_AdditionalParameters()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Runtime and architecture match the using task exactly, but since there is an additional parameter, it still 
            // doesn't match when doing exact matching. 
            Dictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.x86);
            taskParameters.Add("Foo", "Bar");

            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    true /* exact match */,
                    taskParameters,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // However, it should still match itself -- so if we try again, we should get the "no match" 
            // back from the cache this time.  
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    true /* exact match */,
                    taskParameters,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Test retrieving a matching task record using various parameter combinations when allowing 
        /// fuzzy matches.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // no parameters
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that are a successful exact match - should retrieve from cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that would be a successful fuzzy match - should still be retrieved from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that do not match -- but would match the previous fuzzy match request. Should NOT retrieve anything
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // and another fuzzy match -- should still be pulling from the cache.
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.any,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );
        }

        /// <summary>
        /// Test retrieving a matching task record using various parameter combinations when allowing 
        /// fuzzy matches.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters_RecoverFromFailure()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // no parameters - should retrieve the record
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that do not match at all - shouldn't retrieve anything
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr2,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // parameters that are a successful match - should retrieve from the cache this time
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );
        }

        /// <summary>
        /// Test fuzzy matching of parameters when retrieving task records when there are 
        /// multiple using tasks registered for the same task, just with different parameter 
        /// sets. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters_MultipleUsingTasks()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "*";
            element.Architecture = "x64";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // no parameters -- gets the first one (CLR4|x86)
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: null,
                    architecture: null,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that are a successful exact match for CLR4|x86 -- should come from cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that would be a successful fuzzy match for either, so should get the one in the cache (CLR4|x86)
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // parameters that match *|x64 - should retrieve that
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.any,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x64
                );

            // match CLR4|x86 again - comes from the cache
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.any,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // match *|x64 again 
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr2,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.any,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x64
                );

            // CLR2|x86 should not match either task record
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr2,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x86,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // match *|x64 again -- should still be a cache hit
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr2,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.any,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x64
                );
        }

        /// <summary>
        /// Test fuzzy matching of parameters when retrieving task records when there are 
        /// multiple using tasks registered for the same task, just with different parameter 
        /// sets. Specific sub-test:  although we generally pick the first available record if 
        /// there are multiple matches, if we are doing fuzzy matching, we should prefer the 
        /// record that's in the cache, even if it wasn't the original first record. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters_MultipleUsingTasks_PreferCache()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "*";
            element.Architecture = "x64";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // CLR4|x64 -- should be fulfilled by *|x64
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.any,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x64
                );

            // CLR4|* -- could be filled by either, would normally be filled by CLR4|x86 (since it was registered first), 
            // but since *|x64 is in the cache already, we return that one. 
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.any,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x64
                );
        }

        /// <summary>
        /// Test retrieving a matching task record using various parameter combinations when allowing 
        /// fuzzy matches.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters_ExactMatches()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // CLR4|* should match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // CLR4|x64 should not match
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: false
                );

            // try CLR4|* again -- should resolve correctly from the cache. 
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.any,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // try CLR4|x64 again -- should also come from the catch (but needless to say, still not be a match)
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    exactMatchRequired: false,
                    runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    architecture: XMakeAttributes.MSBuildArchitectureValues.x64,
                    shouldBeRetrieved: false,
                    shouldBeRetrievedFromCache: true
                );
        }

        /// <summary>
        /// Validate task retrieval and exact cache retrieval when attempting to load 
        /// a task with parameters beyond just runtime and architecture.  Hint: it shouldn't 
        /// ever work, since we don't currently have a way to create a using task with 
        /// parameters other than those two. 
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires dependent components (e.g. csc2.exe).
        public void RetrieveFromCacheFuzzyMatchingParameters_AdditionalParameters()
        {
            Assert.IsNotNull(s_testTaskLocation, "Need a test task to run this test");

            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask(TestTaskName, s_testTaskLocation, null);
            element.Runtime = "CLR4";
            element.Architecture = "x86";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Runtime and architecture match, so even though we have the extra parameter, it should still match 
            Dictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.x86);
            taskParameters.Add("Foo", "Bar");

            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    false /* fuzzy match */,
                    taskParameters,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: false,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            // And if we try again, we should get it from the cache this time.  
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    false /* fuzzy match */,
                    taskParameters,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );

            taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.x86);
            taskParameters.Add("Baz", "Qux");

            // Even with a different value to the additional parameter, because it's a fuzzy equals and because all 
            // our equivalency check looks for is runtime and architecture, it still successfully retrieves the 
            // existing record from the cache. 
            RetrieveAndValidateRegisteredTaskRecord
                (
                    registry,
                    false /* fuzzy match */,
                    taskParameters,
                    shouldBeRetrieved: true,
                    shouldBeRetrievedFromCache: true,
                    expectedRuntime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                    expectedArchitecture: XMakeAttributes.MSBuildArchitectureValues.x86
                );
        }

        #endregion

        /// <summary>
        /// Verify the using task attributes are expanded correctly
        /// Expect:
        ///     Expanded property and item values to be correct for each of the attributes
        /// </summary>
        [TestMethod]
        public void AllUsingTaskAttributesAreExpanded()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("$(Property1)@(ThirdItem)$(Property2)", "Some\\$(Property3)\\Path\\CustomTasks.dll", null);
            element.TaskFactory = "$(Property1)@(ThirdItem)$(Property2)";
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", null, "$(Property4)@(ThirdItem), Version=9.0.0.0, Culture=neutral, PublicKeyToken=null");
            element.TaskFactory = "";
            elementList.Add(element);

            element = project.AddUsingTask("Custom$(Property5)Task", "Some\\Relative\\Path\\CustomTasks.dll", null);
            element.TaskFactory = null;
            element.Condition = "'@(ThirdItem)$(Property1)' == 'ThirdValue1Value1'";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");

            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            foreach (ProjectUsingTaskElement taskElement in elementList)
            {
                string expandedtaskName = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.TaskName, ExpanderOptions.ExpandPropertiesAndItems, taskElement.TaskNameLocation);
                string expandedAssemblyName = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.AssemblyName, ExpanderOptions.ExpandPropertiesAndItems, taskElement.AssemblyNameLocation);
                string expandedAssemblyFile = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.AssemblyFile, ExpanderOptions.ExpandPropertiesAndItems, taskElement.AssemblyFileLocation);
                string expandedTaskFactory = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.TaskFactory, ExpanderOptions.ExpandPropertiesAndItems, taskElement.TaskFactoryLocation);

                expandedAssemblyName = String.IsNullOrEmpty(expandedAssemblyName) ? null : expandedAssemblyName;
                expandedAssemblyFile = String.IsNullOrEmpty(expandedAssemblyFile) ? null : expandedAssemblyFile;
                expandedTaskFactory = String.IsNullOrEmpty(expandedTaskFactory) ? "AssemblyTaskFactory" : expandedTaskFactory;

                List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(expandedtaskName, null)];
                Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
                Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

                Assert.AreEqual(expandedTaskFactory, registeredTaskRecords[0].TaskFactoryAttributeName);

                AssemblyLoadInfo taskAssemblyLoadInfo = registeredTaskRecords[0].TaskFactoryAssemblyLoadInfo;
                Assert.AreEqual(taskAssemblyLoadInfo, AssemblyLoadInfo.Create(expandedAssemblyName, expandedAssemblyFile == null ? null : Path.GetFullPath(expandedAssemblyFile)), "Task record was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        /// <summary>
        /// Verify tasks are registered only if the condition on the using task is true
        /// Expect:
        ///     Expect two of the conditions to evaluate to false causing two of the tasks to not be registered
        /// </summary>
        [TestMethod]
        public void TaskRegisteredOnlyIfConditionIsTrue()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("$(Property1)@(ThirdItem)$(Property2)", "Some\\$(Property3)\\Path\\CustomTasks.dll", null);
            element.Condition = "'true' != 'false'";
            elementList.Add(element);

            element = project.AddUsingTask("YetAnotherCustomTask", null, "$(Property4)@(ThirdItem), Version=9.0.0.0, Culture=neutral, PublicKeyToken=null");
            element.Condition = "false";
            elementList.Add(element);

            element = project.AddUsingTask("Custom$(Property5)Task", "Some\\Relative\\Path\\CustomTasks.dll", null);
            element.Condition = "'@(ThirdItem)$(Property1)' == 'ThirdValue1Value1'";
            elementList.Add(element);

            element = project.AddUsingTask("MyTask", "TasksAssembly.dll", null);
            element.Condition = "'@(ThirdItem)$(Property1)' == 'ThirdValue1'";
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(2, registeredTaskCount, "Expected two registered tasks in TaskRegistry.TaskRegistrations!");

            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            for (int i = 0; i <= 2; i += 2)
            {
                ProjectUsingTaskElement taskElement = elementList[i];
                string expandedtaskName = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.TaskName, ExpanderOptions.ExpandPropertiesAndItems, taskElement.TaskNameLocation);
                string expandedAssemblyName = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.AssemblyName, ExpanderOptions.ExpandPropertiesAndItems, taskElement.AssemblyNameLocation);
                string expandedAssemblyFile = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(taskElement.AssemblyFile, ExpanderOptions.ExpandPropertiesAndItems, taskElement.AssemblyFileLocation);

                expandedAssemblyName = String.IsNullOrEmpty(expandedAssemblyName) ? null : expandedAssemblyName;
                expandedAssemblyFile = String.IsNullOrEmpty(expandedAssemblyFile) ? null : expandedAssemblyFile;

                List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity(expandedtaskName, null)];
                Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
                Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = registeredTaskRecords[0].TaskFactoryAssemblyLoadInfo;
                Assert.AreEqual(taskAssemblyLoadInfo, AssemblyLoadInfo.Create(expandedAssemblyName, Path.GetFullPath(expandedAssemblyFile)), "Task record was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        /// <summary>
        /// Verify that when there are no child elements on the using task that there are no ParameterGroupAndTaskBody
        /// </summary>
        [TestMethod]
        public void NoChildrenElements()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Hello", "File", null);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(1, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");

            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            ProjectUsingTaskElement taskElement = elementList[0];
            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Hello", null)];
            Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");
            Assert.AreEqual(0, registeredTaskRecords[0].ParameterGroupAndTaskBody.UsingTaskParameters.Count);
            Assert.IsNull(registeredTaskRecords[0].ParameterGroupAndTaskBody.InlineTaskXmlBody);
        }
        #endregion

        #region ParameterGroupTests
        /// <summary>
        /// Verify that when there is a parametergroup that there is a ParameterGroupAndTaskBody but that there are no parameters in it.
        /// </summary>
        [TestMethod]
        public void EmptyParameterGroup()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperDuperFactory";

            // Add empty parameterGroup
            element.AddParameterGroup();
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(1, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");
            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            ProjectUsingTaskElement taskElement = elementList[0];
            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)];
            Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");
            TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord = registeredTaskRecords[0].ParameterGroupAndTaskBody;
            Assert.IsNotNull(inlineTaskRecord);
            Assert.IsNull(inlineTaskRecord.InlineTaskXmlBody);
            Assert.IsTrue(inlineTaskRecord.UsingTaskParameters.Count == 0);
        }

        /// <summary>
        /// Verify that when when multiple parameters are set that they show up in the parametergroup object
        /// </summary>
        [TestMethod]
        public void MultipleGoodParameters()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";

            // Add empty parameterGroup
            UsingTaskParameterGroupElement parameterGroup = element.AddParameterGroup();
            ProjectUsingTaskParameterElement defaultParameter = parameterGroup.AddParameter("ParameterWithNoAttributes");

            ProjectUsingTaskParameterElement filledOutAttributesParameter = parameterGroup.AddParameter("ParameterWithAllAttributesHardCoded", bool.TrueString, bool.TrueString, typeof(Int32).FullName);

            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(1, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");
            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            ProjectUsingTaskElement taskElement = elementList[0];
            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)];
            Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

            TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord = registeredTaskRecords[0].ParameterGroupAndTaskBody;
            Assert.IsNotNull(inlineTaskRecord);
            Assert.IsNull(inlineTaskRecord.InlineTaskXmlBody);
            Assert.IsTrue(inlineTaskRecord.UsingTaskParameters.Count == 2);

            TaskPropertyInfo parameterInfo = inlineTaskRecord.UsingTaskParameters[defaultParameter.Name] as TaskPropertyInfo;
            Assert.IsNotNull(parameterInfo);
            Assert.AreEqual(parameterInfo.Name, defaultParameter.Name);
            Assert.AreEqual(parameterInfo.Output, false);
            Assert.AreEqual(parameterInfo.Required, false);
            Assert.AreEqual(parameterInfo.PropertyType, typeof(System.String));

            parameterInfo = inlineTaskRecord.UsingTaskParameters[filledOutAttributesParameter.Name] as TaskPropertyInfo;
            Assert.IsNotNull(parameterInfo);
            Assert.AreEqual(parameterInfo.Name, filledOutAttributesParameter.Name);
            Assert.AreEqual(parameterInfo.Output, true);
            Assert.AreEqual(parameterInfo.Required, true);
            Assert.AreEqual(parameterInfo.PropertyType, typeof(Int32));
        }

        /// <summary>
        /// Verify passing a empty type parameter results in the default type of String being registered
        /// </summary>
        [TestMethod]
        public void EmptyTypeOnParameter()
        {
            string output = bool.TrueString;
            string required = bool.TrueString;
            string type = "";

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"].PropertyType.Equals(typeof(String)));
        }

        /// <summary>
        /// Verify passing a null as a  type parameter results in the default type of String being registered
        /// </summary>
        [TestMethod]
        public void NullTypeOnParameter()
        {
            string output = bool.TrueString;
            string required = bool.TrueString;
            string type = null;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"].PropertyType.Equals(typeof(String)));
        }

        /// <summary>
        /// Verify when registering a randon type which is not allowed that we get an InvalidProjectFileException
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RandomTypeOnParameter()
        {
            string output = bool.TrueString;
            string required = bool.TrueString;
            string type = "ISomethingItem";

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify the following types work when registered as input parameters
        ///     ValueTypeArray
        ///     StringArray
        /// </summary>
        [TestMethod]
        public void GoodValueTypeArrayInputOnInputParameter()
        {
            // Note output is false so these are only input parameters
            string output = bool.FalseString;
            string required = bool.TrueString;

            string type = typeof(int[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(String[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(ITaskItem[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DateTime[]).FullName;
            VerifyTypeParameter(output, required, type);
        }

        /// <summary>
        /// Verify when a class (other than string or ITaskItem) is attempted to be registered as an input parameter we get an invalid project file exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void BadArrayInputOnInputParameter()
        {
            // Note output is false so these are only input parameters
            string output = bool.FalseString;
            string required = bool.TrueString;
            string type = typeof(ArrayList[]).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify that value types and (string and ITaskItem classes) can be registered as input parameters
        /// </summary>
        [TestMethod]
        public void GoodScalarTypeArrayInputOnInputParameter()
        {
            // Note output is false so these are only input parameters
            string output = bool.FalseString;
            string required = bool.TrueString;

            string type = typeof(int).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(String).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(ITaskItem).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DateTime).FullName;
            VerifyTypeParameter(output, required, type);
        }

        /// <summary>
        /// Verify when a class which derives from ITask is attempted to be registered that we get an InvalidProjectFileException. 
        /// We only support ITaskItems and not their derived types as input parameters.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void BadScalarInputOnInputParameterDerivedFromITask()
        {
            // Note output is false so these are only input parameters
            string output = bool.FalseString;
            string required = bool.TrueString;
            string type = type = typeof(DerivedFromITaskItem).FullName + "," + typeof(DerivedFromITaskItem).Assembly.FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify when a random scalar input class is attempted to be registered that we get an invalid proejct file exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void BadScalarInputOnInputParameter()
        {
            // Note output is false so these are only input parameters
            string output = bool.FalseString;
            string required = bool.TrueString;
            string type = typeof(ArrayList).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify the expected outparameters are supported
        ///     String
        ///     String[]
        ///     ValueType
        ///     ValueType[]
        ///     ItaskItem
        ///     ItaskItem[]
        ///     Types which are assignable to ITaskItem or ITaskItem[]
        /// </summary>
        [TestMethod]
        public void GoodOutPutParameters()
        {
            // Notice output is true
            string output = bool.TrueString;
            string required = bool.TrueString;

            string type = typeof(int).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(String).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(ITaskItem).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DerivedFromITaskItem).FullName + "," + typeof(DerivedFromITaskItem).Assembly.FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(ITaskItem[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DateTime).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(String[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DateTime[]).FullName;
            VerifyTypeParameter(output, required, type);

            type = typeof(DerivedFromITaskItem[]).FullName + "," + typeof(DerivedFromITaskItem).Assembly.FullName;
            VerifyTypeParameter(output, required, type);
        }

        /// <summary>
        /// Verify that an arbitrary output type class which is not derived from ITaskItem is not allowed
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void BadOutputParameter()
        {
            // Notice output is true
            string output = bool.TrueString;
            string required = bool.TrueString;
            string type = typeof(ArrayList).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify when the output parameter is not set that it defaults to false
        /// </summary>
        [TestMethod]
        public void EmptyOutput()
        {
            string output = "";
            string required = bool.TrueString;
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(((TaskPropertyInfo)registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"]).Output == false);
        }

        /// <summary>
        /// Verify when the output parameter is empty that it defaults to false
        /// </summary>
        [TestMethod]
        public void NullOutput()
        {
            string output = null;
            string required = bool.TrueString;
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(((TaskPropertyInfo)registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"]).Output == false);
        }

        /// <summary>
        /// Verify that a random string which is not a boolean causes an invalid project file exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RandomOutput()
        {
            string output = "RandomStuff";
            string required = bool.TrueString;
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify an empty required value results in a default value of false
        /// </summary>
        [TestMethod]
        public void EmptyRequired()
        {
            string output = bool.TrueString;
            string required = "";
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(((TaskPropertyInfo)registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"]).Required == false);
        }

        /// <summary>
        /// Verify a null required value results in a default value of false
        /// </summary>
        [TestMethod]
        public void NullRequired()
        {
            string output = bool.TrueString;
            string required = null;
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(((TaskPropertyInfo)registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"]).Required == false);
        }

        /// <summary>
        /// Verify a value which cannot be parsed to a boolean results in a invalidprojectfileexception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RandomRequired()
        {
            string output = bool.TrueString;
            string required = "RANDOM";
            string type = typeof(String).FullName;

            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify that expansion of the attributes works.
        /// </summary>
        [TestMethod]
        public void ExpandedGoodParameters()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";

            // Add empty parameterGroup
            UsingTaskParameterGroupElement parameterGroup = element.AddParameterGroup();
            ProjectUsingTaskParameterElement defaultParameter = parameterGroup.AddParameter("ParameterWithNoAttributes");

            ProjectUsingTaskParameterElement filledOutAttributesParameter = parameterGroup.AddParameter("ParameterWithAllAttributesHardCoded");
            filledOutAttributesParameter.Output = "$(TrueString)";
            filledOutAttributesParameter.Required = "@(ItemWithTrueItem)";
            filledOutAttributesParameter.ParameterType = "$(ITaskItem)";

            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            int registeredTaskCount = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.GetDeepCountOfRegisteredTasks(registry.TaskRegistrations);
            Assert.AreEqual(1, registeredTaskCount, "Expected three registered tasks in TaskRegistry.TaskRegistrations!");
            IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registeredTasks = registry.TaskRegistrations;

            ProjectUsingTaskElement taskElement = elementList[0];
            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)];
            Assert.IsNotNull(registeredTaskRecords, "Task to be found in TaskRegistry.TaskRegistrations!");
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

            TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord = registeredTaskRecords[0].ParameterGroupAndTaskBody;
            Assert.IsNotNull(inlineTaskRecord);
            Assert.IsNull(inlineTaskRecord.InlineTaskXmlBody);
            Assert.IsTrue(inlineTaskRecord.UsingTaskParameters.Count == 2);

            string expandedOutput = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(filledOutAttributesParameter.Output, ExpanderOptions.ExpandPropertiesAndItems, filledOutAttributesParameter.OutputLocation);
            string expandedRequired = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(filledOutAttributesParameter.Required, ExpanderOptions.ExpandPropertiesAndItems, filledOutAttributesParameter.RequiredLocation);
            string expandedType = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(filledOutAttributesParameter.ParameterType, ExpanderOptions.ExpandPropertiesAndItems, filledOutAttributesParameter.ParameterTypeLocation);

            TaskPropertyInfo parameterInfo = inlineTaskRecord.UsingTaskParameters[filledOutAttributesParameter.Name] as TaskPropertyInfo;
            Assert.IsNotNull(parameterInfo);
            Assert.AreEqual(parameterInfo.Name, filledOutAttributesParameter.Name);
            Assert.AreEqual(parameterInfo.Output, bool.Parse(expandedOutput));
            Assert.AreEqual(parameterInfo.Required, bool.Parse(expandedRequired));
            Assert.AreEqual(parameterInfo.PropertyType, Type.GetType(expandedType + "," + typeof(ITaskItem).Assembly.FullName, false /* don't throw on error */, true /* case-insensitive */));
        }
        #endregion

        #region TaskBodyTests

        /// <summary>
        /// Verify that expansion of the evaluate attribute.
        /// </summary>
        [TestMethod]
        public void ExpandedPropertyEvaluate()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";
            element.AddUsingTaskBody("$(FalseString)", String.Empty);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)];
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

            TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord = registeredTaskRecords[0].ParameterGroupAndTaskBody;
            Assert.IsNotNull(inlineTaskRecord);
            Assert.IsFalse(inlineTaskRecord.TaskBodyEvaluated);
        }

        /// <summary>
        /// Verify that expansion of the evaluate attribute.
        /// </summary>
        [TestMethod]
        public void ExpandedItemEvaluate()
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";
            element.AddUsingTaskBody("@(ItemWithTrueItem)", String.Empty);
            elementList.Add(element);

            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            List<TaskRegistry.RegisteredTaskRecord> registeredTaskRecords = registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)];
            Assert.AreEqual(1, registeredTaskRecords.Count, "Expected only one task registered under this TaskName!");

            TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord = registeredTaskRecords[0].ParameterGroupAndTaskBody;
            Assert.IsNotNull(inlineTaskRecord);
            Assert.IsTrue(inlineTaskRecord.TaskBodyEvaluated);
        }

        /// <summary>
        /// Verify when false is passed to evaluate value results in a false value being set
        /// </summary>
        [TestMethod]
        public void FalseEvaluateWithBody()
        {
            string body = "$(Property1)@(ThirdItem)$(Property2)";
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(bool.FalseString, body);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Make sure when evaluate is false the string passed in is not expanded
            Assert.IsFalse(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.TaskBodyEvaluated.Equals(body));
        }

        /// <summary>
        /// Verify when false is passed to evaluate value results in a false value being set
        /// </summary>
        [TestMethod]
        public void EvaluateWithBody()
        {
            string body = "$(Property1)@(ThirdItem)$(Property2)";
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(bool.TrueString, body);
            ProjectUsingTaskElement taskElement = elementList[0];
            ProjectUsingTaskBodyElement bodyElement = taskElement.TaskBody;

            string expandedBody = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.RegistryExpander.ExpandIntoStringAndUnescape(body, ExpanderOptions.ExpandPropertiesAndItems, bodyElement.Location);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            // Make sure when evaluate is false the string passed in is not expanded
            Assert.IsFalse(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.TaskBodyEvaluated.Equals(expandedBody));
        }

        /// <summary>
        /// Verify that a random string which is not a boolean causes an invalid project file exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RandomEvaluate()
        {
            string evaluate = "RandomStuff";
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(evaluate, "");
            TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.Fail();
        }

        /// <summary>
        /// Verify when false is passed to evaluate value results in a false value being set
        /// </summary>
        [TestMethod]
        public void FalseEvaluate()
        {
            string evaluate = bool.FalseString;
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(evaluate, "");
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsFalse(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.TaskBodyEvaluated);
        }

        /// <summary>
        /// Verify an empty evaluate value results in a default value of true
        /// </summary>
        [TestMethod]
        public void EmptyEvaluate()
        {
            string evaluate = "";
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(evaluate, "");
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.TaskBodyEvaluated);
        }

        /// <summary>
        /// Verify a null evaluate value results in a default value of true
        /// </summary>
        [TestMethod]
        public void NullEvaluate()
        {
            string evaluate = null;
            List<ProjectUsingTaskElement> elementList = CreateTaskBodyElementWithAttributes(evaluate, "");
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);
            Assert.IsTrue(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.TaskBodyEvaluated);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// With the given task registry, retrieve a copy of the test task with the given runtime and 
        /// architecture and verify: 
        /// - that it was retrieved (or not) as expected 
        /// - that it was retrieved from the cache (or not) as expected
        /// - that the record that was retrieved had the expected runtime and architecture 
        ///   values as its factory parameters. 
        /// </summary>
        private static void RetrieveAndValidateRegisteredTaskRecord
                                                        (
                                                            TaskRegistry registry,
                                                            bool exactMatchRequired,
                                                            Dictionary<string, string> taskParameters,
                                                            bool shouldBeRetrieved,
                                                            bool shouldBeRetrievedFromCache,
                                                            string expectedRuntime,
                                                            string expectedArchitecture
                                                        )
        {
            bool retrievedFromCache = false;
            var record = registry.GetTaskRegistrationRecord(TestTaskName, null, taskParameters, exactMatchRequired, s_targetLoggingContext, s_elementLocation, out retrievedFromCache);

            if (shouldBeRetrieved)
            {
                Assert.IsNotNull(record, "Should have retrieved a match.");

                if (expectedRuntime != null)
                {
                    Assert.AreEqual(expectedRuntime, record.TaskFactoryParameters[XMakeAttributes.runtime]);
                }

                if (expectedArchitecture != null)
                {
                    Assert.AreEqual(expectedArchitecture, record.TaskFactoryParameters[XMakeAttributes.architecture]);
                }
            }
            else
            {
                Assert.IsNull(record, "Should not have been a match.");
            }

            Assert.AreEqual(shouldBeRetrievedFromCache, retrievedFromCache);
        }

        /// <summary>
        /// With the given task registry, retrieve a copy of the test task with the given runtime and 
        /// architecture and verify: 
        /// - that it was retrieved (or not) as expected 
        /// - that it was retrieved from the cache (or not) as expected
        /// - that the record that was retrieved had the expected runtime and architecture 
        ///   values as its factory parameters. 
        /// </summary>
        private static void RetrieveAndValidateRegisteredTaskRecord
                                                        (
                                                            TaskRegistry registry,
                                                            bool exactMatchRequired,
                                                            string runtime,
                                                            string architecture,
                                                            bool shouldBeRetrieved,
                                                            bool shouldBeRetrievedFromCache,
                                                            string expectedRuntime,
                                                            string expectedArchitecture
                                                        )
        {
            Dictionary<string, string> parameters = null;
            if (runtime != null || architecture != null)
            {
                parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                parameters.Add(XMakeAttributes.runtime, runtime ?? XMakeAttributes.MSBuildRuntimeValues.any);
                parameters.Add(XMakeAttributes.architecture, architecture ?? XMakeAttributes.MSBuildArchitectureValues.any);
            }

            RetrieveAndValidateRegisteredTaskRecord(registry, exactMatchRequired, parameters, shouldBeRetrieved, shouldBeRetrievedFromCache, expectedRuntime, expectedArchitecture);
        }

        /// <summary>
        /// With the given task registry, retrieve a copy of the test task with the given runtime and 
        /// architecture and verify: 
        /// - that it was retrieved (or not) as expected 
        /// - that it was retrieved from the cache (or not) as expected
        /// </summary>
        private static void RetrieveAndValidateRegisteredTaskRecord(TaskRegistry registry, bool exactMatchRequired, Dictionary<string, string> taskParameters, bool shouldBeRetrieved, bool shouldBeRetrievedFromCache)
        {
            // if we're requiring an exact match, we can cheat and figure out what the expected runtime / architecture should be.  
            // if not, then if the user didn't pass us an expected runtime, we can't really check it, so just pass 
            // null (which will be treated as "don't validate"). 
            string expectedRuntime = null;
            string expectedArchitecture = null;
            if (exactMatchRequired)
            {
                taskParameters.TryGetValue(XMakeAttributes.runtime, out expectedRuntime);
                taskParameters.TryGetValue(XMakeAttributes.architecture, out expectedArchitecture);
            }

            RetrieveAndValidateRegisteredTaskRecord(registry, exactMatchRequired, taskParameters, shouldBeRetrieved, shouldBeRetrievedFromCache, expectedRuntime, expectedArchitecture);
        }

        /// <summary>
        /// With the given task registry, retrieve a copy of the test task with the given runtime and 
        /// architecture and verify: 
        /// - that it was retrieved (or not) as expected 
        /// - that it was retrieved from the cache (or not) as expected
        /// </summary>
        private static void RetrieveAndValidateRegisteredTaskRecord(TaskRegistry registry, bool exactMatchRequired, string runtime, string architecture, bool shouldBeRetrieved, bool shouldBeRetrievedFromCache)
        {
            // if we're requiring an exact match, we can cheat and figure out what the expected runtime / architecture should be.  
            // if not, then if the user didn't pass us an expected runtime, we can't really check it, so just pass 
            // null (which will be treated as "don't validate"). 
            string expectedRuntime = exactMatchRequired ? runtime : null;
            string expectedArchitecture = exactMatchRequired ? architecture : null;

            RetrieveAndValidateRegisteredTaskRecord(registry, exactMatchRequired, runtime, architecture, shouldBeRetrieved, shouldBeRetrievedFromCache, expectedRuntime, expectedArchitecture);
        }

        /// <summary>
        /// Make sure the type passed in is the same type which is parsed out.
        /// </summary>
        private static void VerifyTypeParameter(string output, string required, string type)
        {
            List<ProjectUsingTaskElement> elementList = CreateParameterElementWithAttributes(output, required, type);
            TaskRegistry registry = TaskRegistryHelperMethods<ProjectPropertyInstance, ProjectItemInstance>.CreateTaskRegistryAndRegisterTasks(elementList);

            Type paramType = Type.GetType(type);

            // The type may be in the Microsoft.Build.Framework Assembly
            if (paramType == null)
            {
                paramType = Type.GetType(type + "," + typeof(ITaskItem).Assembly.FullName, false /* don't throw on error */, true /* case-insensitive */);
            }

            Assert.IsTrue(registry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("Name", null)][0].ParameterGroupAndTaskBody.UsingTaskParameters["ParameterWithAllAttributesHardCoded"].PropertyType.Equals(paramType));
        }

        /// <summary>
        /// Create a parameter element with the passed in attributes, this method will help with testing.
        /// </summary>
        private static List<ProjectUsingTaskElement> CreateParameterElementWithAttributes(string output, string required, string type)
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";

            // Add empty parameterGroup
            UsingTaskParameterGroupElement parameterGroup = element.AddParameterGroup();
            ProjectUsingTaskParameterElement filledOutAttributesParameter = parameterGroup.AddParameter("ParameterWithAllAttributesHardCoded", output, required, type);
            elementList.Add(element);
            return elementList;
        }

        /// <summary>
        /// Create a task body element with the passed in attributes, this method will help with testing.
        /// </summary>
        private static List<ProjectUsingTaskElement> CreateTaskBodyElementWithAttributes(string evaluate, string body)
        {
            List<ProjectUsingTaskElement> elementList = new List<ProjectUsingTaskElement>();
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectUsingTaskElement element = project.AddUsingTask("Name", "File", null);
            element.TaskFactory = "SuperFactory";
            element.AddUsingTaskBody(evaluate, body);
            elementList.Add(element);
            return elementList;
        }

        /// <summary>
        /// Generates a test task assembly containing a single task named "TestTask"
        /// and returns the path to that assembly.  
        /// </summary>
        private static string GetTestTaskAssemblyLocation()
        {
            string codeFile = null;
            string outputFile = Path.Combine(Path.GetTempPath(), "TaskRegistryTests_TestTask.dll");
            string codeContent = @"
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace TestTask
{
    public class TestTask : Task
    {
        public override bool Execute()
        {
            return true;
        }
    }
}";

            File.Delete(outputFile);
            bool succeeded = true;

            try
            {
                codeFile = FileUtilities.GetTemporaryFile();
                File.WriteAllText(codeFile, codeContent);
                Csc csc = new Csc();
                csc.BuildEngine = new MockEngine();
                csc.Sources = new ITaskItem[] { new TaskItem(codeFile) };
                csc.OutputAssembly = new TaskItem(outputFile);
                csc.References = new ITaskItem[] { new TaskItem("Microsoft.Build.Framework.dll"), new TaskItem("Microsoft.Build.Utilities.Core.dll") };
                csc.Platform = "AnyCPU";
                csc.TargetType = "Library";
                csc.Prefer32Bit = false;
                succeeded = csc.Execute();
            }
            catch (Exception)
            {
                if (File.Exists(outputFile))
                {
                    FileUtilities.DeleteNoThrow(outputFile);
                }

                // now rethrow 
            }
            finally
            {
                File.Delete(codeFile);
            }

            if (succeeded)
            {
                return outputFile;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Create a custom class derived from ITaskItem to test input and output parameters work using this item.
        /// </summary>
        internal class DerivedFromITaskItem : ITaskItem
        {
            /// <summary>
            /// The ItemSpec of the item
            /// </summary>
            public string ItemSpec
            {
                get;
                set;
            }

            /// <summary>
            /// Collection of metadataNames on the item
            /// </summary>
            public ICollection MetadataNames
            {
                get { throw new NotImplementedException(); }
            }

            /// <summary>
            /// Number of metadata items on the item
            /// </summary>
            public int MetadataCount
            {
                get { throw new NotImplementedException(); }
            }

            /// <summary>
            /// Get the meatadata on the item based on the metadataName
            /// </summary>
            public string GetMetadata(string metadataName)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Set some metadata on the item
            /// </summary>
            public void SetMetadata(string metadataName, string metadataValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Remove some metadata from the item
            /// </summary>
            public void RemoveMetadata(string metadataName)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Copy the metadata from this item to another item.
            /// </summary>
            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Clone the custom metadata from this item
            /// </summary>
            public IDictionary CloneCustomMetadata()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Helper class to assist in using the task registry
        /// </summary>
        /// <typeparam name="P">ProjectPropertyInstance</typeparam>
        /// <typeparam name="I">ProjectItemInstance</typeparam>
        internal class TaskRegistryHelperMethods<P, I>
            where P : class, IProperty
            where I : class, IItem
        {
            /// <summary>
            /// Expander to expand the registry entires
            /// </summary>
            private static Expander<ProjectPropertyInstance, ProjectItemInstance> s_registryExpander;

            /// <summary>
            /// Accessor to the expander
            /// </summary>
            internal static Expander<ProjectPropertyInstance, ProjectItemInstance> RegistryExpander
            {
                get
                {
                    if (s_registryExpander == null)
                    {
                        s_registryExpander = GetExpander();
                    }

                    return s_registryExpander;
                }
            }

            /// <summary>
            /// Count the number of registry records which exist in the task registry
            /// </summary>
            internal static int GetDeepCountOfRegisteredTasks(IDictionary<TaskRegistry.RegisteredTaskIdentity, List<TaskRegistry.RegisteredTaskRecord>> registryRecords)
            {
                if (registryRecords == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (List<TaskRegistry.RegisteredTaskRecord> recordList in registryRecords.Values)
                {
                    count += recordList.Count;
                }

                return count;
            }

            /// <summary>
            /// Create and fill a task registry based on some using task elements.
            /// </summary>
            internal static TaskRegistry CreateTaskRegistryAndRegisterTasks(List<ProjectUsingTaskElement> usingTaskElements)
            {
                TaskRegistry registry = new TaskRegistry(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache);

                foreach (ProjectUsingTaskElement projectUsingTaskElement in usingTaskElements)
                {
                    TaskRegistry.RegisterTasksFromUsingTaskElement<ProjectPropertyInstance, ProjectItemInstance>
                        (
                         s_loggingService,
                         s_loggerContext,
                         Directory.GetCurrentDirectory(),
                         projectUsingTaskElement,
                         registry,
                         RegistryExpander,
                         ExpanderOptions.ExpandPropertiesAndItems
                         );
                }

                return registry;
            }

            /// <summary>
            /// Create an expander with some property values which can be used for testing.
            /// </summary>
            internal static Expander<ProjectPropertyInstance, ProjectItemInstance> GetExpander()
            {
                ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
                PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
                for (int i = 1; i < 6; i++)
                {
                    pg.Set(ProjectPropertyInstance.Create("Property" + i, "Value" + i));
                }

                pg.Set(ProjectPropertyInstance.Create("TrueString", "True"));
                pg.Set(ProjectPropertyInstance.Create("FalseString", "False"));
                pg.Set(ProjectPropertyInstance.Create("ItaskItem", "Microsoft.Build.Framework.ItaskItem[]"));

                List<ProjectItemInstance> intermediateAssemblyItemGroup = new List<ProjectItemInstance>();
                ProjectItemInstance iag = new ProjectItemInstance(project, "IntermediateAssembly", @"subdir1\engine.dll", project.FullPath);
                intermediateAssemblyItemGroup.Add(iag);
                iag.SetMetadata("aaa", "111");

                iag = new ProjectItemInstance(project, "IntermediateAssembly", @"subdir2\tasks.dll", project.FullPath);
                intermediateAssemblyItemGroup.Add(iag);
                iag.SetMetadata("bbb", "222");

                List<ProjectItemInstance> firstItemGroup = new List<ProjectItemInstance>();
                for (int i = 0; i < 3; i++)
                {
                    ProjectItemInstance fig = new ProjectItemInstance(project, "FirstItem" + i, "FirstValue" + i, project.FullPath);
                    firstItemGroup.Add(fig);
                }

                List<ProjectItemInstance> secondItemGroup = new List<ProjectItemInstance>();
                for (int i = 0; i < 3; i++)
                {
                    ProjectItemInstance sig = new ProjectItemInstance(project, "SecondItem" + i, "SecondValue" + i, project.FullPath);
                    secondItemGroup.Add(sig);
                }

                List<ProjectItemInstance> thirdItemGroup = new List<ProjectItemInstance>();
                ProjectItemInstance tig = new ProjectItemInstance(project, "ThirdItem", "ThirdValue1", project.FullPath);
                thirdItemGroup.Add(tig);

                List<ProjectItemInstance> trueItemGroup = new List<ProjectItemInstance>();
                ProjectItemInstance trig = new ProjectItemInstance(project, "ItemWithTrueItem", "true", project.FullPath);
                trueItemGroup.Add(trig);

                ItemDictionary<ProjectItemInstance> secondaryItemsByName = new ItemDictionary<ProjectItemInstance>();
                secondaryItemsByName.ImportItems(intermediateAssemblyItemGroup);
                secondaryItemsByName.ImportItems(firstItemGroup);
                secondaryItemsByName.ImportItems(secondItemGroup);
                secondaryItemsByName.ImportItems(thirdItemGroup);
                secondaryItemsByName.ImportItems(trueItemGroup);

                Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(pg, secondaryItemsByName);
                return expander;
            }
        }
        #endregion
    }
}
