/*
 * Copyright 2016 faddenSoft. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.CodeDom.Compiler;

using PluginCommon;

namespace DynamicScriptSandbox {
    /// <summary>
    /// Test dynamic compilation and execution of a script.
    /// </summary>
    class ScriptTest : MarshalByRefObject, IHost {
        /// <summary>
        /// Hard-coded test script.  For an actual program it would be
        /// better to load this from a file, perhaps one that sits in a
        /// fake project so that VS will identify compilation problems,
        /// but this'll do for a simple test.
        /// 
        /// The script is called directly from the host AppDomain, so we
        /// want to subclass MarshalByRefObject.
        /// 
        /// Having newlines at the end of each line is largely unnecessary,
        /// but it makes the line numbers in compiler warning/error messages
        /// more useful.
        /// </summary>
        private static string TEST_SCRIPT =
            "#warning Test warning\n" +
            "using System;\n" +
            "using PluginCommon;\n" +
            "namespace DynamicScript {\n" +
            "    public class MyTestClass : MarshalByRefObject, IScript {\n" +
            "        public int TestScriptRoundTrip(IHost hostObj, int arg) {\n" +
            "            Console.WriteLine(\"In script (id=\" +\n" +
            "                AppDomain.CurrentDomain.Id + \")\");\n" +
            "            return hostObj.DoSomethingNifty(arg + 1000);\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        /// <summary>
        /// Path where the script plugin DLL lives.  The plugin AppDomain will
        /// have access to this directory, so it would also be a good place
        /// to store compilable scripts.
        /// </summary>
        private string mPluginPath;


        public ScriptTest(string pluginPath) {
            mPluginPath = pluginPath;
        }

        public void RunTest(string dllName) {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("----- ScriptTest -----");
            Console.ResetColor();

            using (DomainManager plugDom = new DomainManager(mPluginPath)) {
                plugDom.CreateDomain("Plugin AppDomain",
                    DomainManager.DomainCapabilities.ALLOW_DYNAMIC);

                IPlugin plugin = plugDom.Load(dllName);
                if (plugin == null) {
                    Console.WriteLine("FAIL");
                    return;
                }

                plugin.SetHostObj(this);

                // Ask the plugin to compile the script in the plugin's
                // AppDomain.  The collection of warning and error messages
                // are serialized and passed back, along with an instance
                // of IScript.
                //
                // We could just as easily dump the warnings and errors from
                // the plugin, but a fancier app might want to display them
                // in some clever UI.
                CompilerErrorCollection cec;
                IScript iscript =
                    plugin.CompileScript(TEST_SCRIPT, out cec);
                for (int i = 0; i < cec.Count; i++) {
                    CompilerError ce = cec[i];
                    if (ce.IsWarning) {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine(ce.ToString());
                }
                Console.ResetColor();
                if (iscript == null) {
                    Console.WriteLine("Compilation failed");
                    return;
                }

                int result = iscript.TestScriptRoundTrip(this, 1);
                Console.WriteLine("Round trip to script: " + result);
            }
        }

        public int DoSomethingNifty(int arg) {
            Console.WriteLine("ScriptTest.DoSomethingNifty(id=" +
                AppDomain.CurrentDomain.Id + ") arg=" + arg);
            return arg + 100;
        }

    }
}
