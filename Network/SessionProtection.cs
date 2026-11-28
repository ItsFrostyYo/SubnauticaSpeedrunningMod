using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ItsFrosti.SpeedrunningMod
{
    // Unity 5.6's stripped Mono profile does not include System.Security.dll.
    // Use Windows DPAPI directly; flags omit LOCAL_MACHINE so protection stays
    // scoped to the current Windows user, matching ProtectedData.CurrentUser.
    internal static class SessionProtection
    {
        private const int UiForbidden = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            internal int Length;
            internal IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DataBlob input, string description,
            ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DataBlob input, out IntPtr description,
            ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        internal static byte[] Protect(byte[] value, byte[] entropy)
        {
            return Transform(value, entropy, true);
        }

        internal static byte[] Unprotect(byte[] value, byte[] entropy)
        {
            return Transform(value, entropy, false);
        }

        private static DataBlob Allocate(byte[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            var blob = new DataBlob { Length = value.Length, Data = Marshal.AllocHGlobal(Math.Max(1, value.Length)) };
            if (value.Length > 0) Marshal.Copy(value, 0, blob.Data, value.Length);
            return blob;
        }

        private static void ReleaseInput(ref DataBlob blob)
        {
            if (blob.Data == IntPtr.Zero) return;
            // Do not leave a second unmanaged copy of the plaintext session behind.
            for (int i = 0; i < blob.Length; i++) Marshal.WriteByte(blob.Data, i, 0);
            Marshal.FreeHGlobal(blob.Data);
            blob.Data = IntPtr.Zero;
        }

        private static byte[] Transform(byte[] value, byte[] entropy, bool protect)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Saved account sessions require Windows DPAPI.");
            var input = new DataBlob();
            var salt = new DataBlob();
            var output = new DataBlob();
            IntPtr description = IntPtr.Zero;
            try
            {
                input = Allocate(value);
                salt = Allocate(entropy ?? new byte[0]);
                bool succeeded = protect
                    ? CryptProtectData(ref input, null, ref salt, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref input, out description, ref salt, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
                if (!succeeded) throw new Win32Exception(Marshal.GetLastWin32Error(), "Account session encryption failed.");
                var result = new byte[output.Length];
                if (result.Length > 0) Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                ReleaseInput(ref input);
                ReleaseInput(ref salt);
                if (output.Data != IntPtr.Zero)
                {
                    for (int i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                    LocalFree(output.Data);
                }
                if (description != IntPtr.Zero) LocalFree(description);
            }
        }
    }
}
