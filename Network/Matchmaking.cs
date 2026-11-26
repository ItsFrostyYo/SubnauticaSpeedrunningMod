using System;
using System.Threading;
using UnityEngine;
using ItsFrosti.SpeedrunningMod.Seeds;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

namespace ItsFrosti.SpeedrunningMod
{
    internal static class ModRankedMatchmakingRuntimeHost
    {
        private enum MatchmakingUiState
        {
            Idle,
            QueueStarting,
            QueueWaiting,
            MatchFound,
            Launching,
            Error
        }

        private static readonly object Sync = new object();
        private static int requestVersion;
        private static SNSRModLoader.PluginContext _context;
        private static bool _installed;
        private static MatchmakingUiState _state = MatchmakingUiState.Idle;
        private static string _queuedMode = string.Empty;
        private static string _ticketId = string.Empty;
        private static string _message = string.Empty;
        private static bool _requestInFlight;
        private static ModMatchmakingTicketDto _pendingTicket;
        private static ModMatchmakingTicketDto _currentTicket;
        private static string _pendingError = string.Empty;
        private static bool _hasPendingResponse;
        private static float _nextPollAt;
        private static bool _launchTriggered;

        internal static void Shutdown()
        {
            _installed = false;
            ResetState();
            _context = null;
        }

        public static void Install(SNSRModLoader.PluginContext context)
        {
            if (_installed)
            {
                return;
            }

            _context = context;
            ModClientIdentityStore.Initialize(context);
            _installed = true;
            Plugin.Logger.Info("Ranked matchmaking runtime installed.");
        }

        public static bool IsQueuePanelVisible
        {
            get
            {
                return _state == MatchmakingUiState.QueueStarting ||
                       _state == MatchmakingUiState.QueueWaiting ||
                       _state == MatchmakingUiState.MatchFound ||
                       _state == MatchmakingUiState.Launching ||
                       _state == MatchmakingUiState.Error;
            }
        }

        public static bool IsBusy
        {
            get { return _state != MatchmakingUiState.Idle; }
        }

        public static void ResetForMainMenu()
        {
            ResetState();
        }

        public static string GetPanelTitle()
        {
            if (string.IsNullOrEmpty(_queuedMode))
            {
                return "Waiting for Matchmaking";
            }

            return "Waiting for Matchmaking (" + _queuedMode + ")";
        }

        public static string GetPanelMessage()
        {
            return string.IsNullOrEmpty(_message) ? "Searching for opponent..." : _message;
        }

        public static void StartQueue(string mode)
        {
            if (!_installed || _context == null)
            {
                return;
            }

            if (!PluginSettings.EnableNetworking || !Plugin.Server.IsConfigured)
            {
                SetErrorState("Enable the server connection in Options > Speedrunning.");
                return;
            }

            if (_requestInFlight || _state == MatchmakingUiState.QueueWaiting || _state == MatchmakingUiState.QueueStarting)
            {
                return;
            }

            _queuedMode = string.IsNullOrEmpty(mode) ? "Creative" : mode;
            _ticketId = string.Empty;
            _message = "Joining " + _queuedMode + " matchmaking...";
            _state = MatchmakingUiState.QueueStarting;
            _launchTriggered = false;
            BeginBackgroundRequest(BackgroundOperation.Enqueue, _queuedMode, string.Empty);
        }

        public static void CancelQueue()
        {
            if (!_installed)
            {
                return;
            }

            string ticketId = _ticketId;
            string queuedMode = _queuedMode;
            ResetState();

            if (!string.IsNullOrEmpty(ticketId) && _context != null && Plugin.Server.IsConfigured)
            {
                BeginBackgroundRequest(BackgroundOperation.Cancel, queuedMode, ticketId);
            }
        }

        public static void Update()
        {
            if (!_installed || _context == null)
            {
                return;
            }

            ConsumePendingResponse();

            if ((_state == MatchmakingUiState.QueueStarting || _state == MatchmakingUiState.QueueWaiting) &&
                !_requestInFlight &&
                !string.IsNullOrEmpty(_ticketId) &&
                Time.unscaledTime >= _nextPollAt)
            {
                BeginBackgroundRequest(BackgroundOperation.Poll, _queuedMode, _ticketId);
            }

            if (_state == MatchmakingUiState.MatchFound && !_launchTriggered)
            {
                TryLaunchMatchedGame();
            }
        }

        private static void ConsumePendingResponse()
        {
            if (!_hasPendingResponse)
            {
                return;
            }

            ModMatchmakingTicketDto ticket;
            string pendingError;
            lock (Sync)
            {
                ticket = _pendingTicket;
                pendingError = _pendingError;
                _pendingTicket = null;
                _pendingError = string.Empty;
                _hasPendingResponse = false;
                _requestInFlight = false;
            }

            if (!string.IsNullOrEmpty(pendingError))
            {
                SetErrorState(pendingError);
                return;
            }

            if (ticket == null)
            {
                return;
            }

            _ticketId = ticket.ticketId ?? string.Empty;
            _queuedMode = string.IsNullOrEmpty(ticket.mode) ? _queuedMode : ticket.mode;
            _message = string.IsNullOrEmpty(ticket.message) ? "Searching for opponent..." : ticket.message;
            _currentTicket = ticket;

            string status = ticket.status ?? string.Empty;
            if (string.Equals(status, "Matched", StringComparison.OrdinalIgnoreCase) && ticket.match != null)
            {
                _state = MatchmakingUiState.MatchFound;
                _nextPollAt = 0f;
                return;
            }

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                ResetState();
                return;
            }

            _state = MatchmakingUiState.QueueWaiting;
            _nextPollAt = Time.unscaledTime + 1.25f;
        }

        private static void TryLaunchMatchedGame()
        {
            ModMatchmakingTicketDto ticket = GetLatestTicketSnapshot();
            if (ticket == null || ticket.match == null)
            {
                return;
            }

            GameMode gameMode;
            string mode = ticket.match.mode ?? string.Empty;
            if (string.Equals(mode, "Survival", StringComparison.OrdinalIgnoreCase)) gameMode = GameMode.Survival;
            else if (string.Equals(mode, "Creative", StringComparison.OrdinalIgnoreCase)) gameMode = GameMode.Creative;
            else if (string.Equals(mode, "Hardcore", StringComparison.OrdinalIgnoreCase)) gameMode = GameMode.Hardcore;
            else { SetErrorState("The server returned an unsupported race mode."); return; }
            if (!Ruleset8SeedPool.HasSeeds(gameMode))
            {
                SetErrorState(mode + " has no filtered race seeds available yet.");
                return;
            }
            if (!string.Equals(ticket.match.seedId, "Ruleset-8", StringComparison.Ordinal) ||
                !Ruleset8RaceSession.Prepare(ticket.match.seedValue, gameMode))
            {
                SetErrorState("The server must supply a Ruleset 8 seed from the filtered " + mode + " pool.");
                return;
            }
            _launchTriggered = true;
            _state = MatchmakingUiState.Launching;
            _message = "Match found. Launching " + mode + " ranked match...";

            ModClientSessionMode.ActivateRankedMultiplayerMatch(
                ticket.match.mode ?? "Creative",
                ticket.match.matchId ?? string.Empty,
                ticket.match.playerId ?? string.Empty,
                ticket.match.opponentPlayerId ?? string.Empty,
                ticket.match.opponentDisplayName ?? string.Empty,
                ticket.match.seedId ?? string.Empty,
                ticket.match.seedValue ?? string.Empty);

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                SetErrorState("Match found, but the main menu was not available to start the run.");
                return;
            }

            Plugin.StartGame(gameMode);
        }

        private static ModMatchmakingTicketDto GetLatestTicketSnapshot()
        {
            return _currentTicket;
        }

        private static void BeginBackgroundRequest(BackgroundOperation operation, string mode, string ticketId)
        {
            if (_requestInFlight || _context == null)
            {
                return;
            }

            _requestInFlight = true;
            int version = requestVersion;
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (Plugin.IsShuttingDown || version != requestVersion) return;
                ModMatchmakingTicketDto response = null;
                string error = string.Empty;

                try
                {
                    ModClientIdentity identity = ModClientIdentityStore.GetIdentity();
                    switch (operation)
                    {
                        case BackgroundOperation.Enqueue:
                            response = Plugin.Server.EnqueueMatchmaking(identity, mode);
                            break;
                        case BackgroundOperation.Cancel:
                            response = Plugin.Server.CancelMatchmaking(ticketId, identity.PlayerId);
                            break;
                        case BackgroundOperation.Poll:
                            response = Plugin.Server.GetMatchmakingTicket(ticketId);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (Plugin.IsShuttingDown) return;
                    error = ex.Message;
                    Plugin.Logger.Warning("Matchmaking request failed: " + ex.Message);
                }

                lock (Sync)
                {
                    if (Plugin.IsShuttingDown || version != requestVersion) return;
                    _pendingTicket = response;
                    _pendingError = error;
                    _hasPendingResponse = true;
                }
            });
        }

        private static void ResetState()
        {
            lock (Sync)
            {
                Interlocked.Increment(ref requestVersion);
                _state = MatchmakingUiState.Idle;
                _queuedMode = string.Empty;
                _ticketId = string.Empty;
                _message = string.Empty;
                _requestInFlight = false;
                _pendingTicket = null;
                _currentTicket = null;
                _pendingError = string.Empty;
                _hasPendingResponse = false;
                _nextPollAt = 0f;
                _launchTriggered = false;
            
            }
        }

        private static void SetErrorState(string message)
        {
            _state = MatchmakingUiState.Error;
            _message = string.IsNullOrEmpty(message) ? "Matchmaking failed." : message;
            _requestInFlight = false;
            _nextPollAt = 0f;
        }

        private enum BackgroundOperation
        {
            Enqueue,
            Poll,
            Cancel
        }
    }
}
