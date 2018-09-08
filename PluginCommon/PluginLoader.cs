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
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using System.Security.Permissions;

namespace PluginCommon {

    /// <summary>
    /// An instance of this class is created in the plugin's AppDomain.  It's
    /// responsible for loading the plugin assembly, and has calls that
    /// allow the host to query and remotely instantiate objects.
    /// <para>
    /// Because the object "lives" in the plugin AppDomain but is called
    /// from the host AppDomain, it must derive from MarshalByRefObject.
    /// </para>
    /// </summary>
    public sealed class PluginLoader : MarshalByRefObject {
        public PluginLoader() {
            Console.WriteLine("Hello from the other side, id=" +
                AppDomain.CurrentDomain.Id);
        }

        /// <summary>
        /// Loads the assembly in the specified DLL, finds the first
        /// concrete class that implements IPlugin, and instantiates it.
        /// </summary>
        /// <param name="dllPath">Absolute path to DLL.</param>
        public IPlugin Load(string dllPath) {
            Assembly asm = Assembly.LoadFile(dllPath);

            foreach (Type type in asm.GetTypes()) {
                if (type.IsClass && !type.IsAbstract &&
                    type.GetInterfaces().Contains(typeof(IPlugin))) {

                    ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
                    IPlugin plugin = (IPlugin)ctor.Invoke(null);
                    Console.WriteLine("Created instance: " + plugin);
                    return plugin;
                }
            }
            throw new Exception("No IPlugin class found");
        }

        /// <summary>
        /// Allows the host to ping the loader class in the plugin AppDomain.
        /// Strictly for debugging.
        /// </summary>
        public int Ping(int val) {
            Console.WriteLine("PluginLoader(id=" + AppDomain.CurrentDomain.Id +
                "): ping " + val);
            return val + 1;
        }


#if false
        // NOTE: if you try to do this with a restricted environment
        // (i.e. a non-Unrestricted PermissionSet in CreateDomain) you will
        // get an exception with a "Inheritance security rules violated"
        // message.  Because the plugin code is being treated as untrusted,
        // the usual workaround -- add [SecurityCritical] above the method
        // declaration -- will not work.
        //
        // So, if you enable this, you must open up the PermissionSet.  Since
        // this is only useful when testing leases and sponsors, this should
        // not be limiting.
        //
        // There might be a way to relax the PermissionSet to allow
        // [SecurityCritical] to work, but that might allow bad behaviors.

        /// <summary>
        /// DEBUG ONLY: establish a fast lease timeout.  Normally the lease
        /// is five minutes; this reduces it to a few seconds.  (I see Renewal
        /// called every 10 seconds, so it appears to have a minimum bound.)
        /// </summary>
        [SecurityPermissionAttribute(SecurityAction.Demand, 
                                 Flags=SecurityPermissionFlag.Infrastructure)]
        public override Object InitializeLifetimeService() {
            ILease lease = (ILease) base.InitializeLifetimeService();

            Console.WriteLine("Default lease: ini=" +
                lease.InitialLeaseTime + " spon=" +
                lease.SponsorshipTimeout + " ren=" +
                lease.RenewOnCallTime);

            if (lease.CurrentState == LeaseState.Initial) {
                // Initial lease duration.
                lease.InitialLeaseTime = TimeSpan.FromSeconds(3);

                // How long we will wait for the sponsor to respond
                // with a lease renewal time.
                lease.SponsorshipTimeout = TimeSpan.FromSeconds(5);

                // Each call to the remote object extends the lease so that
                // it has at least this much time left.
                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }
#endif
#if false
        // Same as the above, but with reflection.  Use this if your library
        // is based on netstandard rather than Win platform, as the .net
        // standard lib doesn't include the Remoting classes.
        [System.Security.SecurityCritical]
        public override object InitializeLifetimeService() {
            object lease = base.InitializeLifetimeService();

            // netstandard2.0 doesn't have System.Runtime.Remoting.Lifetime, so use reflection
            PropertyInfo leaseState = lease.GetType().GetProperty("CurrentState");
            PropertyInfo initialLeaseTime = lease.GetType().GetProperty("InitialLeaseTime");
            PropertyInfo sponsorshipTimeout = lease.GetType().GetProperty("SponsorshipTimeout");
            PropertyInfo renewOnCallTime = lease.GetType().GetProperty("RenewOnCallTime");

            Console.WriteLine("Default lease: ini=" +
                initialLeaseTime.GetValue(lease) + " spon=" +
                sponsorshipTimeout.GetValue(lease) + " renOC=" +
                renewOnCallTime.GetValue(lease));

            if ((int)leaseState.GetValue(lease) == 1 /*LeaseState.Initial*/) {
                // Initial lease duration.
                initialLeaseTime.SetValue(lease, TimeSpan.FromSeconds(8));

                // How long we will wait for the sponsor to respond
                // with a lease renewal time.
                sponsorshipTimeout.SetValue(lease, TimeSpan.FromSeconds(5));

                // Each call to the remote object extends the lease so that
                // it has at least this much time left.
                renewOnCallTime.SetValue(lease, TimeSpan.FromSeconds(2));
            }
            return lease;
        }
#endif
    }

}
