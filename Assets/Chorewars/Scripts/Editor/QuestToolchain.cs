using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chorewars.Build
{
    /// <summary>Which piece of the Android toolchain is being described.</summary>
    public enum ToolKind
    {
        Jdk,
        Sdk,
        Ndk,
    }

    /// <summary>Where a tool path is being used, so a wrong one can be traced to its source.</summary>
    public enum ToolSource
    {
        /// <summary>Nowhere: nothing resolved to a path.</summary>
        Missing,

        /// <summary>This editor's own Android settings — the values a build would actually use.</summary>
        EditorSettings,

        /// <summary>The per-user External Tools preferences.</summary>
        Preferences,

        /// <summary>The copy that ships inside the editor install, under PlaybackEngines.</summary>
        HubInstall,

        /// <summary>An environment variable, used only when nothing else names the tool.</summary>
        Environment,
    }

    /// <summary>One piece of the toolchain: where it is, where that came from, and whether it works.</summary>
    public sealed class ToolStatus
    {
        public ToolKind Kind;
        public string Path;
        public ToolSource Source;
        public bool PathExists;

        /// <summary>
        /// Whether the piece is actually usable, which is stricter than the folder existing: an SDK
        /// directory with no platform-tools will fail a build later, and saying so here is the whole
        /// point of asking.
        /// </summary>
        public bool Usable;

        /// <summary>Versions found, or what is missing from the folder.</summary>
        public string Detail;
    }

    /// <summary>
    /// Finds the Android JDK, SDK and NDK, and says which is missing.
    /// </summary>
    /// <remarks>
    /// Two different things can be wrong, and they look identical from a failed build:
    ///
    /// 1. The files are not on the machine. Install them.
    /// 2. The files are on the machine but this editor session has not noticed. Unity reads the
    ///    PlaybackEngines tree when it starts, so an editor that is already running when Android Build
    ///    Support is installed keeps reporting that it is unsupported until it is restarted. Nothing
    ///    is wrong and a reinstall will not help.
    ///
    /// This reports both, separately, because "the module is not installed" and "the editor has not
    /// been restarted" want opposite responses.
    ///
    /// The paths are resolved in the order a build resolves them: this editor's Android settings
    /// first, then the per-user External Tools preferences, then the copy under
    /// <c>PlaybackEngines/AndroidPlayer</c> that ships with the module. Environment variables are a
    /// last resort and are labelled as such — Unity does not use them, so a path that only exists
    /// there is worth seeing rather than trusting.
    /// </remarks>
    public static class QuestToolchain
    {
        const string EditorSettingsType =
            "UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions";

        const string JdkPreference = "JdkPath";
        const string SdkPreference = "AndroidSdkRoot";
        const string NdkPreference = "AndroidNdkRoot";

        /// <summary>This editor's data folder, which is where PlaybackEngines lives.</summary>
        public static string EditorDataPath => EditorApplication.applicationContentsPath;

        /// <summary>The Android module's own folder inside the editor install.</summary>
        public static string HubAndroidPlayerPath
            => Path.Combine(EditorDataPath, "PlaybackEngines", "AndroidPlayer");

        /// <summary>Whether the module's files are on disk, whether or not this session can use them.</summary>
        public static bool ModuleFilesPresent => Directory.Exists(HubAndroidPlayerPath);

        public static ToolStatus Jdk { get; private set; }
        public static ToolStatus Sdk { get; private set; }
        public static ToolStatus Ndk { get; private set; }

        /// <summary>Resolves all three, then reports.</summary>
        public static void Refresh()
        {
            Jdk = Describe(ToolKind.Jdk, ExternalToolsPath("jdkRootPath"), EditorPrefs.GetString(JdkPreference, null),
                Path.Combine(HubAndroidPlayerPath, "OpenJDK"),
                new[] { "JAVA_HOME", "JDK_HOME" }, "bin", "java");

            Sdk = Describe(ToolKind.Sdk, ExternalToolsPath("sdkRootPath"), EditorPrefs.GetString(SdkPreference, null),
                Path.Combine(HubAndroidPlayerPath, "SDK"),
                new[] { "ANDROID_SDK_ROOT", "ANDROID_HOME" }, "platform-tools", "adb");

            // ndk-build lives at the NDK root and is a shell script, not a directory: it is the
            // required *file* with no required directory. Passing it as a directory reports every
            // valid NDK as incomplete, which is a whole afternoon of confusion.
            Ndk = Describe(ToolKind.Ndk, ExternalToolsPath("ndkRootPath"), EditorPrefs.GetString(NdkPreference, null),
                Path.Combine(HubAndroidPlayerPath, "NDK"),
                new[] { "ANDROID_NDK_ROOT", "ANDROID_NDK_HOME" }, null, "ndk-build");
        }

        public static bool AllUsable
        {
            get
            {
                Refresh();
                return Jdk.Usable && Sdk.Usable && Ndk.Usable;
            }
        }

        public static string Summary()
        {
            Refresh();

            var sb = new StringBuilder();
            sb.Append(Abbreviate(Jdk)).Append(' ');
            sb.Append(Abbreviate(Sdk)).Append(' ');
            sb.Append(Abbreviate(Ndk)).Append(' ');
            sb.Append(SessionNote());

            return sb.ToString();
        }

        public static string Detailed()
        {
            Refresh();

            var sb = new StringBuilder();
            sb.AppendLine("JDK: " + Long(Jdk));
            sb.AppendLine("SDK: " + Long(Sdk));
            sb.AppendLine("NDK: " + Long(Ndk));
            sb.Append("module files on disk: ").Append(ModuleFilesPresent)
                .Append(" at ").Append(HubAndroidPlayerPath).AppendLine();
            sb.Append("this editor session can build for Android: ")
                .Append(QuestBuild.IsAndroidBuildSupported).AppendLine();
            sb.Append("verdict: ").Append(AdviceText());

            return sb.ToString();
        }

        public static string Advice()
        {
            Refresh();
            return AdviceText();
        }

        static string AdviceText()
        {
            if (!ModuleFilesPresent)
                return "the Android Build Support module is not installed. Add it from Unity Hub " +
                       "(Editor install > Add modules > Android Build Support, with SDK and NDK).";

            if (!AllUsable)
            {
                var missing = new List<string>();

                if (!Jdk.Usable) missing.Add("JDK");
                if (!Sdk.Usable) missing.Add("SDK");
                if (!Ndk.Usable) missing.Add("NDK");

                return "the Android module is present but its " + string.Join(", ", missing) +
                       " is missing or incomplete; the detail above says which part.";
            }

            if (!QuestBuild.IsAndroidBuildSupported)
                return "everything is on disk, but this editor session started before the module was " +
                       "installed. Restart the editor and it will be able to build.";

            return "ready to build.";
        }

        [MenuItem("BoreDOOM/Quest/Report Android toolchain")]
        static void ReportFromMenu()
        {
            Debug.Log("[BoreDOOM] Android toolchain —\n" + Detailed());
        }
        /// <summary>
        /// Checks the resolver can recognise a real NDK, by building one. The trap this guards is
        /// specific: ndk-build is a file at the NDK root, not a directory, so it must be passed as
        /// the required *file*. Getting that wrong makes every valid NDK read as incomplete, and it
        /// fails quietly -- the resolver still returns a plausible-looking "missing ndk-build".
        /// </summary>
        [MenuItem("BoreDOOM/Quest/Run toolchain self-test")]
        public static void SelfTestFromMenu()
        {
            string report;
            var passed = SelfTest(out report);

            if (passed) Debug.Log("[BoreDOOM] QuestToolchain self-test PASSED" + Environment.NewLine + report);
            else Debug.LogError("[BoreDOOM] QuestToolchain self-test FAILED" + Environment.NewLine + report);
        }

        /// <summary>Points the resolver at a synthetic NDK root and checks what it makes of it.</summary>
        public static bool SelfTest(out string report)
        {
            var sb = new StringBuilder();
            var passed = true;

            var root = Path.Combine(Path.GetTempPath(), "boredoom-questtoolchain-selftest");
            var nothing = Path.Combine(root, "not-a-real-ndk");

            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "ndk-build"), "#!/bin/sh");
                File.WriteAllText(Path.Combine(root, "source.properties"), "Pkg.Revision = 99.9.99999");

                var present = Describe(ToolKind.Ndk, root, null, nothing, new string[0], null, "ndk-build");

                passed &= Expect(sb, "a root with an ndk-build file is usable", present.Usable, present.Detail);
                passed &= Expect(sb, "and its revision is read", Contains(present.Detail, "99.9.99999"), present.Detail);
                passed &= Expect(sb, "and it is credited to the editor's settings",
                    present.Source == ToolSource.EditorSettings, present.Source.ToString());

                File.Delete(Path.Combine(root, "ndk-build"));

                var without = Describe(ToolKind.Ndk, root, null, nothing, new string[0], null, "ndk-build");

                passed &= Expect(sb, "a root without ndk-build is not usable", !without.Usable, without.Detail);
                passed &= Expect(sb, "and says ndk-build is what is missing",
                    Contains(without.Detail, "missing ndk-build"), without.Detail);
            }
            catch (Exception exception)
            {
                passed = false;
                sb.Append("  FAULT  ").Append(exception.GetType().Name).Append(": ").AppendLine(exception.Message);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception) { }
            }

            report = sb.ToString();
            return passed;
        }

        static bool Expect(StringBuilder sb, string what, bool passed, string detail)
        {
            sb.Append(passed ? "  PASS  " : "  FAIL  ").Append(what);

            if (!passed && !string.IsNullOrEmpty(detail)) sb.Append("  (").Append(detail).Append(')');

            return passed;
        }

        static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack) && haystack.Contains(needle);

        static string SessionNote()
        {
            var sb = new StringBuilder();

            if (!ModuleFilesPresent) return "moduleFiles=missing";

            sb.Append("moduleFiles=present sessionSeesModule=").Append(QuestBuild.IsAndroidBuildSupported);

            if (AllUsable && !QuestBuild.IsAndroidBuildSupported) sb.Append(" (restart the editor)");

            return sb.ToString();
        }

        static string Abbreviate(ToolStatus tool)
        {
            var sb = new StringBuilder();
            sb.Append(tool.Kind.ToString().ToLowerInvariant()).Append('=');
            if (string.IsNullOrEmpty(tool.Path))
            {
                sb.Append("(not found)");
                return sb.ToString();
            }
            sb.Append(tool.Usable ? "ok" : "MISSING");
            if (!string.IsNullOrEmpty(tool.Detail)) sb.Append('/').Append(tool.Detail);
            return sb.ToString();
        }

        static string Long(ToolStatus tool)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(tool.Path) ? "(not found)" : tool.Path);
            sb.Append(" [").Append(SourceName(tool.Source)).Append(']');
            sb.Append(tool.Usable ? " — usable" : " — NOT USABLE");
            if (!string.IsNullOrEmpty(tool.Detail)) sb.Append(" (").Append(tool.Detail).Append(')');
            return sb.ToString();
        }

        static string SourceName(ToolSource source)
        {
            switch (source)
            {
                case ToolSource.EditorSettings: return "this editor's Android settings";
                case ToolSource.Preferences: return "External Tools preferences";
                case ToolSource.HubInstall: return "editor install";
                case ToolSource.Environment: return "environment variable";
                default: return "not found";
            }
        }

        static ToolStatus Describe(ToolKind kind, string fromEditorSettings, string fromPreferences,
            string fromHub, string[] environmentNames, string requiredDir, string requiredFile)
        {
            var tool = new ToolStatus { Kind = kind };
            var candidates = new[]
            {
                new KeyValuePair<string, ToolSource>(fromEditorSettings, ToolSource.EditorSettings),
                new KeyValuePair<string, ToolSource>(fromPreferences, ToolSource.Preferences),
                new KeyValuePair<string, ToolSource>(fromHub, ToolSource.HubInstall),
            };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.Key) || !Directory.Exists(candidate.Key)) continue;
                tool.Path = candidate.Key; tool.Source = candidate.Value; tool.PathExists = true; break;
            }
            if (!tool.PathExists)
            {
                foreach (var name in environmentNames)
                {
                    var value = Environment.GetEnvironmentVariable(name);
                    if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) continue;
                    tool.Path = value; tool.Source = ToolSource.Environment; tool.PathExists = true; break;
                }
            }
            if (!tool.PathExists)
            {
                tool.Detail = "expected at " + fromHub; return tool;
            }
            tool.Usable = Inspect(tool.Path, kind, requiredDir, requiredFile, out var detail);
            tool.Detail = detail; return tool;
        }

        static bool Inspect(string root, ToolKind kind, string requiredDir, string requiredFile, out string detail)
        {
            var missing = new List<string>(); var found = new List<string>();
            if (!string.IsNullOrEmpty(requiredDir))
            {
                var directory = Path.Combine(root, requiredDir);
                if (Directory.Exists(directory)) found.Add(requiredDir);
                else missing.Add(requiredDir);
            }
            if (!string.IsNullOrEmpty(requiredFile))
            {
                if (File.Exists(Path.Combine(root, requiredDir ?? string.Empty, requiredFile))) found.Add(requiredFile);
                else missing.Add(requiredFile);
            }
            switch (kind)
            {
                case ToolKind.Jdk:
                    found.AddRange(VersionsFromReleaseFile(Path.Combine(root, "release"), "JAVA_VERSION"));
                    break;
                case ToolKind.Sdk:
                    var buildTools = ChildNames(Path.Combine(root, "build-tools"));
                    if (buildTools.Count > 0) found.Add("build-tools " + string.Join(",", buildTools));
                    else missing.Add("build-tools");
                    var platforms = ChildNames(Path.Combine(root, "platforms"));
                    if (platforms.Count > 0) found.Add("platforms " + string.Join(",", platforms));
                    else missing.Add("platforms");
                    break;
                case ToolKind.Ndk:
                    found.AddRange(VersionsFromReleaseFile(Path.Combine(root, "source.properties"), "Pkg.Revision"));
                    break;
            }
            var sbd = new StringBuilder();
            if (found.Count > 0) sbd.Append(string.Join(", ", found));
            if (missing.Count > 0) { if (sbd.Length > 0) sbd.Append("; "); sbd.Append("missing ").Append(string.Join(", ", missing)); }
            detail = sbd.ToString(); return missing.Count == 0;
        }

        static List<string> VersionsFromReleaseFile(string path, string key)
        {
            var values = new List<string>();
            try
            {
                if (!File.Exists(path)) return values;
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                    var separator = trimmed.IndexOf('='); if (separator < 0) continue;
                    values.Add(trimmed.Substring(separator + 1).Trim().Trim('"')); break;
                }
            }
            catch (Exception) { }
            return values;
        }

        static List<string> ChildNames(string directory)
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(directory)) return names;
                foreach (var child in Directory.GetDirectories(directory)) names.Add(Path.GetFileName(child));
                names.Sort(StringComparer.Ordinal);
            }
            catch (Exception) { }
            return names;
        }

        static string ExternalToolsPath(string propertyName)
        {
            try
            {
                var type = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
                if (type == null) return null;
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return property?.GetValue(null) as string;
            }
            catch (Exception) { return null; }
        }
    }
}
