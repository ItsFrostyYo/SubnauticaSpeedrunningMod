using SNSRModLoader;

namespace ItsFrosti.SpeedrunningMod
{
    internal sealed class ModClientIdentity
    {
        internal string PlayerId { get; set; }
        internal string DisplayName { get; set; }
    }

    internal static class ModClientIdentityStore
    {
        private static ModClientIdentity identity;
        internal static void Initialize(PluginContext context)
        {
            if (identity != null) return;
            identity = new ModClientIdentity { PlayerId = PluginSettings.PlayerId, DisplayName = PluginSettings.DisplayName };
        }
        internal static ModClientIdentity GetIdentity() { return identity; }
    }
}
