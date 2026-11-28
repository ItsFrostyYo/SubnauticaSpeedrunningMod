using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItsFrosti.SpeedrunningMod.Ui;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

namespace ItsFrosti.SpeedrunningMod
{
    internal static class ModRankedMatchmakingRuntimeHost
    {
        private enum Phase { Idle, Waiting, Found, Transitioning, Error }
        private enum Operation { Enqueue, Poll, Cancel, Snapshot }
        private static readonly object Sync = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly QueueTiming Timing = new QueueTiming();
        private static Phase phase;
        private static string mode = "", ticketId = "", message = "";
        private static ModMatchmakingTicketDto ticket;
        private static ModMatchStateDto assignment;
        private static bool installed, inFlight, cancelRequested, quitRequested, backgroundOverride, previousBackground;
        private static int version;
        private static double nextPoll;
        private static volatile bool hasPending;
        private static ModMatchmakingTicketDto pendingTicket;
        private static ModMatchStateDto pendingAssignment;
        private static string pendingError;
        private static long pendingStatus;
        private static Operation pendingOperation;
        private static double Now { get { return Clock.Elapsed.TotalSeconds; } }

        internal static void Install(SNSRModLoader.PluginContext context) { installed = true; }
        internal static void Shutdown() { installed = false; Reset(); }
        public static bool IsBusy { get { return phase != Phase.Idle; } }
        public static bool IsQueuePanelVisible { get { return IsBusy; } }
        public static bool CanCancel { get { return IsBusy && phase != Phase.Transitioning; } }
        internal static bool IsWorldTransitionPending { get { return phase == Phase.Transitioning; } }
        public static void ResetForMainMenu() { /* Queues survive world/menu transitions. */ }
        public static string GetPanelTitle() { return GetHudTitle(); }
        public static string GetPanelMessage() { return GetHudDetail() + (string.IsNullOrEmpty(message) ? "" : "\n" + message); }
        internal static string GetHudTitle()
        {
            return Timing.HasOpponent ? "Found Opponent, " + (string.IsNullOrEmpty(Timing.OpponentName) ? "Opponent" : Timing.OpponentName)
                : "In Queue for " + mode;
        }
        internal static string GetHudDetail()
        {
            if (phase == Phase.Error) return message;
            if (cancelRequested) return "Cancelling Queue";
            if (Timing.HasOpponent)
            {
                if (!Timing.ServerReady) return "Connecting to Server";
                int seconds = Timing.Countdown(Now);
                if (seconds > 0) return seconds.ToString();
                return assignment == null ? "Connecting to Server" : "Preparing Race";
            }
            return phase == Phase.Error ? message : Timing.Elapsed(Now);
        }

        public static void StartQueue(string category)
        {
            if (!installed || IsBusy) return;
            if (string.IsNullOrEmpty(RaceAccount.PlayerId)) { ErrorMessage.AddMessage("Sign in with Google in Options > Race first."); return; }
            if (ModPrivateRaceRoomRuntimeHost.IsRoomActive || !string.IsNullOrEmpty(ModClientSessionMode.RankedMultiplayerMatchId)) return;
            mode = category; phase = Phase.Waiting; Timing.Start(Now);
            ticketId = message = ""; ticket = null; assignment = null;
            cancelRequested = quitRequested = false; nextPoll = Now;
            previousBackground = Application.runInBackground; Application.runInBackground = true; backgroundOverride = true;
            Request(Operation.Enqueue);
        }
        public static void CancelQueue()
        {
            if (!CanCancel) return;
            if (string.IsNullOrEmpty(ticketId) && !inFlight) { Reset(); return; }
            cancelRequested = true;
            if (!inFlight && !string.IsNullOrEmpty(ticketId)) Request(Operation.Cancel);
        }
        public static void Update()
        {
            if (!installed || !IsBusy) return;
            ModOverlayRuntime.EnsureInstalled();
            Consume();
            if (!IsBusy) return;
            if (Timing.ConnectionTimedOut(Now) && phase != Phase.Error)
            {
                phase = Phase.Error;
                message = "Could not connect to the race server. Cancel the queue and try again.";
            }
            if (phase == Phase.Error && !cancelRequested) return;
            if (!inFlight && Now >= nextPoll)
            {
                if (cancelRequested && !string.IsNullOrEmpty(ticketId)) Request(Operation.Cancel);
                else if (ticket != null && ticket.match != null && assignment == null) Request(Operation.Snapshot);
                else if (!string.IsNullOrEmpty(ticketId)) Request(Operation.Poll);
                else if (string.IsNullOrEmpty(ticketId) && phase != Phase.Error) Request(Operation.Enqueue);
            }
            if (!cancelRequested && (phase == Phase.Found || phase == Phase.Transitioning) && Timing.ServerReady && assignment != null && Timing.Countdown(Now) == 0)
            {
                phase = Phase.Transitioning;
                TransitionToMatchedWorld();
            }
        }
        private static void TransitionToMatchedWorld()
        {
            // Do not touch the old world's seed while it is loading, saving, or unloading.
            bool loading = uGUI.main != null && uGUI.main.loading != null && uGUI.main.loading.IsLoading;
            if (loading || (SaveLoadManager.main != null && SaveLoadManager.main.isSaving)) return;
            bool atMenu = Player.main == null && uGUI_MainMenu.main != null &&
                uGUI_MainMenu.main.gameObject.activeInHierarchy &&
                !string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase) &&
                !Ruleset8RaceSession.IsStartingNewGame(uGUI_MainMenu.main);
            if (!atMenu)
            {
                if (!quitRequested && IngameMenu.main != null)
                {
                    quitRequested = true;
                    Plugin.Logger.Info("Queue preparation finished; unloading the current world through native QuitGame.");
                    IngameMenu.main.QuitGame(false);
                }
                return;
            }
            if (assignment == null) return;
            if (assignment.players == null || assignment.players.Length != 2 ||
                !string.Equals(assignment.status, "WaitingForReady", StringComparison.OrdinalIgnoreCase))
            { Reset(); ErrorMessage.AddMessage("Assigned race is no longer ready. Please queue again."); return; }
            var readyAssignment = assignment;
            // Release queue UI/background scope before starting the native new-game path.
            Reset();
            ModPrivateRaceRoomRuntimeHost.BeginQueuedRace(readyAssignment);
        }
        private static void Request(Operation operation)
        {
            if (inFlight) return;
            inFlight = true; int requestVersion = version;
            string id = ticketId, category = mode;
            string matchId = ticket == null || ticket.match == null ? "" : ticket.match.matchId;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ModMatchmakingTicketDto result = null; ModMatchStateDto snapshot = null;
                string error = ""; long status = 0;
                try
                {
                    if (Plugin.IsShuttingDown) return;
                    if (operation == Operation.Enqueue) result = Plugin.Server.EnqueueMatchmaking(ModClientIdentityStore.GetIdentity(), category);
                    else if (operation == Operation.Poll) result = Plugin.Server.GetMatchmakingTicket(id);
                    else if (operation == Operation.Cancel) result = Plugin.Server.CancelMatchmaking(id, RaceAccount.PlayerId);
                    else snapshot = Plugin.Server.GetMatchState(matchId);
                }
                catch (Exception ex)
                {
                    error = ex.Message; var http = ex as NativeHttpFailure; if (http != null) status = http.StatusCode;
                    Plugin.Logger.Warning("Queue request failed: " + error);
                }
                lock (Sync)
                {
                    if (Plugin.IsShuttingDown || requestVersion != version) return;
                    pendingTicket = result; pendingAssignment = snapshot; pendingError = error;
                    pendingStatus = status; pendingOperation = operation; hasPending = true;
                }
            });
        }
        private static void Consume()
        {
            if (!hasPending) return;
            ModMatchmakingTicketDto result; ModMatchStateDto snapshot; string error; long status; Operation operation;
            lock (Sync)
            {
                result = pendingTicket; snapshot = pendingAssignment; error = pendingError;
                status = pendingStatus; operation = pendingOperation; hasPending = false; inFlight = false;
            }
            nextPoll = Now + 1.25;
            if (!string.IsNullOrEmpty(error))
            {
                if (operation == Operation.Cancel && (status == 401 || status == 403 || status == 404))
                {
                    Reset();
                    ErrorMessage.AddMessage(error);
                    return;
                }
                if (operation == Operation.Cancel && status == 409) { cancelRequested = false; nextPoll = Now; return; }
                if (operation == Operation.Snapshot && status == 404)
                {
                    Plugin.Logger.Warning("Discarding missing race assignment and requesting a fresh queue ticket.");
                    StartFreshTicket();
                    return;
                }
                message = Timing.HasOpponent ? "Connecting to Server" : "Reconnecting to queue...";
                if (status == 401 || status == 403 || status == 404) { message = error; phase = Phase.Error; }
                return;
            }
            message = "";
            if (snapshot != null)
            {
                if (snapshot.players == null || snapshot.players.Length != 2 ||
                    !string.Equals(snapshot.status, "WaitingForReady", StringComparison.OrdinalIgnoreCase))
                {
                    phase = Phase.Error;
                    message = "Assigned race is no longer available. Cancel the queue and try again.";
                    return;
                }
                assignment = snapshot;
                Timing.Connected(Now);
                Plugin.Logger.Info("Queue race server connected; preparation countdown: " + Timing.Countdown(Now) + " seconds.");
                return;
            }
            if (result == null) return;
            ticket = result; ticketId = result.ticketId;
            if (string.Equals(result.status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                string reason = result.message;
                bool requested = cancelRequested;
                Reset();
                if (!requested && !string.IsNullOrEmpty(reason)) ErrorMessage.AddMessage(reason);
                return;
            }
            bool paired = string.Equals(result.status, "Provisioning", StringComparison.OrdinalIgnoreCase) || string.Equals(result.status, "Matched", StringComparison.OrdinalIgnoreCase);
            if (paired)
            {
                string name = result.match == null ? result.opponentDisplayName : result.match.opponentDisplayName;
                Timing.Found(name, Now); cancelRequested = false;
                if (phase != Phase.Transitioning) phase = Phase.Found;
            }
            else if (!Timing.HasOpponent) phase = Phase.Waiting;
        }
        private static void StartFreshTicket()
        {
            // The gateway releases a missing assignment before returning 404.
            // Keep the selected category, but never reuse its old local deadline.
            ticketId = message = ""; ticket = null; assignment = null;
            cancelRequested = quitRequested = false; phase = Phase.Waiting;
            Timing.Start(Now); nextPoll = Now;
        }
        private static void Reset()
        {
            lock (Sync)
            {
                version++; phase = Phase.Idle; inFlight = hasPending = cancelRequested = quitRequested = false;
                ticketId = mode = message = ""; ticket = null; assignment = null;
                pendingTicket = null; pendingAssignment = null; pendingError = "";
            }
            if (backgroundOverride) { Application.runInBackground = previousBackground; backgroundOverride = false; }
        }
    }
}
