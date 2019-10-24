// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.GCEventsVerbose
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                //GCKeyword (0x1): 0b1
                new Provider("Microsoft-Windows-DotNETRuntime", 0b1, EventLevel.Verbose)
            };
            
            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
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
            List<string> testList = new List<string>();
            for(int i=0; i<100000000; i++)
            {
                string t = "Test string!";
                testList.Add(t);
            }
            GC.Collect();
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
             int GCCreateSegmentEvents = 0;
            int GCFreeSegmentEvents = 0;
            source.Clr.GCCreateSegment += (eventData) => GCCreateSegmentEvents += 1;
            source.Clr.GCFreeSegment += (eventData) => GCFreeSegmentEvents += 1;

            int GCAllocationTickEvents = 0;
            source.Clr.GCAllocationTick += (eventData) => GCAllocationTickEvents += 1;

            int GCCreateConcurrentThreadEvents = 0;
            int GCTerminateConcurrentThreadEvents = 0;
            source.Clr.GCCreateConcurrentThread += (eventData) => GCCreateConcurrentThreadEvents += 1;
            source.Clr.GCTerminateConcurrentThread += (eventData) => GCTerminateConcurrentThreadEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");

                Logger.logger.Log("GCCreateSegmentEvents: " + GCCreateSegmentEvents);
                Logger.logger.Log("GCFreeSegmentEvents: " + GCFreeSegmentEvents);
                bool GCSegmentResult = GCCreateSegmentEvents > 0 && GCFreeSegmentEvents > 0;
                Logger.logger.Log("GCSegmentResult: " + GCSegmentResult); 

                Logger.logger.Log("GCAllocationTickEvents: " + GCAllocationTickEvents);
                bool GCAllocationTickResult = GCAllocationTickEvents > 0;
                Logger.logger.Log("GCAllocationTickResult: " + GCAllocationTickResult); 

                Logger.logger.Log("GCCreateConcurrentThreadEvents: " + GCCreateConcurrentThreadEvents);
                Logger.logger.Log("GCTerminateConcurrentThreadEvents: " + GCTerminateConcurrentThreadEvents);
                bool GCConcurrentResult = GCCreateConcurrentThreadEvents > 0 && GCTerminateConcurrentThreadEvents > 0;
                Logger.logger.Log("GCConcurrentResult: " + GCConcurrentResult);

                bool GCCollectResults = GCSegmentResult && GCAllocationTickResult && GCConcurrentResult;
                Logger.logger.Log("GCCollectResults: " + GCCollectResults);

                return GCCollectResults ? 100 : -1;
            };
        };
    }
}