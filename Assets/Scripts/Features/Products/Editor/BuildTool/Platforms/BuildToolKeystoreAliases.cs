using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;

namespace Products.Editor
{
    internal static class BuildToolKeystoreAliases
    {
        internal static async Task<string[]> Read(string store, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Keystore 비밀번호를 입력하세요.");
            string module = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll");
            var settings = Assembly.LoadFrom(module).GetType("UnityEditor.Android.AndroidExternalToolsSettings", true);
            string jdk = (string)settings.GetProperty("jdkRootPath").GetValue(null);
            const string projectPrefix = "{inproject}:";
            const string dedicatedPrefix = "{dedicated}:";
            if (store.StartsWith(projectPrefix, StringComparison.Ordinal)) store = store.Substring(projectPrefix.Length).TrimStart();
            else if (store.StartsWith(dedicatedPrefix, StringComparison.Ordinal))
                store = Path.Combine((string)settings.GetProperty("keystoresDedicatedLocation").GetValue(null), store.Substring(dedicatedPrefix.Length).TrimStart());
            string path = Path.GetFullPath(store);
            if (!File.Exists(path)) throw new FileNotFoundException("Keystore 파일을 확인하세요.");
            string executable = Path.Combine(jdk, "bin", Environment.OSVersion.Platform == PlatformID.Win32NT ? "keytool.exe" : "keytool");
            if (!File.Exists(executable)) throw new FileNotFoundException("Unity JDK 경로를 확인하세요.");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                Arguments = "-J-Duser.language=en -J-Duser.country=US -J-Dfile.encoding=UTF-8 -list -v -keystore \"" + path + "\" -storepass:env MATCH_BUILD_KEYSTORE_PASSWORD"
            };
            // 명령줄 인수·파일 대신 자식 프로세스에만 전달한다. 실패 출력도 로그에 기록하지 않는다.
            start.EnvironmentVariables["MATCH_BUILD_KEYSTORE_PASSWORD"] = password;
            using (var process = new Process { StartInfo = start })
            {
                process.Start(); start.EnvironmentVariables.Remove("MATCH_BUILD_KEYSTORE_PASSWORD");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var completion = Task.Run(() => process.WaitForExit());
                if (await Task.WhenAny(completion, Task.Delay(15000)) != completion)
                {
                    process.Kill(); await completion;
                    throw new TimeoutException("Alias 조회 시간이 초과됐습니다.");
                }
                string text = await output; await error;
                if (process.ExitCode != 0) throw new InvalidOperationException("Keystore를 열지 못했습니다.");
                var entries = Regex.Matches(text, @"^Alias name: ([^\r\n]+)\r?\n(?:(?!^Alias name:).)*?^Entry type: PrivateKeyEntry\s*$", RegexOptions.Multiline | RegexOptions.Singleline);
                string[] aliases = entries.Cast<Match>().Select(match => match.Groups[1].Value.TrimEnd('\r')).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (aliases.Length == 0) throw new InvalidOperationException("서명용 키가 없습니다.");
                return aliases;
            }
        }
    }
}
