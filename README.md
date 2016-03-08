DynamicScriptSandbox
--------------------

This is an experiment with C#, dynamic compilation, and AppDomains.  The
goal was to create an environment in which an application could allow the
user to edit C# scripts within the program, then compile and execute them.
The programs would be able to call specific functions in the main program,
but would be run in a sandbox to reduce the possibility of malicious actions.

I had little prior experience with most of the features exercised by this
code, so it's entirely possible that the approach used is dumb and/or wrong.
This is a work in progress.  Use with caution.


### Overview ###

Compiling C# source code into an Assembly is remarkably easy in .NET.  The
trouble is that the assembly can't be unloaded, so your memory footprint
will expand with each recompilation.  It's also harder to keep the compiled
code from deliberately or inadvertently disrupting the main application.

By placing the compiled assembly in a separate AppDomain, we create a way
to discard the compiled code -- just unload the AppDomain -- and expand
the set of security tools.  However, we introduce a new problem relating to
object lifetimes.

The AppDomain is (effectively or actually) a separate virtual machine, with
an independent garbage collector.  The objects in the main app's AppDomain
don't automatically keep objects in the plugin's AppDomain from being
discarded by the garbage collector.  The approach Microsoft used to handle
this was to set an expiration time on objects created across an AppDomain
boundary.  The timer resets when the object is used, but eventually it will
be collected.  To prevent this, a "sponsor" object in the main app must
renew the "lease".

This project demonstrates dynamic compilation into a dedicated AppDomain with
proper handling of lease expiration.  For example, the "script test" does
the following:

 - Creates a new AppDomain (the "plugin" domain).  Some security restrictions
   are set in place.
 - Loads a "script compiler" assembly into the new domain, from a .DLL
   file.  The main app, running in the "host" domain, refers to this
   through the IPlugin interface.
 - Passes the source code across the domain boundary, where it is
   compiled into an assembly.  A reference to an IScript interface is
   returned, along with all compiler diagnostics.
 - The main app calls an IScript method, passing a reference back to itself
   through an IHost interface.  The script calls back across the AppDomain
   boundary into the main app, modifies the return value, and returns it.
 - Unloads the AppDomain.

It's possible to use the interfaces directly, calling objects across the
AppDomain boundary, so long as the objects are subclasses of
MarshalByRefObject.  When serialized, the remote side gets a proxy object
that forwards the calls, rather than a copy of the object itself.

This is similar to any number of "C# plugin" examples, but they were all
lacking in one area or another.


### About the Code ###

This was developed with Microsoft Visual Studio Community 2015 on Windows 10.
I don't know if it will work with Mono on Linux.

To test it yourself, download the project, open it with Visual Studio, and
hit Ctrl-Shift-B to build the main app and the plugin assembly.  Hit F5 to
run the tests in the debugger.  Uncaught exceptions in either AppDomain will
be caught by the debugger, and exceptions will be serialized across the
AppDomain boundary.

There are three projects:

 1. The main project (which includes the DomainManager, the Sponsor wrapper,
    and three increasingly complex test programs).
 2. PluginCommon, which has the common interfaces as well as the
    implementation of PluginLoader.
 3. ScriptPlugin, an implementation of IPlugin that knows how to compile
    C# code.

Most build settings are at their defaults, but the ScriptPlugin project's
output directory is set to a subdirectory of the main project.  The
idea was to put the plugin assembly DLL in a place where the test
project could find it.  In a "real" app this would live elsewhere.

I tried to make the sandboxing as restrictive as possible, but it appears
that the use of the compiler requires full trust.  If you just want to
execute previously-built code, you can severely limit the plugin's
capabilities, e.g. disallow all disk access except for read-only access
to the plugin directory.


### License ###

This code is distributed under the Apache 2 open-source license.


### Acknowledgments ###

When figuring out how to make this work, the following pages were invaluable:

["What is the best scripting language to embed in a C# desktop application?"]
(http://stackoverflow.com/a/596097/294248)

["A Plug-In System Using Reflection, AppDomain and ISponsor"]
(http://www.brad-smith.info/blog/archives/500)

["How to: Run Partially Trusted Code in a Sandbox"]
(https://msdn.microsoft.com/en-us/library/bb763046.aspx)

["Remoting Example: Lifetimes"]
(https://msdn.microsoft.com/en-us/library/6tkeax11(v=vs.85).aspx)


Related technologies:

 - [.NET AddIn](https://msdn.microsoft.com/en-us/library/bb788290(v=vs.110).aspx)
 - [Windows Communication Foundation](https://msdn.microsoft.com/en-us/library/ms731082(v=vs.110).aspx)
(WCF)
 - [CS-Script](http://www.csscript.net/)
