// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRMethodVerbose
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000, EventLevel.Verbose)
            };            

            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Console.WriteLine("Validation method: RunAndValidateEventCounts");
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            //{"Microsoft-Windows_DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
            { "Microsoft-DotNETCore-SampleProfiler", -1 }
        };
        private delegate int HelloInvoker(string msg, int ret);
        private static Action _eventGeneratingAction = () => 
        {
            Console.WriteLine("Event generating method: _eventGeneratingAction start");
            ///*
            for(int i=0; i<100; i++)
            {
                using(M_verbose verbose = new M_verbose())
                {
                    verbose.IsZero('f');
                    verbose.Dispose();
                }
            }
            //*/


            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            int Event2 = 0;
            source.Clr.MethodLoadVerbose += (eventData) => Event1 += 1;
            source.Clr.MethodUnloadVerbose += (eventData) => Event2 += 1;

            int Event3 = 0;            
            source.Clr.MethodJittingStarted += (eventData) => Event3 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("Method load verbose events: " + Event1);
                Console.WriteLine("Method unload verbose events: " + Event2);
                Console.WriteLine("Method jitting start events: " + Event3);
                return Event1 > 0 && Event2 > 0 && Event3 > 0 ? 100 : -1;
            };
        };
    }
    

    public class M_verbose : IDisposable
    {      
        public bool IsZero(char c)
        {
            bool i = (c== 0);
            System.Console.WriteLine("i: {0}, c: {1}", i, (c>0));
            return i;
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}