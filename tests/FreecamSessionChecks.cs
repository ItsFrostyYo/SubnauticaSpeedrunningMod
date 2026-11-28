// Stand-ins for engine boundaries. Tests execute the actual FreecamSession code;
// native Unity rendering/input still needs a manual in-game check.
namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(System.Type type, string name) { }
    }
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(System.Type type, string name)
        { return type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance); }
        public static System.Reflection.FieldInfo Field(System.Type type, string name)
        { return type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance); }
    }
}
namespace UnityEngine
{
    public class Object
    {
        public static object Found;
        public static T FindObjectOfType<T>() where T : class { return Found as T; }
    }
    public class GameObject { public bool activeSelf = true; public bool activeInHierarchy; }
    public static class Time { public static float timeScale = 1, realtimeSinceStartup; public static int frameCount; }
    public enum KeyCode { Escape }
    public static class Input { public static bool Escape; public static bool GetKeyDown(KeyCode key) { return Escape; } }
}
namespace UWE
{
    public static class FreezeTime
    {
        private static float oldTimeScale = 1;
        private static readonly System.Collections.Generic.HashSet<string> Owners = new System.Collections.Generic.HashSet<string>();
        public static void Begin(string owner, bool dontPauseSound = false)
        {
            if (Owners.Count == 0) oldTimeScale = UnityEngine.Time.timeScale;
            Owners.Add(owner); UnityEngine.Time.timeScale = 0;
        }
        public static void End(string owner)
        { if (Owners.Remove(owner) && Owners.Count == 0) UnityEngine.Time.timeScale = oldTimeScale; }
    }
}
public class FreecamController
{
    private bool mode;
    private float speed = 0.4f, t = 0;
    public int Toggles;
    public bool GetActive() { return mode; }
    private void FreecamToggle() { mode = !mode; Toggles++; }
    public float Speed { get { return speed; } }
    public float LastUpdate { get { return t; } }
}
public class Player { public static Player main; }
public static class LaunchRocket { public static bool isLaunching; }
public static class GameInput
{
    public enum Button { UIMenu }
    public static bool Menu;
    public static bool GetButtonDown(Button key) { return Menu; }
}
public class IngameMenu
{
    public static IngameMenu main;
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public bool selected;
    public int Opens;
    public void Open() { selected = gameObject.activeInHierarchy = true; Opens++; UWE.FreezeTime.Begin("IngameMenu"); }
    public void Close() { selected = gameObject.activeInHierarchy = false; UWE.FreezeTime.End("IngameMenu"); }
    public void QuitGame(bool desktop) { }
    public void QuitGameAsync(bool desktop) { }
}
public static class SceneCleaner { public static void Open() { } }
public static class FreecamSessionChecks
{
    private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); }
    public static void Run()
    {
        var camera = new FreecamController();
        UnityEngine.Object.Found = camera;
        Player.main = new Player();
        var menu = IngameMenu.main = new IngameMenu();
        UnityEngine.Time.realtimeSinceStartup = 100;
        // Opponent forfeits while winner already has pause open.
        menu.Open();
        Check(ItsFrosti.SpeedrunningMod.Ui.FreecamSession.TrySpectateForfeitWin(), "spectate failed");
        Check(camera.GetActive() && camera.Speed == 64 && camera.LastUpdate == 100, "native camera settings incorrect");
        Check(menu.selected && menu.Opens == 2, "menu input not restored after camera activation");
        menu.Close();
        Check(UnityEngine.Time.timeScale == 0, "closing pause resumed spectator world");
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.TrySpectateForfeitWin();
        Check(camera.Toggles == 1, "repeated result toggled camera off");
        // Simulate quit while pause is open, including a late native resume.
        menu.Open();
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.RestoreNormalTime();
        Check(UnityEngine.Time.timeScale == 1, "exit did not reset speed");
        menu.Close();
        Check(UnityEngine.Time.timeScale == 1, "late native resume restored frozen speed");
        UnityEngine.Time.timeScale = 0.25f;
        menu.Open();
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.RestoreNormalTime(); menu.Close();
        Check(UnityEngine.Time.timeScale == 1, "ordinary save quit retained custom speed");
        LaunchRocket.isLaunching = true;
        Check(!ItsFrosti.SpeedrunningMod.Ui.FreecamSession.TrySpectateForfeitWin(), "rocket launch was interrupted");
        LaunchRocket.isLaunching = false;
        // General freecam ESC opening, including an unfocused camera.
        float last = 0;
        var dummy = new UnityEngine.GameObject();
        dummy.activeSelf = false;
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.ObserveCamera(camera, dummy, ref last);
        UnityEngine.Input.Escape = true;
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.LateUpdate();
        Check(!menu.selected && last == 100, "unfocused camera stole input or retained stale movement time");
        dummy.activeSelf = true;
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.ObserveCamera(camera, dummy, ref last);
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.LateUpdate();
        Check(menu.selected, "ESC did not open freecam pause");
        menu.Close();
        var closeHook = typeof(ItsFrosti.SpeedrunningMod.Ui.FreecamSession).GetNestedType("MenuClosedPatch", System.Reflection.BindingFlags.NonPublic);
        closeHook.GetMethod("Prefix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.LateUpdate();
        Check(!menu.selected, "ESC immediately reopened closed pause");
        UnityEngine.Time.frameCount++;
        UnityEngine.Input.Escape = false;
        GameInput.Menu = true;
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.LateUpdate();
        Check(menu.selected, "bound menu input did not open freecam pause");
        ItsFrosti.SpeedrunningMod.Ui.FreecamSession.RestoreNormalTime(); menu.Close();
    }
}
