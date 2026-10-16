using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Products.Editor
{
    internal sealed class BuildToolSigningPasswords
    {
        internal static string Root = "UserSettings/MatchBuildSigning";
        internal static bool Supported => Environment.OSVersion.Platform == PlatformID.Win32NT;
        private readonly byte[] entropy;
        internal readonly string FilePath;

        internal BuildToolSigningPasswords(string context)
        {
            using (var hash = SHA256.Create()) entropy = hash.ComputeHash(Encoding.UTF8.GetBytes(context));
            FilePath = Path.Combine(Root, BitConverter.ToString(entropy).Replace("-", "") + ".bin");
        }

        internal void Save(string storePassword, string keyPassword)
        {
            byte[] plain;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                { writer.Write(storePassword); writer.Write(keyPassword); }
                plain = stream.ToArray();
                Array.Clear(stream.GetBuffer(), 0, (int)stream.Length);
            }
            byte[] encrypted;
            try { encrypted = Transform(plain, true); }
            finally { Array.Clear(plain, 0, plain.Length); }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, encrypted);
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal bool TryRead(out string storePassword, out string keyPassword)
        {
            storePassword = keyPassword = "";
            if (!File.Exists(FilePath)) return false;
            byte[] plain = Transform(File.ReadAllBytes(FilePath), false);
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(plain), Encoding.UTF8))
                { storePassword = reader.ReadString(); keyPassword = reader.ReadString(); }
                return true;
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        internal void Delete() { if (File.Exists(FilePath)) File.Delete(FilePath); }

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob { public int Size; public IntPtr Data; }
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref Blob input, string description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private byte[] Transform(byte[] bytes, bool protect)
        {
            if (!Supported) throw new PlatformNotSupportedException("암호화 저장은 Windows에서 지원합니다.");
            var input = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            var salt = new Blob { Size = entropy.Length, Data = Marshal.AllocHGlobal(entropy.Length) };
            var output = new Blob();
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length); Marshal.Copy(entropy, 0, salt.Data, entropy.Length);
                // LOCAL_MACHINE을 사용하지 않아 현재 Windows 사용자만 복호화할 수 있다. UI 프롬프트는 금지한다.
                bool success = protect ? CryptProtectData(ref input, null, ref salt, IntPtr.Zero, IntPtr.Zero, 1, out output) :
                    CryptUnprotectData(ref input, IntPtr.Zero, ref salt, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!success) throw new CryptographicException("서명 비밀번호 암호화 또는 복호화에 실패했습니다.");
                var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
            }
            finally
            {
                for (int i = 0; i < input.Size; i++) Marshal.WriteByte(input.Data, i, 0);
                Marshal.FreeHGlobal(input.Data); Marshal.FreeHGlobal(salt.Data);
                if (output.Data != IntPtr.Zero)
                {
                    for (int i = 0; i < output.Size; i++) Marshal.WriteByte(output.Data, i, 0);
                    LocalFree(output.Data);
                }
            }
        }
    }
}
