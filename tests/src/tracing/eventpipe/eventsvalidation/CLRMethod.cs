// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Runtime.InteropServices;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRMethod
{
    public class ProviderValidation
    { 
        static string assemblyPath=null;
        static void GetAssemblyPath()
        {  
            assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            System.Console.WriteLine(assemblyPath);
        }
        public static int Main(string[] args)
        {
            Logger.logger.Log("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b1_0000, EventLevel.Informational)
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
            GetAssemblyPath();
            Assembly assembly = Assembly.LoadFile(assemblyPath+"\\SimAssembly.dll"); 

            Object obj = assembly.CreateInstance("SimAssembly.ClassAss");
            if(obj != null)
            {
                MethodInfo method = obj.GetType().GetMethod("GetValue");
                object[] par = {5};

                if(method != null)
                {
                    object returnValue = method.Invoke(obj, new object[]{100});
                    System.Console.WriteLine(returnValue);
                }
            }
            else
            {
                System.Console.WriteLine("Failed!!!!");
            }
            
            Logger.logger.Log("Event generating method: _eventGeneratingAction end");
        }; 
        
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Logger.logger.Log("Callback method: _DoesTraceContainEvents");
            int MethodLoadEvents = 0;
            int MethodUnloadEvents = 0;
            source.Clr.MethodLoad += (eventData) => MethodLoadEvents += 1;
            source.Clr.MethodUnload += (eventData) => MethodUnloadEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");
                Logger.logger.Log("MethodLoadEvents: " + MethodLoadEvents);
                Logger.logger.Log("MethodUnloadEvents: " + MethodUnloadEvents);
                bool MethodLoaderResult = MethodLoadEvents > 0 && MethodUnloadEvents > 0;
                Logger.logger.Log("MethodLoaderResult: " + MethodLoaderResult);

                return MethodLoaderResult ? 100 : -1;
            };
        };
    }
}