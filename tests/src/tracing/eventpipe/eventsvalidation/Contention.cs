// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.Contention
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                //ContentionKeyword (0x4000): 0b100_0000_0000_0000
                new Provider("Microsoft-Windows-DotNETRuntime", 0b100_0000_0000_0000, EventLevel.Informational)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            //registering Dynamic_All and Clr event callbacks will override each other, disable the check for the prodider and check the events counts in the callback
            //{ "Microsoft-Windows-DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
        };

        private static Action _eventGeneratingAction = () => 
        {
            for (int i = 0; i < 50; i++)
            {
                if (i % 10 == 0)
                    Logger.logger.Log($"Thread lock occured {i} times...");
                var myobject = new TestClass();
                Thread thread1 = new Thread(new ThreadStart(() => myobject.DoSomething(myobject)));
                Thread thread2 = new Thread(new ThreadStart(() => myobject.DoSomething(myobject)));
                thread1.Start();
                thread2.Start();
                thread1.Join();
                thread2.Join();
            }
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int ContentionStartEvents = 0;
            source.Clr.ContentionStart += (eventData) => ContentionStartEvents += 1;
            int ContentionStopEvents = 0;
            source.Clr.ContentionStop += (eventData) => ContentionStopEvents += 1;
            return () => {
                Logger.logger.Log("Event counts validation");
                Logger.logger.Log("ContentionStartEvents: " + ContentionStartEvents);
                Logger.logger.Log("ContentionStopEvents: " + ContentionStopEvents);
                return ContentionStartEvents >= 50 && ContentionStopEvents >= 50 ? 100 : -1;
            };
        };

        public class TestClass{
            public int a;
            public void DoSomething(TestClass obj)
            {
                lock(obj)
                {
                    obj.a = 3;
                    Thread.Sleep(100);
                }
            } 
        }
    }
}