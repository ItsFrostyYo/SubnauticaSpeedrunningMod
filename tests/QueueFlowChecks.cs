namespace SNSRModLoader { public class PluginContext { } }
namespace UnityEngine
{
    public class GameObject { public bool activeInHierarchy = true; }
    public static class Application { public static bool runInBackground; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public static class SceneManager { public static Scene GetActiveScene() { return new Scene {name = "XMenu"}; } }
}
namespace ItsFrosti.SpeedrunningMod.Seeds.Ruleset8
{ public static class Ruleset8RaceSession { public static bool IsStartingNewGame(uGUI_MainMenu menu) { return false; } } }
namespace ItsFrosti.SpeedrunningMod.Ui
{ public static class ModOverlayRuntime { public static void EnsureInstalled() { } } }
public class Player { public static Player main; }
public class uGUI_MainMenu { public static uGUI_MainMenu main = new uGUI_MainMenu(); public UnityEngine.GameObject gameObject = new UnityEngine.GameObject(); }
public class uGUI { public static uGUI main; public Loading loading; public class Loading { public bool IsLoading; } }
public class SaveLoadManager { public static SaveLoadManager main; public bool isSaving; }
public class IngameMenu { public static IngameMenu main = new IngameMenu(); public static int Quits; public void QuitGame(bool desktop) { Quits++; } }
public static class ErrorMessage { public static void AddMessage(string value) { } }
namespace ItsFrosti.SpeedrunningMod
{
    public class NativeHttpFailure : System.Exception { public long StatusCode; public NativeHttpFailure(long status) : base("HTTP " + status) { StatusCode = status; } }
    public static class Plugin { public static bool IsShuttingDown; public static FakeServer Server = new FakeServer(); public static FakeLog Logger = new FakeLog(); }
    public class FakeLog { public void Info(string value) { } public void Warning(string value) { } }
    public static class RaceAccount { public static string PlayerId = "alice"; }
    public static class ModClientIdentityStore { public static object GetIdentity() { return null; } }
    public static class ModClientSessionMode { public static string RankedMultiplayerMatchId = ""; }
    public static class ModPrivateRaceRoomRuntimeHost
    {
        public static bool IsRoomActive;
        public static int Starts;
        public static void BeginQueuedRace(ModMatchStateDto state) { Starts++; }
    }
    public class ModMatchStateDto { public string status; public object[] players; }
    public class ModMatchmakingAssignmentDto { public string matchId = "assigned", opponentDisplayName = "Bob"; }
    public class ModMatchmakingTicketDto { public string ticketId = "ticket", status, opponentDisplayName = "Bob", message; public ModMatchmakingAssignmentDto match; }
    public class FakeServer
    {
        public int Enqueues, Snapshots;
        public long SnapshotError;
        public bool FreshWaiting;
        public ModMatchmakingTicketDto EnqueueMatchmaking(object identity, string mode)
        { Enqueues++; return new ModMatchmakingTicketDto { status = FreshWaiting ? "Waiting" : "Provisioning" }; }
        public ModMatchmakingTicketDto GetMatchmakingTicket(string id)
        { return new ModMatchmakingTicketDto {status = "Matched", match = new ModMatchmakingAssignmentDto()}; }
        public ModMatchmakingTicketDto CancelMatchmaking(string id, string player)
        { return new ModMatchmakingTicketDto {status = "Cancelled"}; }
        public ModMatchStateDto GetMatchState(string id)
        {
            Snapshots++;
            if (SnapshotError != 0) { FreshWaiting = true; throw new NativeHttpFailure(SnapshotError); }
            return new ModMatchStateDto {status = "WaitingForReady", players = new object[2]};
        }
    }
}
public static class QueueFlowChecks
{
    public static double Now;
    private static readonly System.Type Host = typeof(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost);
    private static bool Pending { get { return (bool)Host.GetField("hasPending", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null); } }
    private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); }
    private static void Response()
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!Pending && timeout.ElapsedMilliseconds < 2000) System.Threading.Thread.Sleep(1);
        Check(Pending, "request did not complete");
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Update();
    }
    private static void Begin(double now, long error)
    {
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Shutdown();
        ItsFrosti.SpeedrunningMod.Plugin.Server = new ItsFrosti.SpeedrunningMod.FakeServer { SnapshotError = error };
        ItsFrosti.SpeedrunningMod.ModPrivateRaceRoomRuntimeHost.Starts = IngameMenu.Quits = 0;
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Install(null);
        Now = now;
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.StartQueue("Survival");
        Response();
    }
    private static void Poll(double time)
    { Now = time; ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Update(); Response(); }
    public static void Run()
    {
        Begin(100, 0);
        Check(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.GetHudDetail() == "Connecting to Server", "countdown before connection");
        Poll(102); // Ticket now has a match id.
        Poll(104); // Confirm live server match; start countdown here.
        Check(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.GetHudDetail() == "5", "countdown did not follow connection");
        Check(IngameMenu.Quits == 0 && ItsFrosti.SpeedrunningMod.ModPrivateRaceRoomRuntimeHost.Starts == 0, "world changed before countdown");
        Player.main = new Player();
        Poll(109);
        Check(IngameMenu.Quits == 1 && ItsFrosti.SpeedrunningMod.ModPrivateRaceRoomRuntimeHost.Starts == 0, "native quit handoff missing");
        Player.main = null;
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Update();
        Check(ItsFrosti.SpeedrunningMod.ModPrivateRaceRoomRuntimeHost.Starts == 1, "race did not launch after return to menu");
        Begin(200, 0);
        Poll(202); Poll(206);
        Check(ItsFrosti.SpeedrunningMod.ModPrivateRaceRoomRuntimeHost.Starts == 1, "slow connection added a countdown");
        Begin(300, 404);
        Poll(302); Poll(304);
        Response(); // Fresh enqueue was issued immediately after the missing snapshot.
        Check(ItsFrosti.SpeedrunningMod.Plugin.Server.Enqueues == 2, "missing assignment reused old ticket");
        Check(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.GetHudTitle().StartsWith("In Queue for"), "stale opponent survived reset");
        Begin(400, 403);
        Poll(402); Poll(404);
        Check(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.GetHudDetail() == "HTTP 403", "terminal error hidden by connecting label");
        Check(ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.CanCancel, "failed connection could not be cancelled");
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.CancelQueue(); Response();
        Check(!ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.IsBusy, "cancelled ticket remained busy");
        ItsFrosti.SpeedrunningMod.ModRankedMatchmakingRuntimeHost.Shutdown();
    }
}
