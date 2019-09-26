// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.IOThread
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
        
        static string filePath = Path.Combine(Path.GetTempPath(), "Temp.txt");
        private static Action _eventGeneratingAction = () => 
        {
            for(int i=0; i<50; i++)
            {
                if (i % 10 == 0)
                    Logger.logger.Log($"Create file stream {i} times...");

                FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024, true);
                byte[] bytes= new byte[1024 * 1024];
                
                fileStream.BeginWrite(bytes, 0, bytes.Length, new AsyncCallback(asyncCallback), fileStream);
                Thread.Sleep(1000);
            }
            Thread.Sleep(50000);
        };
        static void asyncCallback(IAsyncResult result)
        {
            FileStream fileStream = (FileStream)result.AsyncState;
            fileStream.EndWrite(result);
            fileStream.Close();
            File.Delete(filePath);
        } 

        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            int IOThreadCreationStartEvents = 0;
            int TIOThreadCreationStopEvents = 0;
            source.Clr.IOThreadCreationStart += (eventData) => IOThreadCreationStartEvents += 1;
            source.Clr.IOThreadCreationStop += (eventData) => TIOThreadCreationStopEvents += 1;

            return () => {
                Logger.logger.Log("Event counts validation");

                Logger.logger.Log("IOThreadCreationStartEvents: " + IOThreadCreationStartEvents);
                Logger.logger.Log("TIOThreadCreationStopEvents: " + TIOThreadCreationStopEvents);
                bool IOThreadCreationStartStopResult = IOThreadCreationStartEvents >= 1 && TIOThreadCreationStopEvents >= 1;
                Logger.logger.Log("IOThreadCreationStartStopResult check: " + IOThreadCreationStartStopResult);

                return IOThreadCreationStartStopResult ? 100 : -1;
            };
        };
    }
}