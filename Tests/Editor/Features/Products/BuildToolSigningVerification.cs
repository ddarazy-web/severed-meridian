using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public static class BuildToolSigningVerification
    {
        public static async void Run()
        {
            string originalPasswordRoot = BuildToolSigningPasswords.Root;
            BuildToolSigningPasswords.Root = "Logs/BuildToolSigning/Secure-" + Guid.NewGuid().ToString("N");
            BuildToolSigningPasswords savedPasswords = null;
            string beforeStore = PlayerSettings.Android.keystorePass, beforeKey = PlayerSettings.Android.keyaliasPass;
            var original = new BuildToolStateSnapshot();
            string copy = "Assets/Settings/BuildProfiles/__SigningVerification-" + Guid.NewGuid().ToString("N") + ".asset";
            string preferences = "Logs/BuildToolSigning/selection-" + Guid.NewGuid().ToString("N") + ".json";
            string fixture = "Logs/BuildToolSigning/fixture-" + Guid.NewGuid().ToString("N") + ".jks";
            MatchBuildToolWindow window = null;
            Exception failure = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fixture));
                await CreateFixture(fixture);
                var aliases = await BuildToolKeystoreAliases.Read("{inproject}: " + fixture, "fixture-pass");
                BuildToolPreflightVerification.Check(aliases.SequenceEqual(new[] { "first-key", "second-key" }), "real keystore lists both signing aliases");
                bool rejected = false;
                try { await BuildToolKeystoreAliases.Read(fixture, "wrong-fixture-pass"); }
                catch (InvalidOperationException) { rejected = true; }
                BuildToolPreflightVerification.Check(rejected, "incorrect keystore password is rejected without raw command output");
                if (!AssetDatabase.CopyAsset(ProductProfiles.Definitions[0].ProfilePath, copy)) throw new Exception("fixture copy failed");
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(copy);
                BuildToolAndroidSigning.SaveAlias(profile, aliases[1]);
                var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile, false));
                BuildToolPreflightVerification.Check(settings.FindProperty("AndroidKeyaliasName").stringValue == "second-key" &&
                    File.ReadAllText(copy).Contains("second-key"), "selected alias persists in only the selected profile");
                window = ScriptableObject.CreateInstance<MatchBuildToolWindow>(); window.SelectionPath = preferences;
                window.Show(); window.CreateGUI(); window.SelectProfile(AssetDatabase.AssetPathToGUID(copy));
                var root = window.rootVisualElement;
                BuildToolPreflightVerification.Check(root.Q<ScrollView>("profile-detail").contentContainer[0].name == "platform-settings",
                    "Android signing is the first profile detail section");
                var storeField = root.Q<TextField>("android-keystore-password");
                var keyField = root.Q<TextField>("android-key-password");
                BuildToolPreflightVerification.Check(storeField.isPasswordField && keyField.isPasswordField &&
                    !root.Q<DropdownField>("android-key-alias").enabledSelf, "passwords are masked and alias selection waits for keystore lookup");
                storeField.value = "fixture-pass"; keyField.value = "fixture-key-pass";
                BuildToolPreflightVerification.Check(PlayerSettings.Android.keystorePass == "fixture-pass" && PlayerSettings.Android.keyaliasPass == "fixture-key-pass",
                    "password controls feed Unity Android build signing settings");
                BuildToolPreflightVerification.Check(!File.ReadAllText(copy).Contains("fixture-pass") && !File.ReadAllText(preferences).Contains("fixture-pass"),
                    "passwords are absent from profile and tool preference files");
                string context = Path.GetFullPath(".") + "\n" + AssetDatabase.AssetPathToGUID(copy) + "\n" + settings.FindProperty("AndroidKeystoreName").stringValue + "\nsecond-key";
                savedPasswords = new BuildToolSigningPasswords(context);
                typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(root.Q<Button>("android-save-passwords").clickable, new object[] { null });
                BuildToolPreflightVerification.Check(File.Exists(savedPasswords.FilePath) && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(savedPasswords.FilePath)).Contains("fixture-pass"),
                    "save button writes encrypted bytes without plaintext passwords");
                PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                typeof(BuildToolAndroidSigning).GetField("loadedContext", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, null);
                window.ReloadProfiles();
                BuildToolPreflightVerification.Check(root.Q<TextField>("android-keystore-password").value == "fixture-pass" &&
                    root.Q<TextField>("android-key-password").value == "fixture-key-pass", "saved passwords automatically populate signing controls");
                var runner = BuildToolController.Runner;
                var lockField = typeof(BuildToolRunner).GetField("<StoreError>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object oldError = lockField.GetValue(runner);
                try
                {
                    lockField.SetValue(runner, "fixture lock");
                    window.SelectProfile(AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions.Last().ProfilePath));
                    BuildToolPreflightVerification.Check(PlayerSettings.Android.keystorePass == "fixture-pass" && PlayerSettings.Android.keyaliasPass == "fixture-key-pass" &&
                        root.Q<TextField>("android-keystore-password") == null, "viewing another profile cannot change passwords while a build request is locked");
                }
                finally { lockField.SetValue(runner, oldError); }
                window.SelectProfile(AssetDatabase.AssetPathToGUID(copy));
                typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(root.Q<Button>("android-delete-passwords").clickable, new object[] { null });
                BuildToolPreflightVerification.Check(!File.Exists(savedPasswords.FilePath) && PlayerSettings.Android.keystorePass == "" && PlayerSettings.Android.keyaliasPass == "",
                    "delete button removes saved ciphertext and current passwords");
                window.SelectProfile(AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions[2].ProfilePath));
                BuildToolPreflightVerification.Check(root.Q("platform-settings") == null, "Android signing controls are absent for Windows");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                PlayerSettings.Android.keystorePass = beforeStore; PlayerSettings.Android.keyaliasPass = beforeKey;
                if (window != null) window.Close();
                AssetDatabase.DeleteAsset(copy);
                if (File.Exists(preferences)) File.Delete(preferences);
                if (File.Exists(fixture)) File.Delete(fixture);
                savedPasswords?.Delete();
                if (Directory.Exists(BuildToolSigningPasswords.Root) && !Directory.EnumerateFileSystemEntries(BuildToolSigningPasswords.Root).Any()) Directory.Delete(BuildToolSigningPasswords.Root);
                BuildToolSigningPasswords.Root = originalPasswordRoot;
                foreach (string dimension in new[] { "Width", "Height", "X", "Y" })
                    EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + "." + dimension);
            }
            try { original.AssertUnchanged(); } catch (Exception error) { failure = failure ?? error; }
            if (failure == null) Debug.Log("PASS BuildToolSigningVerification"); else Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        public static void SecureWrite()
        {
            try
            {
                BuildToolSigningPasswords.Root = "Logs/BuildToolSigning/RestartFixture";
                new BuildToolSigningPasswords("restart-fixture-context").Save("restart-store-fixture", "restart-key-fixture");
                Debug.Log("PASS encrypted signing fixture saved for a new Unity process"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void SecureRead()
        {
            BuildToolSigningPasswords.Root = "Logs/BuildToolSigning/RestartFixture";
            var saved = new BuildToolSigningPasswords("restart-fixture-context");
            var other = new BuildToolSigningPasswords("different-profile-or-alias");
            Exception failure = null;
            try
            {
                BuildToolPreflightVerification.Check(saved.TryRead(out string store, out string key) && store == "restart-store-fixture" && key == "restart-key-fixture",
                    "encrypted passwords restore after actual Unity process restart");
                File.Copy(saved.FilePath, other.FilePath, true);
                bool rejected = false;
                try { other.TryRead(out _, out _); } catch (System.Security.Cryptography.CryptographicException) { rejected = true; }
                BuildToolPreflightVerification.Check(rejected, "copied ciphertext cannot restore under another profile or alias context");
                File.WriteAllBytes(saved.FilePath, new byte[] { 1, 2, 3 });
                rejected = false;
                try { saved.TryRead(out _, out _); } catch (System.Security.Cryptography.CryptographicException) { rejected = true; }
                BuildToolPreflightVerification.Check(rejected, "damaged ciphertext is rejected instead of falling back to plaintext");
            }
            catch (Exception error) { failure = error; }
            finally { saved.Delete(); other.Delete(); }
            if (failure == null) Debug.Log("PASS BuildToolSigningSecureRestartVerification"); else Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static async System.Threading.Tasks.Task CreateFixture(string path)
        {
            // 실제 프로젝트 키 대신 매번 별도의 검사 전용 키를 생성하고 마지막에 제거한다.
            string executable = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK/bin",
                Environment.OSVersion.Platform == PlatformID.Win32NT ? "keytool.exe" : "keytool");
            foreach (string alias in new[] { "first-key", "second-key" })
            {
                var start = new System.Diagnostics.ProcessStartInfo(executable)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    Arguments = "-genkeypair -alias " + alias + " -keyalg RSA -keysize 2048 -validity 1 -dname CN=BuildToolFixture -storetype JKS -keystore \"" +
                        Path.GetFullPath(path) + "\" -storepass:env MATCH_SIGNING_FIXTURE_PASS -keypass:env MATCH_SIGNING_FIXTURE_PASS -noprompt"
                };
                start.EnvironmentVariables["MATCH_SIGNING_FIXTURE_PASS"] = "fixture-pass";
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                    var completion = System.Threading.Tasks.Task.Run(() => process.WaitForExit());
                    if (await System.Threading.Tasks.Task.WhenAny(completion, System.Threading.Tasks.Task.Delay(15000)) != completion)
                    { process.Kill(); await completion; throw new TimeoutException("fixture generation timeout"); }
                    await output; await error;
                    if (process.ExitCode != 0) throw new InvalidOperationException("fixture generation failed");
                }
            }
        }
    }
}
