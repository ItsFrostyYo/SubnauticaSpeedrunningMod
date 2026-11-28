public static class AccountProtectionTest
{
    public static void Run()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("test-session-no-real-credentials");
        byte[] entropy = System.Text.Encoding.UTF8.GetBytes("ItsFrosti.SpeedrunningMod");
        byte[] encrypted = ItsFrosti.SpeedrunningMod.SessionProtection.Protect(data, entropy);
        byte[] roundTrip = ItsFrosti.SpeedrunningMod.SessionProtection.Unprotect(encrypted, entropy);
        if (!Equal(data, roundTrip)) throw new System.Exception("DPAPI round-trip mismatch.");
        byte[] legacy = System.Security.Cryptography.ProtectedData.Protect(data, entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        if (!Equal(data, ItsFrosti.SpeedrunningMod.SessionProtection.Unprotect(legacy, entropy))) throw new System.Exception("Legacy session cannot be read.");
        if (!Equal(data, System.Security.Cryptography.ProtectedData.Unprotect(encrypted, entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser))) throw new System.Exception("Native session format differs from legacy DPAPI.");
        bool rejected = false;
        try { ItsFrosti.SpeedrunningMod.SessionProtection.Unprotect(encrypted, new byte[] { 1, 2, 3 }); }
        catch (System.ComponentModel.Win32Exception) { rejected = true; }
        if (!rejected) throw new System.Exception("Wrong entropy was accepted.");
        encrypted[encrypted.Length / 2] ^= 1;
        rejected = false;
        try { ItsFrosti.SpeedrunningMod.SessionProtection.Unprotect(encrypted, entropy); }
        catch (System.ComponentModel.Win32Exception) { rejected = true; }
        if (!rejected) throw new System.Exception("Modified ciphertext was accepted.");
    }
    private static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
