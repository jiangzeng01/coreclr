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

namespace Tracing.Tests.MethodEvents
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                //JITKeyword (0x10): 0b10000
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000, EventLevel.Verbose)
            };            

            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            //registering Dynamic_All and Clr event callbacks will override each other, disable the check for the prodider and check the events counts in the callback
            //{"Microsoft-Windows_DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
            { "Microsoft-DotNETCore-SampleProfiler", -1 }
        };
        private delegate int HelloInvoker(string msg, int ret);
        private static Action _eventGeneratingAction = () => 
        {
            for(int i=0; i<100; i++)
            {
                if (i % 10 == 0)
                    Logger.logger.Log($"M_verbose occured {i} times...");

                using(M_verbose verbose = new M_verbose())
                {
                    verbose.IsZero('f');
                    verbose.Dispose();
                }
            }
            
            GC.Collect();
            GC.Collect();
            GC.Collect();
        };
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int MethodLoadVerboseEvents = 0;
            int MethodUnloadVerboseEvents = 0;
            source.Clr.MethodLoadVerbose += (eventData) => MethodLoadVerboseEvents += 1;
            //source.Clr.MethodUnloadVerbose += (eventData) => MethodUnloadVerboseEvents += 1;

            int MethodJittingStartedEvents = 0;            
            source.Clr.MethodJittingStarted += (eventData) => MethodJittingStartedEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");
                Logger.logger.Log("MethodLoadVerboseEvents: " + MethodLoadVerboseEvents);
                //Logger.logger.Log("MethodUnloadVerboseEvents: " + MethodUnloadVerboseEvents);
                bool MethoderboseResult = MethodLoadVerboseEvents >= 100;
                Logger.logger.Log("MethoderboseResult check: " + MethoderboseResult);

                Logger.logger.Log("MethodJittingStartedEvents: " + MethodJittingStartedEvents);
                bool MethodJittingStartedResult = MethodJittingStartedEvents >= 100;
                Logger.logger.Log("MethodJittingStartedResult check: " + MethodJittingStartedResult);
                return MethoderboseResult && MethodJittingStartedResult ? 100 : -1;
            };
        };
    }
    

    public class M_verbose : IDisposable
    {      
        public bool IsZero(char c)
        {
            bool i = (c== 0);
            return i;
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}