// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.GCStartStop
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
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
            Logger.logger.Log("Event generating method: _eventGeneratingAction start");
            List<string> testList = new List<string>();
            for(int i=0; i<100000000; i++)
            {
                string t = "This is a test!!!!This is a test!!!!This is a test!!!!";
                testList.Add(t);
            }
            GC.Collect();
            Logger.logger.Log("Event generating method: _eventGeneratingAction end");
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int GCStartEvents = 0;
            int GCEndEvents = 0;
            source.Clr.GCStart += (eventData) => GCStartEvents += 1;
            source.Clr.GCStop += (eventData) => GCEndEvents += 1;
            
            int GCHeapStatsEvents = 0;
            source.Clr.GCHeapStats += (eventData) => GCHeapStatsEvents += 1; 
            
            int GCCreateSegmentEvents = 0;
            int GCFreeSegmentEvents = 0;
            source.Clr.GCCreateSegment += (eventData) => GCCreateSegmentEvents += 1;
            source.Clr.GCFreeSegment += (eventData) => GCFreeSegmentEvents += 1;
            
            int GCRestartEEStartEvents = 0;
            int GCRestartEEStopEvents = 0;
            source.Clr.GCRestartEEStart += (eventData) => GCRestartEEStartEvents += 1;
            source.Clr.GCRestartEEStop += (eventData) => GCRestartEEStopEvents += 1;
            
            int GCSuspendEEStartEvents = 0;
            int GCSuspendEEStopEvents = 0;
            source.Clr.GCSuspendEEStart += (eventData) => GCSuspendEEStartEvents += 1;
            source.Clr.GCSuspendEEStop += (eventData) => GCSuspendEEStopEvents += 1;
            
            int GCAllocationTickEvents = 0;
            source.Clr.GCAllocationTick += (eventData) => GCAllocationTickEvents += 1;
            
            int GCFinalizersStartEvents = 0;
            int GCFinalizersStopEvents = 0;
            source.Clr.GCFinalizersStart += (eventData) => GCFinalizersStartEvents += 1;
            source.Clr.GCFinalizersStop += (eventData) => GCFinalizersStopEvents += 1;
            
            int GCCreateConcurrentThreadEvents = 0;
            int GCTerminateConcurrentThreadEvents = 0;
            source.Clr.GCCreateConcurrentThread += (eventData) => GCCreateConcurrentThreadEvents += 1;
            source.Clr.GCTerminateConcurrentThread += (eventData) => GCTerminateConcurrentThreadEvents += 1;
            return () => {
                Logger.logger.Log("Event counts validation");
                Logger.logger.Log("GCStartEvents: " + GCStartEvents);
                Logger.logger.Log("GCEndEvents: " + GCEndEvents);  
                bool GCStartStopResult = GCStartEvents > 1 && GCEndEvents > 1;  
                Logger.logger.Log("GCStartStopResult: " + GCStartStopResult); 

                Logger.logger.Log("GCHeapStatsEvents: " + GCHeapStatsEvents);        
                bool GCHeapResult = GCHeapStatsEvents > 1;
                Logger.logger.Log("GCHeapResult: " + GCHeapResult); 

                Logger.logger.Log("GCRestartEEStartEvents: " + GCRestartEEStartEvents);
                Logger.logger.Log("GCRestartEEStopEvents: " + GCRestartEEStopEvents);
                bool GCRestartEEResult = GCRestartEEStartEvents > 100 && GCRestartEEStopEvents > 100;
                Logger.logger.Log("GCRestartEEResult: " + GCRestartEEResult); 

                Logger.logger.Log("GCSuspendEEStartEvents: " + GCSuspendEEStartEvents);
                Logger.logger.Log("GCSuspendEEStopEvents: " + GCSuspendEEStopEvents);
                bool GCSuspendEEResult = GCSuspendEEStartEvents > 100 && GCSuspendEEStopEvents > 100;
                Logger.logger.Log("GCSuspendEEResult: " + GCSuspendEEResult); 

                Logger.logger.Log("GCFinalizersStartEvents: " + GCFinalizersStartEvents);
                Logger.logger.Log("GCFinalizersStopEvents: " + GCFinalizersStopEvents);
                bool GCFinalizersResult = GCFinalizersStartEvents > 1 && GCFinalizersStopEvents > 1;
                Logger.logger.Log("GCFinalizersResult: " + GCFinalizersResult);

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
                
                bool GCCollectResults = GCStartStopResult && GCHeapResult && GCRestartEEResult && GCSuspendEEResult 
                                        && GCSegmentResult && GCAllocationTickResult && GCConcurrentResult;
                Logger.logger.Log("GCCollectResults: " + GCCollectResults);
                
                return GCCollectResults ? 100 : -1;
            };
        };
    }
}