namespace ItsFrosti.SpeedrunningMod
{
    internal enum ModClientLaunchMode
    {
        Vanilla,
        ModMultiplayer,
        ModSingleplayerPractice,
        ModBetterRngSingleplayer
    }

    internal static class ModClientSessionMode
    {
        private static ModClientLaunchMode _launchMode = ModClientLaunchMode.Vanilla;
        private static string _multiplayerMode = string.Empty;
        private static string _multiplayerMatchId = string.Empty;
        private static string _multiplayerPlayerId = string.Empty;
        private static string _multiplayerOpponentPlayerId = string.Empty;
        private static string _multiplayerOpponentDisplayName = string.Empty;
        private static string _multiplayerSeedId = string.Empty;
        private static string _multiplayerSeedValue = string.Empty;
        private static bool _privateRaceLocalSessionActive;
        public static bool IsRandomQueueRace { get; private set; }
        internal static void SetRandomQueueRace(bool value) { IsRandomQueueRace = value; }
        private static bool _privateRaceLocalHost;
        private static string _privateRaceLocalDisplayName = string.Empty;

        public static ModClientLaunchMode LaunchMode
        {
            get { return _launchMode; }
        }

        public static bool IsRankedSingleplayerPracticeSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModSingleplayerPractice; }
        }

        public static bool IsRankedMultiplayerSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModMultiplayer; }
        }

        public static bool IsBetterRngSingleplayerSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModBetterRngSingleplayer; }
        }

        public static string RankedMultiplayerMode
        {
            get { return _multiplayerMode; }
        }

        public static string RankedMultiplayerMatchId
        {
            get { return _multiplayerMatchId; }
        }

        public static string RankedMultiplayerPlayerId
        {
            get { return _multiplayerPlayerId; }
        }

        public static string RankedMultiplayerOpponentPlayerId
        {
            get { return _multiplayerOpponentPlayerId; }
        }

        public static string RankedMultiplayerOpponentDisplayName
        {
            get { return _multiplayerOpponentDisplayName; }
        }

        public static string RankedMultiplayerSeedId
        {
            get { return _multiplayerSeedId; }
        }

        public static string RankedMultiplayerSeedValue
        {
            get { return _multiplayerSeedValue; }
        }

        public static bool IsPrivateRaceLocalSessionActive
        {
            get { return _privateRaceLocalSessionActive; }
        }

        public static bool IsPrivateRaceLocalHost
        {
            get { return _privateRaceLocalHost; }
        }

        public static string PrivateRaceLocalDisplayName
        {
            get { return _privateRaceLocalDisplayName; }
        }

        public static void SelectVanilla()
        {
            SetLaunchMode(ModClientLaunchMode.Vanilla, "Vanilla selected");
            ClearMultiplayerSelection();
        }

        public static void SelectRankedMultiplayer()
        {
            SetLaunchMode(ModClientLaunchMode.ModMultiplayer, "Multiplayer race selected");
            ClearMultiplayerSelection();
        }

        public static void SelectRankedMultiplayer(string mode)
        {
            SetLaunchMode(ModClientLaunchMode.ModMultiplayer, "Multiplayer race mode selected: " + (mode ?? string.Empty));
            _multiplayerMode = mode ?? string.Empty;
        }

        public static void ActivateRankedMultiplayerMatch(
            string mode,
            string matchId,
            string playerId,
            string opponentPlayerId,
            string opponentDisplayName,
            string seedId,
            string seedValue)
        {
            SetLaunchMode(ModClientLaunchMode.ModMultiplayer, "Server match activated: " + (matchId ?? string.Empty));
            _multiplayerMode = mode ?? string.Empty;
            _multiplayerMatchId = matchId ?? string.Empty;
            _multiplayerPlayerId = playerId ?? string.Empty;
            _multiplayerOpponentPlayerId = opponentPlayerId ?? string.Empty;
            _multiplayerOpponentDisplayName = opponentDisplayName ?? string.Empty;
            _multiplayerSeedId = seedId ?? string.Empty;
            _multiplayerSeedValue = seedValue ?? string.Empty;
        }

        public static void AttachPrivateRaceLocalRoom(string displayName, bool isHost)
        {
            _privateRaceLocalSessionActive = true;
            _privateRaceLocalHost = isHost;
            _privateRaceLocalDisplayName = displayName ?? string.Empty;
        }

        public static void ActivateLocalPrivateRace(
            string mode,
            string displayName,
            string seedId,
            string seedValue)
        {
            SetLaunchMode(ModClientLaunchMode.ModMultiplayer, "Local private race activated");
            _multiplayerMode = mode ?? string.Empty;
            _multiplayerMatchId = "local-private-race";
            _multiplayerPlayerId = "local-host";
            _multiplayerOpponentPlayerId = "pending-joiner";
            _multiplayerOpponentDisplayName = "Waiting for Opponent";
            _multiplayerSeedId = seedId ?? string.Empty;
            _multiplayerSeedValue = seedValue ?? string.Empty;
            _privateRaceLocalSessionActive = true;
            _privateRaceLocalHost = true;
            _privateRaceLocalDisplayName = displayName ?? string.Empty;
        }

        public static void SelectRankedSingleplayerPractice()
        {
            SetLaunchMode(ModClientLaunchMode.ModSingleplayerPractice, "Singleplayer Race Practice selected");
            ClearMultiplayerSelection();
        }

        public static void SelectBetterRngSingleplayer()
        {
            SetLaunchMode(ModClientLaunchMode.ModBetterRngSingleplayer, "BetterRNG singleplayer selected");
            ClearMultiplayerSelection();
        }

        public static void ResetForMainMenu()
        {
            SetLaunchMode(ModClientLaunchMode.Vanilla, "Returned to main menu");
            ClearMultiplayerSelection();
        }

        private static void SetLaunchMode(ModClientLaunchMode mode, string reason)
        {
            ModClientLaunchMode previous = _launchMode;
            _launchMode = mode;
            if (previous != mode)
            {
                Plugin.Logger.Info("Client launch mode changed: " + previous + " -> " + mode + ". " + (reason ?? string.Empty));
            }
        }

        private static void ClearMultiplayerSelection()
        {
            _multiplayerMode = string.Empty;
            _multiplayerMatchId = string.Empty;
            _multiplayerPlayerId = string.Empty;
            _multiplayerOpponentPlayerId = string.Empty;
            _multiplayerOpponentDisplayName = string.Empty;
            _multiplayerSeedId = string.Empty;
            _multiplayerSeedValue = string.Empty;
            IsRandomQueueRace = false;
            _privateRaceLocalSessionActive = false;
            _privateRaceLocalHost = false;
            _privateRaceLocalDisplayName = string.Empty;
        }
    }
}
