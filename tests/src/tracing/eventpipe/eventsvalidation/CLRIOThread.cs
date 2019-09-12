// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;  
using System.Collections.Generic; 
using System.IO;  
using System.Text;  
using System.Threading;
using System.Threading.Tasks; 
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;

namespace Tracing.Tests.CLRIOThread
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

            ThreadPool.SetMaxThreads(1000,1000);
            for(int i=0; i<10; i++)
            {
                ProcessWriteAsync().Wait();
            }
            Console.WriteLine("Event generating method: _eventGeneratingAction end");
        };

        public async static Task ProcessWriteAsync()  
        {  
            string filePath = @"temp.txt";  
            string text = "Hello World\r\n";  
            
            await WriteTextAsync(filePath, text);
        }  
        
        private async static Task WriteTextAsync(string filePath, string text)  
        {  
            var tokenSource2 = new CancellationTokenSource();
            CancellationToken ct = tokenSource2.Token;

            byte[] encodedText = Encoding.Unicode.GetBytes(text);
        
            FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024, FileOptions.Asynchronous); 
 
            char[] chardata = new char[1024 * 1024];
 
            var sw = new StreamWriter(fileStream);

            await Task.Run(()=>{
                sw.WriteAsync(chardata, 0, chardata.Length);
                
                Thread.Sleep(10000);
                //System.Console.WriteLine(ct.IsCancellationRequested.ToString());
                //if (ct.IsCancellationRequested)
                //{
                //    ct.ThrowIfCancellationRequested();
                //}
            });//, ct);
            
            //stokenSource2.Cancel();             
        }   

        static void ThreadPoolMessage(string data)
        {
            int a, b;
            ThreadPool.GetAvailableThreads(out a, out b);
            string message = string.Format("{0}\n  CurrentThreadId is {1}\n  " +
                "WorkerThreads is:{2}  CompletionPortThreads is :{3}",
                data, Thread.CurrentThread.ManagedThreadId, a.ToString(), b.ToString());
            Console.WriteLine(message);
        }
        private static Func<EventPipeEventSource, Func<int>> _DoesTraceContainEvents = (source) => 
        {
            Console.WriteLine("Callback method: _DoesTraceContainEvents");
            int Event1 = 0;
            int Event2 = 0;
            source.Clr.IOThreadCreationStart += (eventData) => Event1 += 1;
            source.Clr.IOThreadCreationStop += (eventData) => Event2 += 1;
            
            int Event3 = 0;
            int Event4 = 0;
            source.Clr.IOThreadRetirementStart += (eventData) => Event3 += 1;
            source.Clr.IOThreadRetirementStop += (eventData) => Event4 += 1;

            return () => {
                Console.WriteLine("Event counts validation");
                Console.WriteLine("I/O thread start events: " + Event1);
                Console.WriteLine("I/O thread stop events: " + Event2);
                Console.WriteLine("I/O thread Retirement start events: " + Event3);
                //Console.WriteLine("Worker thread start events: " + Event3);
                Console.WriteLine("I/O thread Retirement stop events: " + Event4);
                return Event1 > 1 && Event2 > 0 ? 100 : -1;
            };
        };

    }

}