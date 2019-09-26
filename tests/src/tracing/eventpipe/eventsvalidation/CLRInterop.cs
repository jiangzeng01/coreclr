// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRInterop
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10_0000_0000_0000, EventLevel.Informational)
            };            

            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Console.WriteLine("Validation method: RunAndValidateEventCounts");
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
            { "Microsoft-DotNETCore-SampleProfiler", -1 }
        };

        private static Action _eventGeneratingAction = () => 
        {
            Console.WriteLine("Event generating method: _eventGeneratingAction start");
            
            for(int i = 0; i<10; i++)
            {CLRInteropTemp interop = new CLRInteropTemp();
            interop.GenerateCache("sdfsf");}

            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            source.Clr.ILStubStubGenerated += (eventData) => Event1 += 1;

            int Event2 = 0;
            source.Clr.ILStubStubCacheHit += (eventData) => Event2 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("MSIL stub events: " + Event1);
                Console.WriteLine("MSIL hit events: " + Event2);
                return Event1 == Event2 && Event1 > 0 ? 100 : -1;
            };
        };
    }

    public class CLRInteropTemp
    {
        public void GenerateCache(string s)
        {
            System.Console.WriteLine(s);
        }   
    }
}