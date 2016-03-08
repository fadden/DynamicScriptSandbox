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
using System.IO;

namespace DynamicScriptSandbox {

    /// <summary>
    /// Entry point for the test project.
    /// </summary>
    class Program {
        static void Main(string[] args) {
            string pluginPath = Path.Combine(Environment.CurrentDirectory,
                "Plugins");

            PingTest.RunTest(pluginPath);

            PluginTest pluginTest = new PluginTest(pluginPath);
            pluginTest.RunTest("ScriptPlugin.dll");

            ScriptTest scriptTest = new ScriptTest(pluginPath);
            scriptTest.RunTest("ScriptPlugin.dll");

            Console.WriteLine("Done, hit <Enter> to exit");
            Console.ReadLine();
        }
    }

}
