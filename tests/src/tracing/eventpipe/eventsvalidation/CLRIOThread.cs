// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading; 
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRIOTHreadV
{
    public class ProviderValidation
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EventPipe validation test");
            var providers = new List<Provider>()
            {
                new Provider("Microsoft-DotNETCore-SampleProfiler"),
                new Provider("Microsoft-Windows-DotNETRuntime", 0b10000_0000_0000_0000, EventLevel.Informational)
            };            

            var configuration = new SessionConfiguration(circularBufferSizeMB: 1024, format: EventPipeSerializationFormat.NetTrace,  providers: providers);
            Console.WriteLine("Validation method: RunAndValidateEventCounts");
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
            Console.WriteLine("Event generating method: _eventGeneratingAction start");

            for(int i=0; i<10; i++)
            {
                FileStream fileStream = new FileStream("Temp.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024, true);
                byte[] bytes= new byte[1024 * 1024];
                
                fileStream.BeginWrite(bytes, 0, bytes.Length, new AsyncCallback(asyncCallback), fileStream);
                Thread.Sleep(10000);
            }
            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };
        static void asyncCallback(IAsyncResult result)
        {
            FileStream fileStream = (FileStream)result.AsyncState;
            fileStream.EndWrite(result);
            System.Console.WriteLine(result.AsyncState.ToString());
            fileStream.Close();
        } 
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            int Event2 = 0;
            source.Clr.IOThreadCreationStart += (eventData) => Event1 += 1;
            source.Clr.IOThreadCreationStop += (eventData) => Event2 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("I/O thread start events: " + Event1);
                Console.WriteLine("I/O thread stop events: " + Event2);
                return Event1 > 1 && Event2 > 1 ? 100 : -1;
            };
        };

    }

    internal class AsyncHelper
    {
        WaitCallback callback;
        object state;

        internal AsyncHelper(WaitCallback callback, object state)
        {
            this.callback = callback;
            this.state = state;
        }

        unsafe internal void Callback(uint errorCode, uint numBytes, NativeOverlapped* _overlapped)
        {
            try
            {
                this.callback(this.state);
            }
            finally
            {                
                System.Console.WriteLine("111");
                Overlapped.Free(_overlapped);
            }
        }
    }

}