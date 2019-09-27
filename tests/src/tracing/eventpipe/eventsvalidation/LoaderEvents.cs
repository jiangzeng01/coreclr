// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.Loader;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.LoaderEvents
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                //LoaderKeyword (0x8): 0b1000
                new Provider("Microsoft-Windows-DotNETRuntime", 0b1000, EventLevel.Informational)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            { "Microsoft-Windows-DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
        };
        
        static string assemblyPath=null;
        private static Action _eventGeneratingAction = () => 
        {
            GetAssemblyPath();
            try
            {
                for(int i=0; i<100; i++)
                {
                    if (i % 10 == 0)
                        Logger.logger.Log($"Load/Unload Assembly {i} times...");
                    AssemblyLoad assemblyLoad = new AssemblyLoad();
                    assemblyLoad.LoadFromAssemblyPath(assemblyPath+"\\Common.dll");
                    assemblyLoad.Unload();
                }
                GC.Collect();
                GC.Collect();
                GC.Collect();
            }
            catch(Exception ex)
            {
                Logger.logger.Log(ex.Message+ex.StackTrace);
            }
        };

        static void GetAssemblyPath()
        {
            assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int LoaderAssemblyLoadEvents = 0;
            int LoaderAssemblyUnloadEvents = 0;
            source.Clr.LoaderAssemblyLoad += (eventData) => LoaderAssemblyLoadEvents += 1;
            source.Clr.LoaderAssemblyUnload += (eventData) => LoaderAssemblyUnloadEvents += 1;

            int LoaderModuleLoadEvents = 0;
            int LoaderModuleUnloadEvents = 0;
            source.Clr.LoaderModuleLoad += (eventData) => LoaderModuleLoadEvents += 1;
            source.Clr.LoaderModuleUnload += (eventData) => LoaderModuleUnloadEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");

                Logger.logger.Log("LoaderAssemblyLoadEvents: " + LoaderAssemblyLoadEvents);
                Logger.logger.Log("LoaderAssemblyUnloadEvents: " + LoaderAssemblyUnloadEvents);
                //Unload method just marks as unloadable, not unload immediately, so we check the unload events >=1 to make the tests stable
                bool LoaderAssemblyResult = LoaderAssemblyLoadEvents >= 100 && LoaderAssemblyUnloadEvents >= 1;
                Logger.logger.Log("LoaderAssemblyResult check: " + LoaderAssemblyResult);

                Logger.logger.Log("LoaderModuleLoadEvents: " + LoaderModuleLoadEvents);
                Logger.logger.Log("LoaderModuleUnloadEvents: " + LoaderModuleUnloadEvents);
                //Unload method just marks as unloadable, not unload immediately, so we check the unload events >=1 to make the tests stable
                bool LoaderModuleResult = LoaderModuleLoadEvents >= 100 && LoaderModuleUnloadEvents >= 1;
                Logger.logger.Log("LoaderModuleResult check: " + LoaderModuleResult);
                
                return LoaderAssemblyResult && LoaderModuleResult ? 100 : -1;
            };
        };
    }
    public class AssemblyLoad : AssemblyLoadContext
    {
        public AssemblyLoad() : base(true)
        {
        }
    }
}