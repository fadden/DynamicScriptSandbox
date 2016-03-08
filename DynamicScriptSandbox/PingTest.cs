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

namespace DynamicScriptSandbox {

    class PingTest {
        /// <summary>
        /// Creates a new AppDomain, pings the remote side, and bails.
        /// </summary>
        /// <param name="pluginPath">Absolute path to plugin directory.</param>
        public static void RunTest(string pluginPath) {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("----- PingTest -----");
            Console.ResetColor();

            using (DomainManager plugDom = new DomainManager(pluginPath)) {
                plugDom.CreateDomain("Plugin AppDomain",
                    DomainManager.DomainCapabilities.STATIC_ONLY);

                Console.WriteLine("Ping from host (id=" +
                    AppDomain.CurrentDomain.Id + ")");

                Console.WriteLine("ping 1 returned " +
                    plugDom.Ping(1));

                // If testing Sponsor with short leases, it's useful to
                // re-try the ping after the lease renewal has had a
                // chance to fire.
                //Thread.Sleep(12 * 1000 + 1000);
                Console.WriteLine("ping 2 returned " +
                    plugDom.Ping(2));
            }
        }
    }

}
