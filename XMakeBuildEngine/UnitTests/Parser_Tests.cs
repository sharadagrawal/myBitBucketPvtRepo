﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ParserTest
    {
        /// <summary>
        ///  Make a fake element location for methods who need one.
        /// </summary>
        private MockElementLocation _elementLocation = MockElementLocation.Instance;

        /// <summary>
        /// </summary>
        [TestMethod]
        public void SimpleParseTest()
        {
            Console.WriteLine("SimpleParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;

            tree = p.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo)=='hello'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo)==''", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) and $(buildlab) and $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) or $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) and $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(full) or $(debug) and $(buildlab)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("%(culture)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("%(culture)=='french'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("'foo_%(culture)'=='foo_french'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("true", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("false", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("0", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("0.0 == 0", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void ComplexParseTest()
        {
            Console.WriteLine("ComplexParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;

            tree = p.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("($(foo) or $(bar)) and $(baz)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo) <= 5 and $(bar) >= 15", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) or $(bar) == false) and !($(baz) == simplestring))", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) or Exists('c:\\foo.txt')) and !(($(baz) == simplestring)))", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void NotParseTest()
        {
            Console.WriteLine("NegationParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            tree = p.Parse("!true", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!(true)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!($(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!(%(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!($(foo) <= 5 and $(bar) >= 15)", ParserOptions.AllowAll, _elementLocation);
        }
        /// <summary>
        /// </summary>
        [TestMethod]
        public void FunctionCallParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            tree = p.Parse("SimpleFunctionCall()", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("SimpleFunctionCall( 1234 )", ParserOptions.AllowAll, _elementLocation);
            tree = p.Parse("SimpleFunctionCall( true )", ParserOptions.AllowAll, _elementLocation);
            tree = p.Parse("SimpleFunctionCall( $(property) )", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )", ParserOptions.AllowAll, _elementLocation);
        }

        [TestMethod]
        public void ItemListParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("@(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == @(foo)", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(@(foo), 'otherstuff')", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
        }

        [TestMethod]
        public void MetadataParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("%(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == %(foo)", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(%(foo), 'otherstuff')", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void NegativeTests()
        {
            Console.WriteLine("NegativeTests()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            try
            {
                fExceptionCaught = false;
                // Note no close quote ----------------------------------------------------V
                tree = p.Parse("'a more complex' == 'asdf", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Note no close quote ----------------------------------------------------V
                tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("($(foo) == 'simple string') $(bar)", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("=='x'", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("==", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse(">", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==true", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("1==(2", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.IsTrue(fExceptionCaught);
        }

        /// <summary>
        /// This test verifies that we trigger warnings for expressions that
        /// could be incorrectly evaluated
        /// </summary>
        [TestMethod]
        public void VerifyWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsTrue(ml.FullLog.Contains("MSB4130:"), "Need to warn for this expression - (a) == 1 and $(b) == 2 or $(c) == 3.");

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsTrue(ml.FullLog.Contains("MSB4130:"), "Need to warn for this expression - (a) == 1 or $(b) == 2 and $(c) == 3.");

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsTrue(ml.FullLog.Contains("MSB4130:"), "Need to warn for this expression - ($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4.");
        }

        /// <summary>
        /// This test verifies that we don't trigger warnings for expressions that
        /// couldn't be incorrectly evaluated
        /// </summary>
        [TestMethod]
        public void VerifyNoWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsFalse(ml.FullLog.Contains("MSB4130:"), "No need to warn for this expression - (a) == 1 and $(b) == 2 and $(c) == 3.");

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsFalse(ml.FullLog.Contains("MSB4130:"), "No need to warn for this expression - (a) == 1 or $(b) == 2 or $(c) == 3.");

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 and $(b) == 2) or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsFalse(ml.FullLog.Contains("MSB4130:"), "No need to warn for this expression - ($(a) == 1 and $(b) == 2) or $(c) == 3.");

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2) and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assert.IsFalse(ml.FullLog.Contains("MSB4130:"), "No need to warn for this expression - ($(a) == 1 or $(b) == 2) and $(c) == 3.");
        }
    }
}
