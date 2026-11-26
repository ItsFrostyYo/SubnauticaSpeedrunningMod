using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace ItsFrosti.SpeedrunningMod
{
    // Server workers wait here; Unity's own HTTP transport runs on the main
    // thread. No external bridge or process-wide TLS overrides are needed.
    internal static class NativeHttp
    {
        private sealed class Pending
        {
            internal string Url, Method, Body, Response;
            internal Exception Error;
            internal UnityWebRequest Request;
            internal readonly ManualResetEvent Done = new ManualResetEvent(false);
        }

        private static readonly object Sync = new object();
        private static readonly Queue<Pending> Waiting = new Queue<Pending>();
        private static readonly List<Pending> Active = new List<Pending>();
        private static bool stopped;

        internal static void Initialize()
        {
            lock (Sync) { stopped = false; }
        }

        internal static string Send(string url, string method, string body)
        {
            var pending = new Pending { Url = url, Method = method, Body = body };
            lock (Sync)
            {
                if (stopped)
                {
                    pending.Done.Close();
                    throw new OperationCanceledException("Plugin has unloaded.");
                }
                Waiting.Enqueue(pending);
            }
            pending.Done.WaitOne();
            pending.Done.Close();
            if (pending.Error != null) throw pending.Error;
            return pending.Response;
        }

        internal static void Update()
        {
            lock (Sync)
            {
                while (Waiting.Count > 0)
                {
                    var pending = Waiting.Dequeue();
                    try
                    {
                        var request = new UnityWebRequest(pending.Url, pending.Method);
                        pending.Request = request;
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.timeout = 75;
                        request.SetRequestHeader("Accept", "application/json");
                        if (pending.Body != null)
                        {
                            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(pending.Body));
                            request.SetRequestHeader("Content-Type", "application/json");
                        }
                        request.Send();
                        Active.Add(pending);
                    }
                    catch (Exception error) { Complete(pending, error); }
                }
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    var pending = Active[i];
                    if (!pending.Request.isDone) continue;
                    pending.Response = pending.Request.downloadHandler.text;
                    var error = pending.Request.isError || pending.Request.responseCode >= 400
                        ? new InvalidOperationException("Server request failed (" + pending.Request.responseCode + "): " +
                            (string.IsNullOrEmpty(pending.Response) ? pending.Request.error : pending.Response)) : null;
                    Active.RemoveAt(i);
                    Complete(pending, error);
                }
            }
        }

        private static void Complete(Pending pending, Exception error)
        {
            pending.Error = error;
            try { if (pending.Request != null) pending.Request.Dispose(); }
            catch (Exception disposeError) { if (pending.Error == null) pending.Error = disposeError; }
            finally { pending.Request = null; pending.Done.Set(); }
        }

        internal static void Shutdown()
        {
            lock (Sync)
            {
                stopped = true;
                var error = new OperationCanceledException("Plugin has unloaded.");
                while (Waiting.Count > 0) Complete(Waiting.Dequeue(), error);
                foreach (var pending in Active)
                {
                    try { pending.Request.Abort(); }
                    catch (Exception) { /* Still dispose and release the worker. */ }
                    finally { Complete(pending, error); }
                }
                Active.Clear();
            }
        }
    }
}
