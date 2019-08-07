// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.ThreadStartStop
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000_0000_0000_0000, EventLevel.Informational)
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

        private static Action _eventGeneratingAction = () => 
        {
            Console.WriteLine("Event generating method: _eventGeneratingAction start");
            Task[] taskArray = new Task[250];
            Console.WriteLine("Creating tasks");
            for (int i = 0; i < 250; i++)
            {
                Task task = new Task(() => TestTask());
                taskArray[i] = task;
                taskArray[i].Start();
            }
            Console.WriteLine("Waiting for tasks complete");
            Task.WaitAll(taskArray); 
            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        static void TestTask()
        {
            Thread.Sleep(100);
        }

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int ThreadStartEvents = 0;
            int ThreadStopEvents = 0;
            int ThreadRetirementStartEvents = 0;
            int ThreadRetirementStopEvents = 0;
            source.Clr.ThreadPoolWorkerThreadStart += (eventData) => ThreadStartEvents += 1;
            source.Clr.ThreadPoolWorkerThreadStop += (eventData) => ThreadStopEvents += 1;
            source.Clr.ThreadPoolWorkerThreadRetirementStart += (eventData) => ThreadRetirementStartEvents += 1;
            source.Clr.ThreadPoolWorkerThreadRetirementStop += (eventData) => ThreadRetirementStopEvents += 1;
            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("ThreadStartEvents: " + ThreadStartEvents);
                Console.WriteLine("ThreadStopEvents: " + ThreadStopEvents);
                Console.WriteLine("ThreadRetirementStartEvents: " + ThreadRetirementStartEvents);
                Console.WriteLine("ThreadRetirementStopEvents: " + ThreadRetirementStopEvents);
                
                return -1;
                //return ThreadStartEvents >= 1000 && ThreadRetirementStop >= 1000 && ThreadStartEvents == ThreadRetirementStop ? 100 : -1;
            };
        };
    }
}