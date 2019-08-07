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
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-Windows-DotNETRuntime", 0b100_0000_0000_0000, EventLevel.Informational)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Console.WriteLine("Validation method: RunAndValidateEventCounts");
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            //{ "Microsoft-Windows-DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 }
        };

        private static Action _eventGeneratingAction = () => 
        {
            Console.WriteLine("Event generating method: _eventGeneratingAction start");
            for (int i = 0; i < 100; i++)
            {
                var myobject = new TestClass();
                Thread thread1 = new Thread(new ThreadStart(() => myobject.DoSomething(myobject)));
                Thread thread2 = new Thread(new ThreadStart(() => myobject.DoSomething(myobject)));
                thread1.Start();
                thread2.Start();
                thread1.Join();
                thread2.Join();
            }
            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int ContentionStartEvents = 0;
            int ContentionStopEvents = 0;
            source.Clr.ContentionStart += (eventData) => ContentionStartEvents += 1;
            source.Clr.ContentionStop += (eventData) => ContentionStopEvents += 1;
            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("ContentionStartEvents: " + ContentionStartEvents);
                Console.WriteLine("ContentionStopEvents: " + ContentionStopEvents);
                return ContentionStartEvents >= 100 && ContentionStopEvents >= 100 && ContentionStartEvents == ContentionStopEvents ? 100 : -1;
            };
        };
    }

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