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
using System.Security;
using System.Security.Permissions;

using PluginCommon;


namespace DynamicScriptSandbox {

    /// <summary>
    /// This is a host-side object that manages the plugin AppDomain.
    /// </summary>
    //[SecurityPermission(SecurityAction.LinkDemand, ControlAppDomain = true, Infrastructure = true)]
    class DomainManager : IDisposable {
        private static bool LEASE_TEST = false;

        /// <summary>
        /// For IDisposable.
        /// </summary>
        bool mDisposed = false;

        /// <summary>
        /// Absolute path to the directory where plugin code lives.
        /// </summary>
        private string mPluginPath;

        /// <summary>
        /// AppDomain handle.
        /// </summary>
        private AppDomain mAppDomain;

        /// <summary>
        /// Reference to the remote PluginLoader object.
        /// </summary>
        private Sponsor<PluginLoader> mPluginLoader;

        /// <summary>
        /// Argument to CreateDomain.  Determines how hard we tighten things
        /// up in the plugin AppDomain.
        /// </summary>
        public enum DomainCapabilities { STATIC_ONLY, ALLOW_DYNAMIC }

        /// <summary>
        /// Constructor.  Just does some basic initialization.
        /// </summary>
        /// <param name="pluginPath">Absolute path to plugin directory.</param>
        public DomainManager(string pluginPath) {
            mPluginPath = pluginPath;
        }

        /// <summary>
        /// Creates a new AppDomain.  If our plugin is just executing
        /// pre-compiled code we can lock the permissions down, but if
        /// it needs to dynamically compile code we need to open things up.
        /// </summary>
        /// <param name="appDomainName">The "friendly" name.</param>
        /// <param name="cap">Permission set.</param>
        public void CreateDomain(string appDomainName, DomainCapabilities cap) {
            if (mAppDomain != null) {
                throw new Exception("Domain already created");
            }

            PermissionSet permSet;
            if (LEASE_TEST) {
                // Use this when overriding InitializeLifetimeService in
                // PluginLoader.  See the comments there for more details.
                permSet = new PermissionSet(PermissionState.Unrestricted);

            } else if (cap == DomainCapabilities.ALLOW_DYNAMIC) {
                // Set the permissions in a way that allows the plugin to
                // run the CSharpCodeProvider.  It looks like the compiler
                // requires "FullTrust", which limits our options here.
                // TODO: see if we can narrow this down.
                permSet = new PermissionSet(PermissionState.Unrestricted);

            } else {
                // Start with everything disabled.
                permSet = new PermissionSet(PermissionState.None);
                // Allow code execution.
                permSet.AddPermission(new SecurityPermission(
                    SecurityPermissionFlag.Execution));

                // This appears to be necessary to allow the lease renewal
                // to work.  Without this the lease silently fails to renew.
                permSet.AddPermission(new SecurityPermission(
                    SecurityPermissionFlag.Infrastructure));

                // Allow changes to Remoting stuff.  Without this, we can't
                // register our ISponsor.
                permSet.AddPermission(new SecurityPermission(
                    SecurityPermissionFlag.RemotingConfiguration));

                // Allow read-only file access, but only in the plugin directory.
                // This is necessary to allow PluginLoader to load the assembly.
                FileIOPermission fp = new FileIOPermission(
                    FileIOPermissionAccess.Read |
                        FileIOPermissionAccess.PathDiscovery,
                    mPluginPath);
                permSet.AddPermission(fp);
            }

            // Configure the AppDomain.  Setting the ApplicationBase
            // property is apparently very important, as it mitigates the
            // risk of certain exploits from untrusted plugin code.
            AppDomainSetup adSetup = new AppDomainSetup();
            adSetup.ApplicationBase = mPluginPath;

            //string hostAppBase =
            //    AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

            // Create the AppDomain.  We're not passing in Evidence or
            // StrongName[].  The latter requires signing the plugin DLL
            // with a keypair.
            mAppDomain = AppDomain.CreateDomain("Plugin AppDomain",
                null, adSetup, permSet);

            Console.WriteLine("Created AppDomain '" + appDomainName +
                "', id=" + mAppDomain.Id);

            // Create a PluginLoader in the remote AppDomain.  The local
            // object is actually a proxy.
            PluginLoader pl = (PluginLoader)mAppDomain.CreateInstanceAndUnwrap(
                typeof(PluginLoader).Assembly.FullName,
                typeof(PluginLoader).FullName);

            // Wrap it so it doesn't disappear on us.
            mPluginLoader = new Sponsor<PluginLoader>(pl);
        }

        /// <summary>
        /// Destroy the AppDomain.
        /// </summary>
        private void DestroyDomain(bool disposing) {
            Console.WriteLine("Unloading AppDomain '" +
                mAppDomain.FriendlyName + "', id=" + mAppDomain.Id +
                ", disposing=" + disposing);
            if (mPluginLoader != null) {
                mPluginLoader.Dispose();
                mPluginLoader = null;
            }
            if (mAppDomain != null) {
                // We can't simply invoke AppDomain.Unload() from a finalizer.
                // The unload is handled by a thread that won't run at the
                // same time as the finalizer thread, so if we got here
                // through finalization we will deadlock.  Fortunately the
                // runtime sees the situation and throws an exception out of
                // Unload().
                //
                // If we don't have a finalizer, and we forget to make an
                // explicit cleanup call, the AppDomain will stick around.
                //
                // So we use a workaround from
                // https://stackoverflow.com/q/4064749/294248 and invoke it
                // asynchronously.
                if (disposing) {
                    AppDomain.Unload(mAppDomain);
                } else {
                    new Action<AppDomain>(AppDomain.Unload).BeginInvoke(
                        mAppDomain, null, null);
                }
                mAppDomain = null;
            }
        }

        /// <summary>
        /// Passes the "Ping()" method through to the plugin loader.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public int Ping(int val) {
            return mPluginLoader.Instance.Ping(val);
        }

        /// <summary>
        /// Loads the assembly in the specified DLL into the plugin AppDomain.
        /// </summary>
        /// <param name="dllName"></param>
        /// <returns></returns>
        public IPlugin Load(string dllName) {
            IPlugin plugin = null;
            try {
                plugin = mPluginLoader.Instance.Load(Path.Combine(mPluginPath, dllName));
            } catch (Exception ex) {
                Console.WriteLine("Plugin load failed: " + ex.Message);
            }

            return plugin;
        }


        /// <summary>
        /// Finalizer.  Required for IDisposable.
        /// </summary>
        ~DomainManager() {
            Console.WriteLine("WARNING: DomainManager finalizer running (id=" +
                (mAppDomain != null ? mAppDomain.Id.ToString() : "--") + ")");
            Dispose(false);
        }

        /// <summary>
        /// Generic IDisposable implementation.
        /// </summary>
        public void Dispose() {
            // Dispose of unmanaged resources (i.e. the AppDomain).
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Destroys the AppDomain, if one was created.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) {
            if (mDisposed) {
                return;
            }

            if (disposing) {
                // Free *managed* objects here.  This is mostly an
                // optimization, as such things will be disposed of
                // eventually by the GC.
            }

            // free unmanaged objects
            if (mAppDomain != null) {
                DestroyDomain(disposing);
            }

            mDisposed = true;
        }
    }
}
