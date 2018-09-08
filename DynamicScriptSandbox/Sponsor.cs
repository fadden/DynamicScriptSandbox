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
using System.Runtime.Remoting.Lifetime;
using System.Security.Permissions;

namespace DynamicScriptSandbox {

    /// <summary>
    /// This wraps a MarshalByRefObject instance with a "sponsor".  This
    /// is necessary because objects created by the host in the plugin
    /// AppDomain aren't strongly referenced across the boundary (the two
    /// AppDomains have independent garbage collection).  Because the plugin
    /// AppDomain can't know when the host AppDomain discards its objects,
    /// it will discard its side after a period of disuse.
    ///
    /// The ISponsor/ILease mechanism provides a way for the host-side object
    /// to define the lifespan of the plugin-side objects.  The object
    /// manager in the plugin will invoke Renewal() back in the host-side
    /// AppDomain.
    /// </summary>
    [SecurityPermission(SecurityAction.Demand, Infrastructure = true)]
    class Sponsor<T> : MarshalByRefObject, ISponsor, IDisposable
        where T : MarshalByRefObject {

        /// <summary>
        /// The object we've wrapped.
        /// </summary>
        private T mObj;

        /// <summary>
        /// For IDisposable.
        /// </summary>
        bool mDisposed = false;

        // For debugging, track the last renewal time.
        private DateTime mLastRenewal = DateTime.Now;


        public T Instance {
            get {
                if (mDisposed) {
                    throw new ObjectDisposedException("Sponsor was disposed");
                } else {
                    return mObj;
                }
            }
        }

        public Sponsor(T obj) {
            mObj = obj;

            // Get the lifetime service lease from the MarshalByRefObject,
            // and register ourselves as a sponsor.
            ILease lease = (ILease)obj.GetLifetimeService();
            lease.Register(this);
        }


        /// <summary>
        /// Extends the lease time for the wrapped object.  This is called
        /// from the plugin AppDomain, but executes on the host AppDomain.
        /// </summary>
        [SecurityPermissionAttribute(SecurityAction.LinkDemand,
                                Flags=SecurityPermissionFlag.Infrastructure)]
        TimeSpan ISponsor.Renewal(ILease lease) {
            DateTime now = DateTime.Now;
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") +
                "|Lease renewal for " + mObj + ", last renewed " +
                (now - mLastRenewal) + " sec ago (id=" +
                AppDomain.CurrentDomain.Id + ")");
            mLastRenewal = now;

            if (mDisposed) {
                // Shouldn't happen -- we should be unregistered -- but I
                // don't know if multiple threads are involved.
                Console.WriteLine("WARNING: attempted to renew a disposed Sponsor");
                return TimeSpan.Zero;
            } else {
                // Use the lease's RenewOnCallTime.
                return lease.RenewOnCallTime;
            }
        }

        /// <summary>
        /// Finalizer.  Required for IDisposable.
        /// </summary>
        ~Sponsor() {
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

            // If this is a managed object, call its Dispose method.
            if (disposing) {
                if (mObj is IDisposable) {
                    ((IDisposable)mObj).Dispose();
                }
            }

            // Remove ourselves from the lifetime service.
            object leaseObj;
            try {
                leaseObj = mObj.GetLifetimeService();
            } catch (Exception ex) {
                Console.WriteLine("WARNING: GetLifetimeService failed: " + ex.Message);
                leaseObj = null;
            }
            if (leaseObj is ILease) {
                ILease lease = (ILease)leaseObj;
                lease.Unregister(this);
            }

            mDisposed = true;
        }
    }
}
