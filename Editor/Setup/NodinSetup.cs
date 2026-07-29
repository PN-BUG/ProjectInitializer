#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer.Setup
{
    /// <summary>
    /// The single authority for keeping the project's Nodin package reference valid.
    /// </summary>
    [InitializeOnLoad]
    public static class NodinSetup
    {
        private const string PackageName = "com.zko.nodin";
        private const string EmbeddedPackageReference = "file:nodin";
        private const string GitPackageReference = "https://github.com/PN-BUG/Nodin.git";
        private const string SessionStateKey = "ProjectInitializer.NodinSetup.ManifestStamp";

        static NodinSetup()
        {
            EnsureNodinDependency();
        }

        /// <summary>
        /// Keeps manifest.json aligned with the package source that is actually present.
        /// Embedded Packages/nodin takes precedence over the remote Git source.
        /// </summary>
        public static bool EnsureNodinDependency()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Nodin] Packages/manifest.json was not found.");
                return false;
            }

            string packageDirectory = Path.Combine(Path.GetDirectoryName(manifestPath), "nodin");
            string expectedReference = Directory.Exists(packageDirectory) ? EmbeddedPackageReference : GitPackageReference;
            string currentStamp = GetManifestStamp(manifestPath, packageDirectory, expectedReference);
            if (SessionState.GetString(SessionStateKey, string.Empty) == currentStamp)
                return true;

            try
            {
                string manifest = File.ReadAllText(manifestPath);
                if (!TrySetDependency(manifest, PackageName, expectedReference, out string updatedManifest, out bool changed))
                {
                    Debug.LogWarning("[Nodin] Could not find a valid dependencies object in Packages/manifest.json.");
                    return false;
                }

                if (changed)
                {
                    File.WriteAllText(manifestPath, updatedManifest);
                    Debug.Log($"[Nodin] Package source set to {expectedReference}.");
                    currentStamp = GetManifestStamp(manifestPath, packageDirectory, expectedReference);
                }

                SessionState.SetString(SessionStateKey, currentStamp);
                return true;
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[Nodin] Failed to update manifest.json: {exception.Message}");
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning($"[Nodin] Failed to update manifest.json: {exception.Message}");
                return false;
            }
        }

        private static string GetManifestStamp(string manifestPath, string packageDirectory, string expectedReference)
        {
            long manifestTicks = File.GetLastWriteTimeUtc(manifestPath).Ticks;
            long packageTicks = Directory.Exists(packageDirectory) ? Directory.GetLastWriteTimeUtc(packageDirectory).Ticks : 0L;
            return $"{manifestTicks}:{packageTicks}:{expectedReference}";
        }

        // Small JSON-object editor: it understands quoted strings and nested object/array depth,
        // so formatting and unrelated manifest content are preserved without regex mutation.
        private static bool TrySetDependency(string json, string name, string value, out string result, out bool changed)
        {
            result = json;
            changed = false;
            if (!TryFindTopLevelObject(json, "dependencies", out int objectStart, out int objectEnd))
                return false;

            if (TryFindObjectStringProperty(json, objectStart, objectEnd, name, out int valueStart, out int valueEnd))
            {
                string currentValue = json.Substring(valueStart + 1, valueEnd - valueStart - 1);
                if (currentValue == value)
                    return true;

                result = json.Substring(0, valueStart + 1) + value + json.Substring(valueEnd);
                changed = true;
                return true;
            }

            int firstProperty = FindFirstObjectProperty(json, objectStart, objectEnd);
            string newline = json.Contains("\r\n") ? "\r\n" : "\n";
            string indent = GetIndentation(json, firstProperty >= 0 ? firstProperty : objectEnd);
            string entry = $"{indent}\"{name}\": \"{value}\"";
            string insertion;
            if (firstProperty >= 0)
            {
                insertion = entry + "," + newline;
            }
            else
            {
                string objectIndent = GetIndentation(json, objectStart);
                insertion = newline + objectIndent + "  \"" + name + "\": \"" + value + "\"" + newline + objectIndent;
            }
            int insertionIndex = firstProperty >= 0 ? firstProperty : objectEnd;
            result = json.Insert(insertionIndex, insertion);
            changed = true;
            return true;
        }

        private static bool TryFindTopLevelObject(string json, string propertyName, out int objectStart, out int objectEnd)
        {
            objectStart = objectEnd = -1;
            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != '{') return false;

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') return false;
                if (!TryReadString(json, ref index, out string name)) return false;
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index++] != ':') return false;
                SkipWhitespace(json, ref index);
                if (name == propertyName && index < json.Length && json[index] == '{')
                {
                    objectStart = index;
                    return TryFindMatchingBracket(json, index, '{', '}', out objectEnd);
                }

                if (!SkipValue(json, ref index)) return false;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            return false;
        }

        private static bool TryFindObjectStringProperty(string json, int objectStart, int objectEnd, string propertyName, out int valueStart, out int valueEnd)
        {
            valueStart = valueEnd = -1;
            int index = objectStart + 1;
            while (index < objectEnd)
            {
                SkipWhitespace(json, ref index);
                if (!TryReadString(json, ref index, out string name)) return false;
                SkipWhitespace(json, ref index);
                if (index >= objectEnd || json[index++] != ':') return false;
                SkipWhitespace(json, ref index);
                if (name == propertyName && index < objectEnd && json[index] == '"')
                {
                    valueStart = index;
                    int valueIndex = index;
                    if (!TryReadString(json, ref valueIndex, out _)) return false;
                    valueEnd = valueIndex - 1;
                    return true;
                }

                if (!SkipValue(json, ref index)) return false;
                SkipWhitespace(json, ref index);
                if (index < objectEnd && json[index] == ',') index++;
            }
            return false;
        }

        private static int FindFirstObjectProperty(string json, int objectStart, int objectEnd)
        {
            int index = objectStart + 1;
            SkipWhitespace(json, ref index);
            return index < objectEnd && json[index] == '"' ? index : -1;
        }

        private static string GetIndentation(string json, int index)
        {
            int lineStart = json.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            int cursor = lineStart;
            while (cursor < json.Length && (json[cursor] == ' ' || json[cursor] == '\t')) cursor++;
            return json.Substring(lineStart, cursor - lineStart);
        }

        private static bool SkipValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return false;
            if (json[index] == '"') return TryReadString(json, ref index, out _);
            if (json[index] == '{')
            {
                if (!TryFindMatchingBracket(json, index, '{', '}', out int objectEnd)) return false;
                index = objectEnd + 1;
                return true;
            }
            if (json[index] == '[')
            {
                if (!TryFindMatchingBracket(json, index, '[', ']', out int arrayEnd)) return false;
                index = arrayEnd + 1;
                return true;
            }
            while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']') index++;
            return true;
        }

        private static bool TryFindMatchingBracket(string json, int start, char open, char close, out int end)
        {
            end = -1;
            int depth = 0;
            for (int index = start; index < json.Length; index++)
            {
                if (json[index] == '"')
                {
                    index++;
                    while (index < json.Length)
                    {
                        if (json[index] == '\\') { index += 2; continue; }
                        if (json[index++] == '"') break;
                    }
                    continue;
                }
                if (json[index] == open) depth++;
                else if (json[index] == close && --depth == 0) { end = index; return true; }
            }
            return false;
        }

        private static bool TryReadString(string json, ref int index, out string value)
        {
            value = null;
            if (index >= json.Length || json[index++] != '"') return false;
            int start = index;
            while (index < json.Length)
            {
                if (json[index] == '\\') { index += 2; continue; }
                if (json[index] == '"') { value = json.Substring(start, index - start); index++; return true; }
                index++;
            }
            return false;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }
    }
}
#endif
