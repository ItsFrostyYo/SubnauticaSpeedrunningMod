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
        internal static ModClientIdentity GetIdentity()
        {
            return new ModClientIdentity { PlayerId = RaceAccount.PlayerId, DisplayName = RaceAccount.DisplayName };
        }
    }
}
