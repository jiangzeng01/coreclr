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

namespace Tracing.Tests.CLRThreadPool
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                //ThreadingKeyword (0x10000): 0b10000_0000_0000_0000
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000_0000_0000_0000, EventLevel.Informational)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            { "Microsoft-Windows-DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
        };
        
        private static Action _eventGeneratingAction = () => 
        {
            Task[] taskArray = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                if (i % 10 == 0)
                    Logger.logger.Log($"Create new task {i} times...");
                Task task = new Task(() => TestTask());
                taskArray[i] = task;
                taskArray[i].Start();
            }
            Task.WaitAll(taskArray);
            Thread.Sleep(50000);
        };

        static void TestTask()
        {
            Thread.Sleep(10000);
        }

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int ThreadStartEvents = 0;
            int ThreadStopEvents = 0;
            source.Clr.ThreadPoolWorkerThreadStart += (eventData) => ThreadStartEvents += 1;
            source.Clr.ThreadPoolWorkerThreadStop += (eventData) => ThreadStopEvents += 1;

            int ThreadPoolWorkerThreadAdjustmentSampleEvents = 0;
            int ThreadPoolWorkerThreadAdjustmentAdjustmentEvents = 0;
            source.Clr.ThreadPoolWorkerThreadAdjustmentSample += (eventData) => ThreadPoolWorkerThreadAdjustmentSampleEvents += 1;
            source.Clr.ThreadPoolWorkerThreadAdjustmentAdjustment += (eventData) => ThreadPoolWorkerThreadAdjustmentAdjustmentEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");

                Logger.logger.Log("ThreadStartEvents: " + ThreadStartEvents);
                Logger.logger.Log("ThreadStopEvents: " + ThreadStopEvents);
                bool ThreadStartStopResult = ThreadStartEvents >= 1 && ThreadStopEvents >= 1;
                Logger.logger.Log("ThreadStartStopResult check: " + ThreadStartStopResult);

                Logger.logger.Log("ThreadPoolWorkerThreadAdjustmentSampleEvents: " + ThreadPoolWorkerThreadAdjustmentSampleEvents);
                Logger.logger.Log("ThreadPoolWorkerThreadAdjustmentAdjustmentEvents: " + ThreadPoolWorkerThreadAdjustmentAdjustmentEvents);
                bool ThreadAdjustmentResult = ThreadPoolWorkerThreadAdjustmentSampleEvents >= 1 && ThreadPoolWorkerThreadAdjustmentAdjustmentEvents >= 1;
                Logger.logger.Log("ThreadAdjustmentResult check: " + ThreadAdjustmentResult);
                
                return ThreadStartStopResult && ThreadAdjustmentResult ? 100 : -1;
            };
        };
    }
}