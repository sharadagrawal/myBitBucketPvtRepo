﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class XMakeAppTests
    {
        [TestMethod]
        public void GatherCommandLineSwitchesTwoProperties()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/p:a=b", "/p:c=d" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Property];
            Assert.AreEqual("a=b", parameters[0]);
            Assert.AreEqual("c=d", parameters[1]);
        }

        [TestMethod]
        public void GatherCommandLineSwitchesMaxCpuCountWithArgument()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:2" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.AreEqual("2", parameters[0]);
            Assert.AreEqual(1, parameters.Length);

            Assert.AreEqual(false, switches.HaveErrors());
        }

        [TestMethod]
        public void GatherCommandLineSwitchesMaxCpuCountWithoutArgument()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:3", "/m" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.AreEqual(Convert.ToString(Environment.ProcessorCount), parameters[1]);
            Assert.AreEqual(2, parameters.Length);

            Assert.AreEqual(false, switches.HaveErrors());
        }

        /// <summary>
        ///  /m: should be an error, unlike /m:1 and /m
        /// </summary>
        [TestMethod]
        public void GatherCommandLineSwitchesMaxCpuCountWithoutArgumentButWithColon()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.AreEqual(0, parameters.Length);

            Assert.IsTrue(switches.HaveErrors());
        }

        /*
         * Quoting Rules:
         * 
         * A string is considered quoted if it is enclosed in double-quotes. A double-quote can be escaped with a backslash, or it
         * is automatically escaped if it is the last character in an explicitly terminated quoted string. A backslash itself can
         * be escaped with another backslash IFF it precedes a double-quote, otherwise it is interpreted literally.
         * 
         * e.g.
         *      abc"cde"xyz         --> "cde" is quoted
         *      abc"xyz             --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc"xyz"            --> "xyz" is quoted (the terminal double-quote is explicit)
         * 
         *      abc\"cde"xyz        --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc\\"cde"xyz       --> "cde" is quoted
         *      abc\\\"cde"xyz      --> "xyz" is quoted (the terminal double-quote is assumed)
         * 
         *      abc"""xyz           --> """ is quoted
         *      abc""""xyz          --> """ and "xyz" are quoted (the terminal double-quote is assumed)
         *      abc"""""xyz         --> """ is quoted
         *      abc""""""xyz        --> """ and """ are quoted
         *      abc"cde""xyz        --> "cde"" is quoted
         *      abc"xyz""           --> "xyz"" is quoted (the terminal double-quote is explicit)
         * 
         *      abc""xyz            --> nothing is quoted
         *      abc""cde""xyz       --> nothing is quoted
         */

        [TestMethod]
        public void SplitUnquotedTest()
        {
            ArrayList sa;
            int emptySplits;

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcdxyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abcdxyz", sa[0]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcc dxyz");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abcc", sa[0]);
            Assert.AreEqual("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcc;dxyz", ';');
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abcc", sa[0]);
            Assert.AreEqual("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,c;dxyz", ';', ',');
            Assert.AreEqual(3, sa.Count);
            Assert.AreEqual("abc", sa[0]);
            Assert.AreEqual("c", sa[1]);
            Assert.AreEqual("dxyz", sa[2]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,c;dxyz", 2, false, false, out emptySplits, ';', ',');
            Assert.AreEqual(0, emptySplits);
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc", sa[0]);
            Assert.AreEqual("c;dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,,;dxyz", int.MaxValue, false, false, out emptySplits, ';', ',');
            Assert.AreEqual(2, emptySplits);
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc", sa[0]);
            Assert.AreEqual("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,,;dxyz", int.MaxValue, true, false, out emptySplits, ';', ',');
            Assert.AreEqual(0, emptySplits);
            Assert.AreEqual(4, sa.Count);
            Assert.AreEqual("abc", sa[0]);
            Assert.AreEqual(String.Empty, sa[1]);
            Assert.AreEqual(String.Empty, sa[2]);
            Assert.AreEqual("dxyz", sa[3]);

            // "c d" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c d\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"c d\"xyz", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\"x z");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"x z", sa[0]);

            // "x z" is quoted (the terminal double-quote is explicit)
            sa = QuotingUtilities.SplitUnquoted("abc\"x z\"");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"x z\"", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\\\"cde\"x z");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\\\"cde\"x z", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            // "c e" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\"c e\"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\\\"c", sa[0]);
            Assert.AreEqual("e\"x z", sa[1]);

            // "c e" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\"c e\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\\\\\"c e\"xyz", sa[0]);

            // "c e" is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\"c e\"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\\\\\"c e\"x", sa[0]);
            Assert.AreEqual("z", sa[1]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\\\"cde\"x z");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\\\\\\\"cde\"x z", sa[0]);

            // "xyz" is quoted (the terminal double-quote is assumed)
            // "c e" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\\\"c e\"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\\\\\\\"c", sa[0]);
            Assert.AreEqual("e\"x z", sa[1]);

            // """ is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"\"\"xyz", sa[0]);

            // " "" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\" \"\"xyz", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\"\" \"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\"\"", sa[0]);
            Assert.AreEqual("\"x z", sa[1]);

            // " "" and "xyz" are quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\"x z");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\" \"\"\"x z", sa[0]);

            // """ is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"\"\"\"\"xyz", sa[0]);

            // """ is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\"\"\"\"\"x", sa[0]);
            Assert.AreEqual("z", sa[1]);

            // " "" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\" \"\"\"\"xyz", sa[0]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"\"\"\"\"\"xyz", sa[0]);

            // " "" and " "" are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\" \"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\" \"\"\" \"\"xyz", sa[0]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\" \"\"\"xyz");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\"\"\"", sa[0]);
            Assert.AreEqual("\"\"\"xyz", sa[1]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\" \"\"\"x z");
            Assert.AreEqual(3, sa.Count);
            Assert.AreEqual("abc\"\"\"", sa[0]);
            Assert.AreEqual("\"\"\"x", sa[1]);
            Assert.AreEqual("z", sa[2]);

            // "c e"" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c e\"\"xyz");
            Assert.AreEqual(1, sa.Count);
            Assert.AreEqual("abc\"c e\"\"xyz", sa[0]);

            // "c e"" is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c e\"\"x z");
            Assert.AreEqual(2, sa.Count);
            Assert.AreEqual("abc\"c e\"\"x", sa[0]);
            Assert.AreEqual("z", sa[1]);

            // nothing is quoted
            sa = QuotingUtilities.SplitUnquoted("a c\"\"x z");
            Assert.AreEqual(3, sa.Count);
            Assert.AreEqual("a", sa[0]);
            Assert.AreEqual("c\"\"x", sa[1]);
            Assert.AreEqual("z", sa[2]);

            // nothing is quoted
            sa = QuotingUtilities.SplitUnquoted("a c\"\"c e\"\"x z");
            Assert.AreEqual(4, sa.Count);
            Assert.AreEqual("a", sa[0]);
            Assert.AreEqual("c\"\"c", sa[1]);
            Assert.AreEqual("e\"\"x", sa[2]);
            Assert.AreEqual("z", sa[3]);
        }

        [TestMethod]
        public void UnquoteTest()
        {
            int doubleQuotesRemoved;

            // "cde" is quoted
            Assert.AreEqual("abccdexyz", QuotingUtilities.Unquote("abc\"cde\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.AreEqual("abcxyz", QuotingUtilities.Unquote("abc\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(1, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is explicit)
            Assert.AreEqual("abcxyz", QuotingUtilities.Unquote("abc\"xyz\"", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.AreEqual("abc\"cdexyz", QuotingUtilities.Unquote("abc\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(1, doubleQuotesRemoved);

            // "cde" is quoted
            Assert.AreEqual("abc\\cdexyz", QuotingUtilities.Unquote("abc\\\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.AreEqual("abc\\\"cdexyz", QuotingUtilities.Unquote("abc\\\\\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(1, doubleQuotesRemoved);

            // """ is quoted
            Assert.AreEqual("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // """ and "xyz" are quoted (the terminal double-quote is assumed)
            Assert.AreEqual("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(3, doubleQuotesRemoved);

            // """ is quoted
            Assert.AreEqual("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(4, doubleQuotesRemoved);

            // """ and """ are quoted
            Assert.AreEqual("abc\"\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(4, doubleQuotesRemoved);

            // "cde"" is quoted
            Assert.AreEqual("abccde\"xyz", QuotingUtilities.Unquote("abc\"cde\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // "xyz"" is quoted (the terminal double-quote is explicit)
            Assert.AreEqual("abcxyz\"", QuotingUtilities.Unquote("abc\"xyz\"\"", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // nothing is quoted
            Assert.AreEqual("abcxyz", QuotingUtilities.Unquote("abc\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(2, doubleQuotesRemoved);

            // nothing is quoted
            Assert.AreEqual("abccdexyz", QuotingUtilities.Unquote("abc\"\"cde\"\"xyz", out doubleQuotesRemoved));
            Assert.AreEqual(4, doubleQuotesRemoved);
        }

        [TestMethod]
        public void ExtractSwitchParametersTest()
        {
            string commandLineArg = "\"/p:foo=\"bar";
            int doubleQuotesRemovedFromArg;
            string unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":\"foo=\"bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(2, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(2, doubleQuotesRemovedFromArg);

            commandLineArg = "/p:foo=bar";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(0, doubleQuotesRemovedFromArg);

            commandLineArg = "\"\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(3, doubleQuotesRemovedFromArg);

            // this test is totally unreal -- we'd never attempt to extract switch parameters if the leading character is not a
            // switch indicator (either '-' or '/') -- here the leading character is a double-quote
            commandLineArg = "\"\"\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "/p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(3, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/pr\"operty\":foo=bar";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "property", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(3, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/pr\"op\"\"erty\":foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "property", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(6, doubleQuotesRemovedFromArg);

            commandLineArg = "/p:\"foo foo\"=\"bar bar\";\"baz=onga\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.AreEqual(":\"foo foo\"=\"bar bar\";\"baz=onga\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.AreEqual(6, doubleQuotesRemovedFromArg);
        }

        [TestMethod]
        public void Help()
        {
            Assert.AreEqual(MSBuildApp.ExitType.Success,
                MSBuildApp.Execute(@"c:\bin\msbuild.exe -? "));
        }

        [TestMethod]
        public void ErrorCommandLine()
        {
            Assert.AreEqual(MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(@"c:\bin\msbuild.exe -junk"));

            Assert.AreEqual(MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(@"msbuild.exe -t"));

            Assert.AreEqual(MSBuildApp.ExitType.InitializationError,
                MSBuildApp.Execute(@"msbuild.exe @bogus.rsp"));
        }

        [TestMethod]
        public void ValidVerbosities()
        {
            Assert.AreEqual(LoggerVerbosity.Quiet, MSBuildApp.ProcessVerbositySwitch("Q"));
            Assert.AreEqual(LoggerVerbosity.Quiet, MSBuildApp.ProcessVerbositySwitch("quiet"));
            Assert.AreEqual(LoggerVerbosity.Minimal, MSBuildApp.ProcessVerbositySwitch("m"));
            Assert.AreEqual(LoggerVerbosity.Minimal, MSBuildApp.ProcessVerbositySwitch("minimal"));
            Assert.AreEqual(LoggerVerbosity.Normal, MSBuildApp.ProcessVerbositySwitch("N"));
            Assert.AreEqual(LoggerVerbosity.Normal, MSBuildApp.ProcessVerbositySwitch("normal"));
            Assert.AreEqual(LoggerVerbosity.Detailed, MSBuildApp.ProcessVerbositySwitch("d"));
            Assert.AreEqual(LoggerVerbosity.Detailed, MSBuildApp.ProcessVerbositySwitch("detailed"));
            Assert.AreEqual(LoggerVerbosity.Diagnostic, MSBuildApp.ProcessVerbositySwitch("diag"));
            Assert.AreEqual(LoggerVerbosity.Diagnostic, MSBuildApp.ProcessVerbositySwitch("DIAGNOSTIC"));
        }

        [TestMethod]
        [ExpectedException(typeof(CommandLineSwitchException))]
        public void InvalidVerbosity()
        {
            MSBuildApp.ProcessVerbositySwitch("loquacious");
        }

        [TestMethod]
        public void ValidMaxCPUCountSwitch()
        {
            Assert.AreEqual(1, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "1" }));
            Assert.AreEqual(2, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "2" }));
            Assert.AreEqual(3, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "3" }));
            Assert.AreEqual(4, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "4" }));
            Assert.AreEqual(8, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "8" }));
            Assert.AreEqual(63, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "63" }));

            // Should pick last value
            Assert.AreEqual(4, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "8", "4" }));
        }

        [TestMethod]
        [ExpectedException(typeof(CommandLineSwitchException))]
        public void InvalidMaxCPUCountSwitch1()
        {
            MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "-1" });
        }

        [TestMethod]
        [ExpectedException(typeof(CommandLineSwitchException))]
        public void InvalidMaxCPUCountSwitch2()
        {
            MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "0" });
        }

        [TestMethod]
        [ExpectedException(typeof(CommandLineSwitchException))]
        public void InvalidMaxCPUCountSwitch3()
        {
            // Too big
            MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "foo" });
        }

        [TestMethod]
        [ExpectedException(typeof(CommandLineSwitchException))]
        public void InvalidMaxCPUCountSwitch4()
        {
            MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "1025" });
        }

        /// <summary>
        /// Regression test for bug where the MSBuild.exe command-line app
        /// would sometimes set the UI culture to just "en" which is considered a "neutral" UI 
        /// culture, which didn't allow for certain kinds of string formatting/parsing.
        /// </summary>
        /// <remarks>
        /// fr-FR, de-DE, and fr-CA are guaranteed to be available on all BVTs, so we must use one of these
        /// </remarks>
        [TestMethod]
        public void SetConsoleUICulture()
        {
            Thread thisThread = Thread.CurrentThread;

            // Save the current UI culture, so we can restore it at the end of this unit test.
            CultureInfo originalUICulture = thisThread.CurrentUICulture;

            thisThread.CurrentUICulture = new CultureInfo("fr-FR");
            MSBuildApp.SetConsoleUI();

            // Make sure this doesn't throw an exception.
            string bar = String.Format(CultureInfo.CurrentUICulture, "{0}", (int)1);

            // Restore the current UI culture back to the way it was at the beginning of this unit test.
            thisThread.CurrentUICulture = originalUICulture;
        }

        /// <summary>
        /// Invalid configuration file should not dump stack.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void ConfigurationInvalid()
        {
            string startDirectory = null;
            string newPathToMSBuildExe = null;
            string newPathToMSBuildPdb = null;
            string pathToConfigFile = null;
            string pathToProjectFile = null;
            string output = null;
            string oldValueForMSBuildOldOM = null;

            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                Random rand = new Random();
                startDirectory = Path.Combine(Path.GetTempPath(), Convert.ToString(rand.NextDouble()));
                Directory.CreateDirectory(startDirectory);

                string pathToMSBuildExe = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
                newPathToMSBuildExe = Path.Combine(startDirectory, "msbuild.exe");
                File.Copy(pathToMSBuildExe, newPathToMSBuildExe);

                string pathToMSBuildPdb = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.pdb", ToolLocationHelper.CurrentToolsVersion);
                newPathToMSBuildPdb = Path.Combine(startDirectory, "msbuild.pdb");
                if (File.Exists(pathToMSBuildPdb))
                {
                    File.Copy(pathToMSBuildPdb, newPathToMSBuildPdb);
                }

                pathToConfigFile = Path.Combine(startDirectory, "msbuild.exe.config");
                string configContent = @"<?xml version =""1.0""?>
                                            <configuration>
                                                <configSections>
                                                    <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
                                                    <foo/>
                                                </configSections>
                                                <startup>
                                                    <supportedRuntime version=""v4.0""/>
                                                </startup>
                                                <foo/>
                                                <msbuildToolsets default=""X"">
                                                <foo/>
                                                    <toolset toolsVersion=""X""> 
                                                        <foo/>
                                                    <property name=""MSBuildBinPath"" value=""Y""/>
                                                    <foo/>
                                                    </toolset>
                                                <foo/>
                                                </msbuildToolsets>
                                                <foo/>
                                            </configuration>";
                File.WriteAllText(pathToConfigFile, configContent);

                pathToProjectFile = Path.Combine(startDirectory, "foo.proj");
                string projectString =
                   "<?xml version='1.0' encoding='utf-8'?>" +
                    "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='X'>" +
                    "<Target Name='t'></Target>" +
                    "</Project>";
                File.WriteAllText(pathToProjectFile, projectString);

                output = RunProcessAndGetOutput(newPathToMSBuildExe, "\"" + pathToProjectFile + "\"", expectSuccess: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
            finally
            {
                if (output != null)
                {
                    Console.WriteLine(output);
                }

                try
                {
                    // Process does not let go its lock on the exe file until about 1 millisecond after 
                    // p.WaitForExit() returns. Do I know why? No I don't.
                    RobustDelete(pathToConfigFile);
                    RobustDelete(newPathToMSBuildExe);
                    RobustDelete(newPathToMSBuildPdb);
                    RobustDelete(pathToProjectFile);
                    RobustDelete(startDirectory);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
                }
            }

            // If there's a space in the %TEMP% path, the config file is read in the static constructor by the URI class and we catch there;
            // if there's not, we will catch when we try to read the toolsets. Either is fine; we just want to not crash.
            Assert.IsTrue(output.Contains("MSB1043") || output.Contains("MSB4136"));
        }

        /// <summary>
        /// Try hard to delete a file or directory specified
        /// </summary>
        private void RobustDelete(string path)
        {
            if (path != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true /*and files*/);
                        }
                        else if (File.Exists(path))
                        {
                            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); // make writeable
                            File.Delete(path);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Thread.Sleep(10);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Run the process and get stdout and stderr
        /// </summary>
        private string RunProcessAndGetOutput(string process, string parameters, bool expectSuccess = true)
        {
            ProcessStartInfo psi = new ProcessStartInfo(process);
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.Arguments = parameters;
            string output = String.Empty;
            int exitCode = 1;
            Process p = new Process();
            p.EnableRaisingEvents = true;
            p.StartInfo = psi;

            p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
            {
                if (args != null)
                {
                    output += args.Data + "\r\n";
                }
            };

            p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
            {
                if (args != null)
                {
                    output += args.Data + "\r\n";
                }
            };

            Console.WriteLine("Executing [{0} {1}]", process, parameters);

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.StandardInput.Close();
            p.WaitForExit();

            exitCode = p.ExitCode;
            p.Close();

            Console.WriteLine("==== OUTPUT ====");
            Console.WriteLine(output);
            Console.WriteLine("==============");

            Assert.AreEqual(expectSuccess, (exitCode == 0));

            return output;
        }

        /// <summary>
        /// Tests that the environment gets passed on to the node during build.
        /// </summary>
        [TestMethod]
        public void TestEnvironment()
        {
            string projectString = ObjectModelHelpers.CleanupFileContents(
                   @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                    <Target Name=""t""><Error Text='Error' Condition=""'$(MyEnvVariable)' == ''""/></Target>
                    </Project>");
            string tempdir = Path.GetTempPath();
            string projectFileName = tempdir + "\\msbEnvironmenttest.proj";
            string quotedProjectFileName = "\"" + projectFileName + "\"";

            try
            {
                Environment.SetEnvironmentVariable("MyEnvVariable", "1");
                using (StreamWriter sw = new StreamWriter(projectFileName))
                {
                    sw.WriteLine(projectString);
                }
                //Should pass
                Assert.AreEqual(MSBuildApp.ExitType.Success, MSBuildApp.Execute(@"c:\bin\msbuild.exe " + quotedProjectFileName));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MyEnvVariable", null);
                File.Delete(projectFileName);
            }
        }

        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void MSBuildEngineLogger()
        {
            string projectString =
                   "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                    "<Target Name=\"t\"><Message Text=\"Hello\"/></Target>" +
                    "</Project>";
            string tempdir = Path.GetTempPath();
            string projectFileName = tempdir + "\\msbLoggertest.proj";
            string quotedProjectFileName = "\"" + projectFileName + "\"";

            try
            {
                using (StreamWriter sw = new StreamWriter(projectFileName))
                {
                    sw.WriteLine(projectString);
                }
                //Should pass
                Assert.AreEqual(MSBuildApp.ExitType.Success,
                    MSBuildApp.Execute(@"c:\bin\msbuild.exe /logger:FileLogger,""Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" " + quotedProjectFileName));

                //Should fail as we are not changing existing lines
                Assert.AreEqual(MSBuildApp.ExitType.InitializationError,
                        MSBuildApp.Execute(@"c:\bin\msbuild.exe /logger:FileLogger,Microsoft.Build,Version=11111 " + quotedProjectFileName));
            }
            finally
            {
                File.Delete(projectFileName);
            }
        }

        private string _pathToMSBuildExe = Path.Combine(Environment.CurrentDirectory, "MSBuild.exe");
        private string _pathToArbitraryBogusFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"); // OK on 64 bit as well

        /// <summary>
        /// Basic case
        /// </summary>
        [TestMethod]
        public void GetCommandLine()
        {
            string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + _pathToArbitraryBogusFile + "\"" + " /v:diag", expectSuccess: false);

            Assert.IsTrue(output.Contains(_pathToMSBuildExe + " /v:diag " + _pathToArbitraryBogusFile));
        }

        /// <summary>
        /// Quoted path
        /// </summary>
        [TestMethod]
        public void GetCommandLineQuotedExe()
        {
            string quotedPathToMSBuildExe = "\"" + _pathToMSBuildExe + "\"";
            string output = RunProcessAndGetOutput(quotedPathToMSBuildExe, "\"" + _pathToArbitraryBogusFile + "\"" + " /v:diag", expectSuccess: false);

            Assert.IsTrue(output.Contains(_pathToMSBuildExe + " /v:diag " + _pathToArbitraryBogusFile));
        }

        /// <summary>
        /// On path
        /// </summary>
        [TestMethod]
        public void GetCommandLineQuotedExeOnPath()
        {
            string output = null;
            string current = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(_pathToMSBuildExe);

                output = RunProcessAndGetOutput("msbuild.exe", "\"" + _pathToArbitraryBogusFile + "\"" + " /v:diag", expectSuccess: false);
            }
            finally
            {
                Environment.CurrentDirectory = current;
            }

            Assert.IsTrue(output.IndexOf(_pathToMSBuildExe + " /v:diag " + _pathToArbitraryBogusFile, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Changes to the current directory interfere with the toolset reader.
        public void ResponseFileInProjectDirectoryFoundImplicitly()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            string currentDirectory = Environment.CurrentDirectory;

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                // Find the project in the current directory
                Directory.SetCurrentDirectory(directory);
                string output = RunProcessAndGetOutput(_pathToMSBuildExe, String.Empty);
                Assert.IsTrue(output.Contains("[A=1]"));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files.
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryExplicit()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\"");
                Assert.IsTrue(output.Contains("[A=1]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and not any random .rsp
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryRandomName()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "foo.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\"");
                Assert.IsTrue(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but lower precedence than the actual command line
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryCommandLineWins()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\"" + " /p:A=2");
                Assert.IsTrue(output.Contains("[A=2]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but lower precedence than the actual command line and higher than the msbuild.rsp next to msbuild.exe
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void ResponseFileInProjectDirectoryWinsOverMainMSBuildRsp()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            string exeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string exePath = Path.Combine(exeDirectory, "msbuild.exe");
            string mainRspPath = Path.Combine(exeDirectory, "msbuild.rsp");

            try
            {
                Directory.CreateDirectory(directory);
                Directory.CreateDirectory(exeDirectory);

                File.Copy(_pathToMSBuildExe, exePath);

                File.WriteAllText(mainRspPath, "/p:A=0");

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                File.WriteAllText(rspPath, "/p:A=1");

                string output = RunProcessAndGetOutput(exePath, "\"" + projectPath + "\"");
                Assert.IsTrue(output.Contains("[A=1]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);

                File.Delete(exePath);
                File.Delete(mainRspPath);
                Directory.Delete(exeDirectory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but not if it's the same as the msbuild.exe directory
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void ProjectDirectoryIsMSBuildExeDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");
            string exePath = Path.Combine(directory, "msbuild.exe");

            try
            {
                Directory.CreateDirectory(directory);

                File.Copy(_pathToMSBuildExe, exePath);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                File.WriteAllText(rspPath, "/p:A=1");

                string output = RunProcessAndGetOutput(exePath, "\"" + projectPath + "\"");
                Assert.IsTrue(output.Contains("[A=1]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                File.Delete(exePath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution with /noautoresponse in, is an error
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryItselfWithNoAutoResponseSwitch()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1 /noautoresponse";
                File.WriteAllText(rspPath, rspContent);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\"", expectSuccess: false);
                Assert.IsTrue(output.Contains("MSB1027")); // msbuild.rsp cannot have /noautoresponse in it
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be ignored if cmd line has /noautoresponse
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryButCommandLineNoAutoResponseSwitch()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "msbuild.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1 /noautoresponse";
                File.WriteAllText(rspPath, rspContent);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\" /noautoresponse", expectSuccess: true);
                Assert.IsTrue(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files. Sanity test when there isn't one.
        /// </summary>
        [TestMethod]
        public void ResponseFileInProjectDirectoryNullCase()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string output = RunProcessAndGetOutput(_pathToMSBuildExe, "\"" + projectPath + "\"");
                Assert.IsTrue(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                Directory.Delete(directory);
            }
        }

        #region IgnoreProjectExtensionTests

        /// <summary>
        /// Test the case where the extension is a valid extension but is not a project
        /// file extension. In this case no files should be ignored
        /// </summary>
        [TestMethod]
        public void TestProcessProjectSwitchOneProjNotFoundExtension()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }

        /// <summary>
        /// Test the case where two identical extensions are asked to be ignored
        /// </summary>
        [TestMethod]
        public void TestTwoIdenticalExtensionsToIgnore()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", ".phantomextension" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }
        /// <summary>
        /// Pass a null and an empty list of project extensions to ignore, this simulates the switch not being set on the commandline
        /// </summary>
        [TestMethod]
        public void TestProcessProjectSwitchNullandEmptyProjectsToIgnore()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = (string[])null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");

            extensionsToIgnore = new string[] { };
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }

        /// <summary>
        /// Pass in one extension and a null value
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchNullInList()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", null };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }

        /// <summary>
        /// Pass in one extension and an empty string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchEmptyInList()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", string.Empty };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }

        /// <summary>
        /// If only a dot is specified then the extension is invalid
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchExtensionWithoutDot()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { "phantomextension" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// Put some junk into the extension, in this case there should be an exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchMalformed()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".C:\\boocatmoo.a" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected my.proj to be only project found");
        }

        /// <summary>
        /// Test what happens if there are no project or solution files in the directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchWildcards()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".proj*", ".nativeproj?" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            // Should not get here
            Assert.Fail();
        }

        [TestMethod]
        public void TestProcessProjectSwitch()
        {
            string[] projects = new string[] { "test.nativeproj", "test.vcproj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", ".vcproj" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.nativeproj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.nativeproj to be only project found");

            projects = new string[] { "test.nativeproj", "test.vcproj", "test.proj" };
            extensionsToIgnore = new string[] { ".phantomextension", ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.proj to be only project found");

            projects = new string[] { "test.nativeproj", "test.vcproj" };
            extensionsToIgnore = new string[] { ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.nativeproj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.nativeproj to be only project found");

            projects = new string[] { "test.proj", "test.sln" };
            extensionsToIgnore = new string[] { ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.sln to be only solution found");

            projects = new string[] { "test.proj", "test.sln", "test.proj~", "test.sln~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.sln to be only solution found");

            projects = new string[] { "test.proj" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.proj to be only project found");

            projects = new string[] { "test.proj", "test.proj~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.proj to be only project found");

            projects = new string[] { "test.sln" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.sln to be only solution found");

            projects = new string[] { "test.sln", "test.sln~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.sln to be only solution found");


            projects = new string[] { "test.sln~", "test.sln" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.sln to be only solution found");
        }


        /// <summary>
        /// Ignore .sln and .vcproj files to replicate Building_DF_LKG functionality
        /// </summary>
        [TestMethod]
        public void TestProcessProjectSwitchReplicateBuildingDFLKG()
        {
            string[] projects = new string[] { "test.proj", "test.sln", "Foo.vcproj" };
            string[] extensionsToIgnore = { ".sln", ".vcproj" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.IsTrue(String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase) == 0, "Expected test.proj to be only project found");
        }


        /// <summary>
        /// Test the case where we remove all of the project extensions that exist in the directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchRemovedAllprojects()
        {
            string[] projects;
            string[] extensionsToIgnore = null;
            projects = new string[] { "test.nativeproj", "test.vcproj" };
            extensionsToIgnore = new string[] { ".nativeproj", ".vcproj" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            Assert.Fail();
        }

        /// <summary>
        /// Test the case where there is a solution and a project in the same directory but they have different names
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchSlnProjDifferentNames()
        {
            string[] projects = new string[] { "test.proj", "Different.sln" };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Test the case where we have two proj files in the same directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchTwoProj()
        {
            string[] projects = new string[] { "test.proj", "Different.proj" };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);

            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Test the case where we have two native project files in the same directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchTwoNative()
        {
            string[] projects = new string[] { "test.nativeproj", "Different.nativeproj" };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);

            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Test when there are two solutions in the smae directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchTwoSolutions()
        {
            string[] projects = new string[] { "test.sln", "Different.sln" };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Check the case where there are more than two projects in the directory and one is a proj file
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchMoreThenTwoProj()
        {
            string[] projects = new string[] { "test.nativeproj", "Different.csproj", "Another.proj" };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Test what happens if there are no project or solution files in the directory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void TestProcessProjectSwitchNoProjectOrSolution()
        {
            string[] projects = new string[] { };
            string[] extensionsToIgnore = null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
            // Should not get here
            Assert.Fail();
        }

        /// <summary>
        /// Helper class to simulate directory work for ignore project extensions
        /// </summary>
        internal class IgnoreProjectExtensionsHelper
        {
            private List<string> _directoryFileNameList;

            /// <summary>
            /// Takes in a list of file names to simulate as being in a directory
            /// </summary>
            /// <param name="filesInDirectory"></param>
            internal IgnoreProjectExtensionsHelper(string[] filesInDirectory)
            {
                _directoryFileNameList = new List<string>();
                foreach (string file in filesInDirectory)
                {
                    _directoryFileNameList.Add(file);
                }
            }

            /// <summary>
            /// Mocks System.IO.GetFiles. Takes in known search patterns and returns files which
            /// are provided through the constructor
            /// </summary>
            /// <param name="path">not used</param>
            /// <param name="searchPattern">Pattern of files to return</param>
            /// <returns></returns>
            internal string[] GetFiles(string path, string searchPattern)
            {
                List<string> fileNamesToReturn = new List<string>();
                foreach (string file in _directoryFileNameList)
                {
                    if (String.Compare(searchPattern, "*.sln", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (String.Compare(Path.GetExtension(file), ".sln", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            fileNamesToReturn.Add(file);
                        }
                    }
                    else if (String.Compare(searchPattern, "*.*proj", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (Path.GetExtension(file).Contains("proj"))
                        {
                            fileNamesToReturn.Add(file);
                        }
                    }
                }
                return fileNamesToReturn.ToArray();
            }
        }
        #endregion

        #region ProcessFileLoggerSwitches
        /// <summary>
        /// Test the case where no file logger switches are given, should be no file loggers attached
        /// </summary>
        [TestMethod]
        public void TestProcessFileLoggerSwitch1()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
        }

        /// <summary>
        /// Test the case where a central logger and distributed logger are attached
        /// </summary>
        [TestMethod]
        public void TestProcessFileLoggerSwitch2()
        {
            bool distributedFileLogger = true;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected one distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
        }

        /// <summary>
        /// Test the case where a central file logger is attached but no distributed logger
        /// </summary>
        [TestMethod]
        public void TestProcessFileLoggerSwitch3()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected a central loggers to be attached");

            // add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[1] { "Parameter" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");

            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "Parameter" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
        }

        /// <summary>
        /// Test the case where a distributed file logger is attached but no central logger
        /// </summary>
        [TestMethod]
        public void TestProcessFileLoggerSwitch4()
        {
            bool distributedFileLogger = true;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "logFile=" + Path.Combine(Environment.CurrentDirectory, "MSBuild.log"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[1] { "verbosity=Normal;" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";logFile=" + Path.Combine(Environment.CurrentDirectory, "MSBuild.log"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "verbosity=Normal", "" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";logFile=" + Path.Combine(Environment.CurrentDirectory, "MSBuild.log"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "", "Parameter1" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, ";Parameter1;logFile=" + Path.Combine(Environment.CurrentDirectory, "MSBuild.log"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");


            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "verbosity=Normal;logfile=c:\\temp\\cat.log" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";" + fileLoggerParameters[1], StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");

            distributedLoggerRecords = new List<DistributedLoggerRecord>();
            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "verbosity=Normal;logfile=..\\cat.log;Parameter1" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "Parameter1;verbosity=Normal;logFile=" + Path.Combine(Environment.CurrentDirectory, "..\\cat.log;Parameter1"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");

            loggers = new ArrayList();
            distributedLoggerRecords = new List<DistributedLoggerRecord>();
            fileLoggerParameters = new string[6] { "Parameter1", ";Parameter;", "", ";", ";Parameter", "Parameter;" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Console.WriteLine(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters);
            Assert.IsTrue(string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "Parameter1;Parameter;;;Parameter;Parameter;logFile=" + Path.Combine(Environment.CurrentDirectory, "msbuild.log"), StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");
        }

        /// <summary>
        /// Verify when in single proc mode the file logger enables mp logging and does not show eventId
        /// </summary>
        [TestMethod]
        public void TestProcessFileLoggerSwitch5()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           1
                       );
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
        }
        #endregion

        #region ProcessConsoleLoggerSwitches
        [TestMethod]
        public void ProcessConsoleLoggerSwitches()
        {
            ArrayList loggers = new ArrayList();
            LoggerVerbosity verbosity = LoggerVerbosity.Normal;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>(); ;
            string[] consoleLoggerParameters = new string[6] { "Parameter1", ";Parameter;", "", ";", ";Parameter", "Parameter;" };

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                           true,
                           consoleLoggerParameters,
                           distributedLoggerRecords,
                           verbosity,
                           1,
                           loggers
                       );
            Assert.IsTrue(loggers.Count == 0, "Expected no central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 0, "Expected no distributed loggers to be attached");

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                           false,
                           consoleLoggerParameters,
                           distributedLoggerRecords,
                           verbosity,
                           1,
                           loggers
                       );
            Assert.IsTrue(loggers.Count == 1, "Expected a central loggers to be attached");
            Assert.IsTrue(string.Compare(((ILogger)loggers[0]).Parameters, "EnableMPLogging;SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;parameter;Parameter", StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameters passed in");

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                          false,
                          consoleLoggerParameters,
                          distributedLoggerRecords,
                          verbosity,
                          2,
                          loggers
                      );
            Assert.IsTrue(loggers.Count == 1, "Expected a central loggers to be attached");
            Assert.IsTrue(distributedLoggerRecords.Count == 1, "Expected a distributed logger to be attached");
            DistributedLoggerRecord distributedLogger = ((DistributedLoggerRecord)distributedLoggerRecords[0]);
            Assert.IsTrue(string.Compare(distributedLogger.CentralLogger.Parameters, "SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;parameter;Parameter", StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameters passed in");
            Assert.IsTrue(string.Compare(distributedLogger.ForwardingLoggerDescription.LoggerSwitchParameters, "SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;Parameter;Parameter", StringComparison.OrdinalIgnoreCase) == 0, "Expected parameter in logger to match parameter passed in");
        }
        #endregion
    }
}
