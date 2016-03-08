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
using System.Linq;
using System.Reflection;

using PluginCommon;

namespace Plugin {

    /// <summary>
    /// Our script-compiling plugin.
    /// </summary>
    public class ScriptPlugin : MarshalByRefObject, IPlugin {
        private IHost mHostObj;

        public void SetHostObj(IHost hostObj) {
            Console.WriteLine("Initializing ScriptPlugin, hostObj=" +
                hostObj + ", AppDomain=" + AppDomain.CurrentDomain.Id);

            mHostObj = hostObj;
        }

        /// <summary>
        /// Provides a simple test -- returns (arg + 1).
        /// </summary>
        public int TestRoundTrip(int arg) {
            Console.WriteLine("ScriptPlugin.TestThingsOut(id=" +
                AppDomain.CurrentDomain.Id + ") arg=" + arg);

            if (mHostObj == null) {
                return 0;
            }

            return mHostObj.DoSomethingNifty(arg + 10);
        }

        /// <summary>
        /// Compiles the provided script.
        /// </summary>
        /// <param name="script">The source code to compile.</param>
        /// <param name="cec">Error and warning messages.</param>
        /// <returns>An interface reference, or null on failure.</returns>
        public IScript CompileScript(string script,
            out CompilerErrorCollection cec) {

            Console.WriteLine("CompileScript(id=" +
                AppDomain.CurrentDomain.Id + "): script len=" + script.Length);
            Assembly asm = CompileCode(script, out cec);
            if (asm == null) {
                return null;
            }
            return ConstructIScript(asm);
        }


        /// <summary>
        /// Compiles the C# source code to an assembly.
        /// </summary>
        /// <param name="code">The source code to compile.</param>
        /// <param name="cec">Error and warning messages.</param>
        /// <returns>The newly-created assembly, or null on failure.</returns>
        private static Assembly CompileCode(string code,
            out CompilerErrorCollection cec) {

            // Get access to the C# code generator and code compiler.  Other
            // languages can be used as well.
            Microsoft.CSharp.CSharpCodeProvider csProvider =
                new Microsoft.CSharp.CSharpCodeProvider();

            CompilerParameters parms = new CompilerParameters();
            // We want a DLL, not an EXE.
            parms.GenerateExecutable = false;
            // No need to save to disk (unless you want to cache results).
            parms.GenerateInMemory = true;
            // Be vocal about warnings.
            parms.WarningLevel = 3;

            // Make assemblies available.  We must add our own PluginCommon,
            // so that the script can use the interfaces defined there.  Others
            // can be added depending on what you want the script to be
            // capable of doing.
            parms.ReferencedAssemblies.Add("PluginCommon.dll");

            // Compile the code and return the result.
            CompilerResults cr =
                csProvider.CompileAssemblyFromSource(parms, code);

            cec = cr.Errors;

            if (cr.Errors.HasErrors) {
                return null;
            } else {
                return cr.CompiledAssembly;
            }
        }

        /// <summary>
        /// Finds the first concrete class that implements IScript and
        /// constructs an instance.
        /// </summary>
        private static IScript ConstructIScript(Assembly asm) {
            foreach (Type type in asm.GetExportedTypes()) {
                if (type.IsClass && !type.IsAbstract &&
                    type.GetInterfaces().Contains(typeof(IScript))) {

                    ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
                    IScript iscript = (IScript)ctor.Invoke(null);
                    Console.WriteLine("Created instance: " + iscript);
                    return iscript;
                }
            }
            throw new Exception("No IScript class found");
        }
    }

}
