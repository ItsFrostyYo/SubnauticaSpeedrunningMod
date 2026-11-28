using System;
using System.IO;
using System.Text;
using System.Threading;
using LitJson;
using SNSRModLoader;
using UnityEngine;
using UnityEngine.UI;
namespace ItsFrosti.SpeedrunningMod
{
    internal static class RaceAccount
    {
        private static readonly object Sync = new object(), RefreshSync = new object();
        private static string path, access = "", refresh = "", playerId = "", displayName = "", deviceId = "", deviceSecret = "", openUrl = "", status = "Signed out";
        private static long expires; private static int busy, epoch; private static float nextPoll; private static DateTime deviceExpiry;
        private static Text statusText;
        private static bool backgroundOverride, previousRunInBackground;
        private static string lastFailure = "";

        private static string WaitingMessage(string id)
        {
            return "Finish sign-in in browser. Confirm matching code: " + id.Substring(id.Length - 6).ToUpperInvariant();
        }
        private static void BeginBrowserWait()
        {
            if (backgroundOverride) return;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            backgroundOverride = true;
        }
        private static void EndBrowserWait()
        {
            if (!backgroundOverride) return;
            Application.runInBackground = previousRunInBackground;
            backgroundOverride = false;
        }
        private static void LogFailure(string operation, Exception error)
        {
            var http = error as NativeHttpFailure;
            string summary = operation + ": " + error.GetType().Name + (http == null ? "" : " HTTP " + http.StatusCode);
            if (summary == lastFailure) return;
            lastFailure = summary;
            Plugin.Logger.Warning("Account " + summary + ". No session data logged.");
        }
        internal static bool Enabled { get { return true; } }
        internal static string PlayerId { get { lock (Sync) return playerId; } }
        internal static string DisplayName { get { lock (Sync) return displayName; } }
        internal static void Initialize(PluginContext context)
        {
            path = context.GetSettingsPath("account-session.dat");
            if (!Enabled || !File.Exists(path)) return;
            try { var clear = SessionProtection.Unprotect(File.ReadAllBytes(path), Encoding.UTF8.GetBytes(Plugin.PluginId)); Apply(JsonMapper.ToObject(Encoding.UTF8.GetString(clear)), false); }
            catch { status = "Stored session unavailable. Sign in again."; }
        }
        private static string Read(JsonData data, string name) { return data != null && data.IsObject && data.Keys.Contains(name) && data[name] != null ? data[name].ToString() : ""; }
        private static long Expiry(string token)
        {
            try { var value = token.Split('.')[1].Replace('-', '+').Replace('_', '/'); value = value.PadRight((value.Length + 3) / 4 * 4, '='); var data = JsonMapper.ToObject(Encoding.UTF8.GetString(Convert.FromBase64String(value))); long result; return long.TryParse(Read(data, "exp"), out result) ? result : 0; } catch { return 0; }
        }
        private static void Apply(JsonData data, bool save)
        {
            string nextAccess = Read(data, "accessToken"), nextRefresh = Read(data, "refreshToken");
            string nextPlayer = Read(data, "playerId"), nextName = Read(data, "displayName");
            if (nextAccess.Length == 0 || nextRefresh.Length == 0 || nextPlayer.Length == 0)
                throw new InvalidOperationException("Account service returned an incomplete session.");
            bool saved = true;
            if (save)
            {
                try
                {
                    var bytes = SessionProtection.Protect(Encoding.UTF8.GetBytes(data.ToJson()), Encoding.UTF8.GetBytes(Plugin.PluginId));
                    string temporary = path + ".tmp";
                    File.WriteAllBytes(temporary, bytes);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                catch (Exception error) { saved = false; LogFailure("session persistence", error); }
            }
            lock (Sync)
            {
                access = nextAccess; refresh = nextRefresh; playerId = nextPlayer; displayName = nextName;
                expires = Expiry(access);
                status = "Signed in as " + displayName + (saved ? "" : " (this game session only)");
            }
        }
        private static string Url(string relative) { return RaceService.ApiUrl.TrimEnd('/') + relative; }
        internal static string Bearer()
        {
            if (!Enabled) return null;
            lock (RefreshSync)
            {
                string currentRefresh; int version;
                lock (Sync)
                {
                    if (string.IsNullOrEmpty(refresh)) throw new InvalidOperationException("Sign in with Google in Options > Race first.");
                    if (expires > (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds + 120) return access;
                    currentRefresh = refresh; version = epoch;
                }
                // Never hold the UI state lock while waiting for the main-thread HTTP transport.
                var data = JsonMapper.ToObject(NativeHttp.Send(Url("/api/auth/refresh"), "POST", AccountProtocol.StringRequest("refreshToken", currentRefresh)));
                lock (Sync) { if (version != epoch) throw new OperationCanceledException("Account changed."); Apply(data, true); return access; }
            }
        }
        internal static void SignIn()
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            if (ModPrivateRaceRoomRuntimeHost.IsConnected || ModRankedMatchmakingRuntimeHost.IsBusy || !string.IsNullOrEmpty(ModClientSessionMode.RankedMultiplayerMatchId))
            { status = "Leave your race, room or queue before signing in."; busy = 0; return; }
            BeginBrowserWait();
            var version = Interlocked.Increment(ref epoch);
            lock (Sync) { deviceId = deviceSecret = ""; status = "Opening Google sign-in in browser..."; lastFailure = ""; }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var response = JsonMapper.ToObject(NativeHttp.Send(Url("/api/auth/device/start"), "POST", "{}"));
                    string id = Read(response, "deviceId"), secret = Read(response, "deviceSecret");
                    if (id.Length != 36 || secret.Length == 0) throw new InvalidOperationException("Invalid sign-in response.");
                    lock (Sync)
                    {
                        if (version != epoch) return;
                        deviceId = id; deviceSecret = secret; openUrl = Read(response, "verificationUrl");
                        deviceExpiry = DateTime.UtcNow.AddMinutes(10); status = WaitingMessage(id);
                    }
                }
                catch (Exception error)
                {
                    LogFailure("start", error);
                    lock (Sync) if (version == epoch) { status = "Could not start sign-in. Please retry."; deviceId = deviceSecret = ""; }
                }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }
        internal static void AddOptions(uGUI_OptionsPanel panel, int tab)
        {
            var label = "Account: " + status; panel.AddHeading(tab, label);
            foreach (var text in panel.GetComponentsInChildren<Text>(true)) if (text.text == label) statusText = text;
            panel.AddButton(tab, "Sign in with Google", SignIn);
            panel.AddButton(tab, "Manage account", delegate { Application.OpenURL(Url("/account")); });
            panel.AddButton(tab, "Sign out", SignOut);
        }
        private static void SignOut()
        {
            if (ModPrivateRaceRoomRuntimeHost.IsConnected || ModRankedMatchmakingRuntimeHost.IsBusy || !string.IsNullOrEmpty(ModClientSessionMode.RankedMultiplayerMatchId)) { status = "Leave the race, room or queue before signing out."; return; }
            EndBrowserWait();
            lock (Sync) { epoch++; access = refresh = playerId = displayName = deviceId = deviceSecret = openUrl = ""; expires = 0; status = "Signed out"; if (File.Exists(path)) File.Delete(path); }
        }
        internal static void Update()
        {
            if (statusText != null) statusText.text = "Account: " + status;
            string url, id, secret; int version;
            lock (Sync) { url = openUrl; openUrl = ""; id = deviceId; secret = deviceSecret; version = epoch; }
            if (url.Length > 0)
            {
                Uri uri, origin;
                if (Uri.TryCreate(url, UriKind.Absolute, out uri) && Uri.TryCreate(RaceService.ApiUrl, UriKind.Absolute, out origin) && uri.Scheme == origin.Scheme && uri.Authority == origin.Authority)
                    Application.OpenURL(url);
            }
            if (id.Length == 0)
            {
                if (Interlocked.CompareExchange(ref busy, 0, 0) == 0) EndBrowserWait();
                return;
            }
            if (Time.realtimeSinceStartup < nextPoll) return;
            if (DateTime.UtcNow > deviceExpiry)
            {
                lock (Sync) { deviceId = deviceSecret = ""; status = "Sign-in expired. Click Sign in with Google again."; }
                return;
            }
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            nextPoll = Time.realtimeSinceStartup + 3;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string payload = AccountProtocol.StringRequest("deviceSecret", secret);
                try
                {
                    var data = JsonMapper.ToObject(NativeHttp.Send(Url("/api/auth/device/" + id + "/poll"), "POST", payload));
                    bool approved = Read(data, "status") == "Approved";
                    lock (Sync)
                    {
                        if (version != epoch) return;
                        if (approved) { Apply(data, true); deviceId = deviceSecret = ""; }
                        else status = WaitingMessage(id);
                    }
                    if (approved)
                    {
                        Plugin.Logger.Info("Account sign-in completed. Session contents are not logged.");
                        try { NativeHttp.Send(Url("/api/auth/device/" + id + "/ack"), "POST", payload); }
                        catch (Exception error) { LogFailure("acknowledgement", error); }
                    }
                }
                catch (Exception error)
                {
                    LogFailure("poll", error);
                    var http = error as NativeHttpFailure;
                    lock (Sync)
                    {
                        if (version != epoch) return;
                        if (http != null && (http.StatusCode == 410 || http.StatusCode == 401))
                        { deviceId = deviceSecret = ""; status = "Sign-in expired. Click Sign in with Google again."; }
                        else status = WaitingMessage(id) + " (retrying connection)";
                    }
                }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }
        internal static void Shutdown() { EndBrowserWait(); lock (Sync) { epoch++; access = refresh = playerId = displayName = deviceId = deviceSecret = openUrl = ""; } statusText = null; }
    }
}
