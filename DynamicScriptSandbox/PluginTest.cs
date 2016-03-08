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

using PluginCommon;

namespace DynamicScriptSandbox {
    /// <summary>
    /// Quick plugin test.  We pass a reference to the plugin AppDomain to
    /// ourselves (so we want to derive from MarshalByRefObject) so that
    /// the plugin can invoke our IHost interface methods.
    /// </summary>
    class PluginTest : MarshalByRefObject, IHost {
        private string mPluginPath;

        public PluginTest(string pluginPath) {
            mPluginPath = pluginPath;
        }

        public void RunTest(string dllName) {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("----- PluginTest -----");
            Console.ResetColor();

            using (DomainManager plugDom = new DomainManager(mPluginPath)) {
                plugDom.CreateDomain("Plugin AppDomain",
                    DomainManager.DomainCapabilities.STATIC_ONLY);

                IPlugin plugin = plugDom.Load(dllName);
                if (plugin == null) {
                    Console.WriteLine("FAIL");
                    return;
                }

                int result;

                result = plugin.TestRoundTrip(1);
                Console.WriteLine("Round trip, no host obj: " + result);

                plugin.SetHostObj(this);
                result = plugin.TestRoundTrip(1);
                Console.WriteLine("Round trip, w/ host obj: " + result);
            }
        }

        public int DoSomethingNifty(int arg) {
            Console.WriteLine("PluginTest.DoSomethingNifty(id=" +
                AppDomain.CurrentDomain.Id + ") arg=" + arg);
            return arg + 100;
        }

    }
}
