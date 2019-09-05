// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRThreadPool
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000000000000000, EventLevel.Informational)
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
            
            for(int i=0; i<100; i++)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(ShowThreadInfo), i); 
                Thread.Sleep(1000); 
            }

            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        static void ShowThreadInfo(object s)
        {
        }

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            int Event2 = 0;
            source.Clr.ThreadPoolWorkerThreadStart += (eventData) => Event1 += 1;
            source.Clr.ThreadPoolWorkerThreadStop += (eventData) => Event2 += 1;
            
            int Event3 = 0;
            int Event4 = 0;
            source.Clr.ThreadPoolWorkerThreadRetirementStart += (eventData) => Event3 += 1;
            source.Clr.ThreadPoolWorkerThreadRetirementStop += (eventData) => Event4 += 1;
            
            int Event5 = 0;
            int Event6 = 0;
            source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment += (eventData) => Event5 += 1;
            source.Clr.ThreadPoolWorkerThreadAdjustmentSample += (eventData) => Event6 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("Worker thread start events: " + Event1);
                Console.WriteLine("Worker thread stop events: " + Event2);
                Console.WriteLine("Worker thread Retirement start events: " + Event3);
                Console.WriteLine("Worker thread Retirement stop events: " + Event4);
                Console.WriteLine("Worker thread Adjustment events: " + Event5);
                Console.WriteLine("Worker thread Adjustment Sample events: " + Event6);
                return Event1 > 1 && Event2 > 0 ? 100 : -1;
            };
        };
    } 
}