using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using ItsFrosti.SpeedrunningMod;
using UnityEngine.Networking;

internal static class Program
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Run(bool activate, bool abortThrows)
    {
        NativeHttp.Initialize();
        UnityWebRequest.ThrowOnAbort = abortThrows;
        int disposed = UnityWebRequest.Disposed;
        int completed = 0;
        Thread[] workers = new Thread[12];
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new Thread(() => {
                try { NativeHttp.Send("http://test.invalid", "GET", null); }
                catch (OperationCanceledException) { Interlocked.Increment(ref completed); }
            });
            workers[i].IsBackground = true;
            workers[i].Start();
        }
        object sync = typeof(NativeHttp).GetField("Sync", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        ICollection queue = (ICollection)typeof(NativeHttp).GetField("Waiting", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Stopwatch ready = Stopwatch.StartNew();
        while (true)
        {
            lock (sync) if (queue.Count == workers.Length) break;
            Check(ready.ElapsedMilliseconds < 2000, "Workers did not enqueue.");
            Thread.Yield();
        }
        if (activate) NativeHttp.Update();
        Stopwatch shutdown = Stopwatch.StartNew();
        NativeHttp.Shutdown();
        NativeHttp.Shutdown(); // Quitting and loader unload may both call this.
        foreach (Thread worker in workers) Check(worker.Join(1000), "Shutdown left a worker waiting for HTTP.");
        Check(completed == workers.Length, "Not every request was cancelled.");
        Check(shutdown.ElapsedMilliseconds < 1000, "Shutdown waited for a request timeout.");
        if (activate) Check(UnityWebRequest.Disposed - disposed == workers.Length, "Active requests were not disposed.");
        bool rejected = false;
        try { NativeHttp.Send("http://test.invalid", "GET", null); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "Transport accepted work after shutdown.");
        Console.WriteLine("PASS: " + (activate ? "active" : "queued") + " requests cancelled in " + shutdown.ElapsedMilliseconds + " ms; abortThrows=" + abortThrows);
    }
    public static void Main() { Run(false, false); Run(true, false); Run(true, true); }
}

// Native transport test double: no Unity player or real HTTP is used.
namespace UnityEngine.Networking
{
    public class DownloadHandlerBuffer { public string text = ""; }
    public class UploadHandlerRaw { public UploadHandlerRaw(byte[] value) { } }
    public class UnityWebRequest : IDisposable
    {
        public static bool ThrowOnAbort;
        public static int Disposed;
        public DownloadHandlerBuffer downloadHandler;
        public UploadHandlerRaw uploadHandler;
        public int timeout;
        public bool isDone = false;
        public bool isError = false;
        public long responseCode = 200;
        public string error = "";
        public UnityWebRequest(string url, string method) { }
        public void SetRequestHeader(string key, string value) { }
        public void Send() { }
        public void Abort() { if (ThrowOnAbort) throw new Exception("Native abort failed"); }
        public void Dispose() { Interlocked.Increment(ref Disposed); }
    }
}
