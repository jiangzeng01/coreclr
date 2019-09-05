// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.Loader;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRLoaderAssembly
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b1000, EventLevel.Informational)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Console.WriteLine("Validation method: RunAndValidateEventCounts");
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
            
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            { "Microsoft-Windows-DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
            { "Microsoft-DotNETCore-SampleProfiler", -1 }
        };

        static string assemblyPath=null;
        private static Action _eventGeneratingAction = () => 
        {
            Console.WriteLine("Event generating method: _eventGeneratingAction start");
            GetAssemblyPath();
            try
            {
                for(int i=0; i<100; i++)
                {
                    AssemblyLoad assemblyLoad = new AssemblyLoad();
                    
                    assemblyLoad.LoadFromAssemblyPath(assemblyPath+"\\Common.dll");
                    assemblyLoad.Unload();
                }
            }
            catch(Exception ex)
            {
                System.Console.WriteLine(ex.Message+ex.StackTrace);
            }

            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        static void GetAssemblyPath()
        {
            assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            int Event2 = 0;
            source.Clr.LoaderAssemblyLoad += (eventData) => Event1 += 1;
            source.Clr.LoaderAssemblyUnload += (eventData) => Event2 += 1;
            
            int Event3 = 0;
            int Event4 = 0;
            source.Clr.LoaderModuleLoad += (eventData) => Event3 += 1;
            source.Clr.LoaderModuleUnload += (eventData) => Event4 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("Loader Assembly load events: " + Event1);
                Console.WriteLine("Loader Assembly Unload events: " + Event2);
                Console.WriteLine("Loader Module load events: " + Event3);
                Console.WriteLine("Loader Module Unload events: " + Event4);
                return Event1 > 100 && Event2 > 1 && Event3 > 100 && Event4 > 1 ? 100 : -1;
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