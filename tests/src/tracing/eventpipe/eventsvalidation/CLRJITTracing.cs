// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Runtime.CompilerServices; 
using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRJITTracing
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Logger.logger.Log("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b1_0000, EventLevel.Verbose)
            };            

            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Logger.logger.Log("Validation method: RunAndValidateEventCounts");
            return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, configuration, _DoesTraceContainEvents);
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            //{"Microsoft-Windows_DotNETRuntime", -1 },
            { "Microsoft-Windows-DotNETRuntimeRundown", -1 },
            { "Microsoft-DotNETCore-SampleProfiler", -1 }
        };
        private static Action _eventGeneratingAction = () => 
        {
            Logger.logger.Log("Event generating method: _eventGeneratingAction start");

            int[] x = new int[5]{5,4,7,8,3};

            Thread.Sleep(1000);
            foreach(int y in x)
            {
                MethodA(y);
                MethodB(y);
            }

            var s=Obj.Name;
            
            Logger.logger.Log("Event generating method: _eventGeneratingAction end");
        };
        struct Obj
        {
            public static string Name
            {
                get;set;
            }
        }
        static int MethodB(int i)
        {
            var r = new Random();
            Logger.logger.Log(System.Reflection.MethodBase.GetCurrentMethod().Name);
            i = (i / 1000) + r.Next();
            i = (i / 1000) + r.Next();
            return MethodA(i);
        }
        static int MethodA(int i)
        {
            Random r = new Random();
            i = (i / 1000) + r.Next();
            i = (i / 1000) + r.Next();
            return i;
        }      
        
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Logger.logger.Log("Callback method: _DoesTraceContainEvents");
            int TailFailed = 0;
            source.Clr.MethodTailCallFailed += (eventData) => TailFailed += 1;
            List<string> list1 = new List<string>();            
            source.Clr.MethodTailCallFailed += (eventData) => list1.Add(eventData.ClrInstanceID.ToString());
            
            int TailSucceeded = 0;
            source.Clr.MethodTailCallSucceeded += (eventData) => TailSucceeded += 1;
            List<string> list2 = new List<string>();            
            source.Clr.MethodTailCallSucceeded += (eventData) => list2.Add(eventData.ClrInstanceID.ToString());
            
            int InliningFailed = 0;
            source.Clr.MethodInliningFailed += (eventData) => InliningFailed += 1;
            List<string> list3 = new List<string>();            
            source.Clr.MethodInliningFailed += (eventData) => list3.Add(eventData.ClrInstanceID.ToString());
            
            int InliningSucceeded = 0;
            source.Clr.MethodInliningSucceeded += (eventData) => InliningSucceeded += 1;
            List<string> list4 = new List<string>();            
            source.Clr.MethodInliningSucceeded += (eventData) => list4.Add(eventData.ClrInstanceID.ToString());

            return () => {
                if(list1.Count == 0)
                    Logger.logger.Log("List1 No Events!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                else
                {                    
                    foreach(var l in list1)
                        Logger.logger.Log("Address: \n"+l.ToString());
                }
                if(list2.Count == 0)
                    Logger.logger.Log("List2 No Events!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                else
                {                    
                    foreach(var l in list2)
                        Logger.logger.Log("Address: \n"+l.ToString());
                }
                if(list3.Count == 0)
                    Logger.logger.Log("List3 No Events!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                else
                {                    
                    foreach(var l in list3)
                        Logger.logger.Log("Address: \n"+l.ToString());
                }
                if(list4.Count == 0)
                    Logger.logger.Log("List4 No Events!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                else
                {                    
                    foreach(var l in list4)
                        Logger.logger.Log("Address: \n"+l.ToString());
                }

                Logger.logger.Log("Event counts validation");
                Logger.logger.Log("MethodTailFailedEvents: " + TailFailed);
                Logger.logger.Log("MethodTailSucceededEvents: " + TailSucceeded);
                bool MethodTailResult = TailFailed > 0 && TailSucceeded > 0;                
                
                Logger.logger.Log("MethodTailFailedEvents: " + InliningFailed);
                Logger.logger.Log("MethodTailSucceededEvents: " + InliningSucceeded);
                bool MethodInliningResult = InliningFailed > 0 && InliningSucceeded > 0;

                return MethodTailResult && MethodInliningResult ? 100 : -1;
            };
        };
    }
}