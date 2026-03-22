using System.Globalization;
using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.Services;

internal sealed class DesktopSnapshotReader
{
    public string ResolveSavedVariablesPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return FindDefaultSavedVariablesPath();
    }

    public string FindDefaultSavedVariablesPath()
    {
        // Allow override for diagnostics and custom installs.
        var overridePath = Environment.GetEnvironmentVariable("CCM_SAVEDVARIABLES_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var possibleRoots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var keys = new[]
                {
                    @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft",
                    @"SOFTWARE\Blizzard Entertainment\World of Warcraft"
                };

                foreach (var keyName in keys)
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName);
                    if (key?.GetValue("InstallPath") is string installPath && !string.IsNullOrWhiteSpace(installPath))
                    {
                        var normalizedPath = installPath.TrimEnd('\\', '/');
                        var parent = Directory.GetParent(normalizedPath)?.FullName;
                        
                        // The registry usually points to a specific flavor e.g. C:\Program Files (x86)\World of Warcraft\_retail_\
                        possibleRoots.Add(Path.Combine(normalizedPath, "WTF", "Account"));

                        // Also try looking at the parent directory
                        if (!string.IsNullOrWhiteSpace(parent))
                        {
                            possibleRoots.Add(Path.Combine(parent, "_retail_", "WTF", "Account"));
                            possibleRoots.Add(Path.Combine(parent, "_classic_", "WTF", "Account"));
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry access errors
            }
        }

        // Add fallback hardcoded paths
        possibleRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft", "_retail_", "WTF", "Account"));
        possibleRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "World of Warcraft", "_retail_", "WTF", "Account"));
        possibleRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft", "_classic_", "WTF", "Account"));
        possibleRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "World of Warcraft", "_classic_", "WTF", "Account"));

        foreach (var root in possibleRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var accountDirectory in Directory.GetDirectories(root))
            {
                var candidate = Path.Combine(accountDirectory, "SavedVariables", "CCM.lua");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    public bool TryReadSnapshot(string? filePath, out DesktopSnapshot snapshot)
    {
        snapshot = DesktopSnapshot.Empty;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        var content = File.ReadAllText(filePath);
        var marker = "desktopSnapshot = {";
        var markerIndex = content.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            var snapshotParser = new LuaTableParser(content, markerIndex + "desktopSnapshot = ".Length);
            if (snapshotParser.TryParseValue(out var snapshotValue) && snapshotValue is LuaTable snapshotTable)
            {
                snapshot = MapSnapshot(snapshotTable, filePath);
                return true;
            }
        }

        var dbMarker = "CCM_DB = {";
        var dbMarkerIndex = content.IndexOf(dbMarker, StringComparison.Ordinal);
        if (dbMarkerIndex < 0)
        {
        return false;
    }

        var dbParser = new LuaTableParser(content, dbMarkerIndex + "CCM_DB = ".Length);
        if (!dbParser.TryParseValue(out var dbValue) || dbValue is not LuaTable dbTable)
        {
            return false;
        }

        // Current format: desktopSnapshot nested inside CCM_DB as ["desktopSnapshot"] = { ... }
        var nestedSnapshot = dbTable.GetTable("desktopSnapshot");
        if (nestedSnapshot != null)
        {
            snapshot = MapSnapshot(nestedSnapshot, filePath);
            return true;
        }

        // Older legacy format: cooldowns and characters stored directly in CCM_DB
        snapshot = MapLegacySnapshot(dbTable, filePath, File.GetLastWriteTimeUtc(filePath));
        return true;
    }

    private static DesktopSnapshot MapSnapshot(LuaTable table, string filePath)
    {
        var snapshot = new DesktopSnapshot
        {
            SourceFile = filePath,
            SchemaVersion = table.GetInt("schemaVersion"),
            GeneratedAt = table.GetLong("generatedAt"),
        };

        var characterTable = table.GetTable("characters");
        if (characterTable != null)
        {
            foreach (var entry in characterTable.ArrayValues.OfType<LuaTable>())
            {
                var character = new CharacterRecord
                {
                    CharacterKey = entry.GetString("characterKey"),
                    CharacterName = entry.GetString("characterName"),
                    RealmName = entry.GetString("realmName"),
                };
                var professions = entry.GetTable("professions");
                if (professions != null)
                {
                    foreach (var profession in professions.ArrayValues)
                    {
                        var text = LuaTable.ToStringValue(profession);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            character.Professions.Add(text);
                        }
                    }
                }
                snapshot.Characters.Add(character);
            }
        }

        var cooldownTable = table.GetTable("cooldowns");
        if (cooldownTable != null)
        {
            foreach (var entry in cooldownTable.ArrayValues.OfType<LuaTable>())
            {
                snapshot.Cooldowns.Add(new CooldownRecord
                {
                    Id = entry.GetLong("id"),
                    CharacterKey = entry.GetString("characterKey"),
                    CharacterName = entry.GetString("characterName"),
                    RealmName = entry.GetString("realmName"),
                    Profession = entry.GetString("profession"),
                    ItemName = entry.GetString("itemName"),
                    Expansion = entry.GetString("expansion"),
                    RecipeId = entry.GetNullableLong("recipeID"),
                    CurrentCharges = entry.GetNullableInt("currentCharges"),
                    MaxCharges = entry.GetNullableInt("maxCharges"),
                    DurationSeconds = entry.GetInt("durationSeconds"),
                    ReadyTime = entry.GetLong("readyTime"),
                    Enabled = entry.GetBool("enabled", true),
                    Source = entry.GetString("source"),
                    ConcentrationCurrent = entry.GetNullableInt("concentrationCurrent"),
                    ConcentrationMaximum = entry.GetNullableInt("concentrationMaximum"),
                    ConcentrationScanTime = entry.GetNullableLong("concentrationScanTime"),
                });
            }
        }

        return snapshot;
    }

    private static DesktopSnapshot MapLegacySnapshot(LuaTable dbTable, string filePath, DateTime generatedAtUtc)
    {
        var snapshot = new DesktopSnapshot
        {
            SourceFile = filePath,
            SchemaVersion = 0,
            GeneratedAt = new DateTimeOffset(generatedAtUtc).ToUnixTimeSeconds(),
        };

        var characterTable = dbTable.GetTable("characters");
        if (characterTable != null)
        {
            foreach (var pair in characterTable.Fields)
            {
                if (pair.Value is not LuaTable entry)
                {
                    continue;
                }

                var character = new CharacterRecord
                {
                    CharacterKey = entry.GetString("key"),
                    CharacterName = entry.GetString("name"),
                    RealmName = entry.GetString("realm"),
                };
                var professions = entry.GetTable("professions");
                if (professions != null)
                {
                    foreach (var professionPair in professions.Fields)
                    {
                        if (professionPair.Value is bool isKnown && isKnown && !string.IsNullOrWhiteSpace(professionPair.Key))
                        {
                            character.Professions.Add(professionPair.Key);
                        }
                    }
                }
                character.Professions.Sort(StringComparer.Ordinal);
                snapshot.Characters.Add(character);
            }
        }

        var cooldownTable = dbTable.GetTable("cooldowns");
        if (cooldownTable != null)
        {
            foreach (var entry in cooldownTable.ArrayValues.OfType<LuaTable>())
            {
                snapshot.Cooldowns.Add(new CooldownRecord
                {
                    Id = entry.GetLong("id"),
                    CharacterKey = entry.GetString("characterKey"),
                    CharacterName = entry.GetString("characterName"),
                    RealmName = entry.GetString("realmName"),
                    Profession = entry.GetString("profession"),
                    ItemName = entry.GetString("itemName"),
                    Expansion = entry.GetString("expansion"),
                    RecipeId = entry.GetNullableLong("recipeID"),
                    CurrentCharges = entry.GetNullableInt("currentCharges"),
                    MaxCharges = entry.GetNullableInt("maxCharges"),
                    DurationSeconds = entry.GetInt("durationSeconds"),
                    ReadyTime = entry.GetLong("readyTime"),
                    Enabled = entry.GetBool("enabled", true),
                    Source = entry.GetString("source"),
                    ConcentrationCurrent = entry.GetNullableInt("concentrationCurrent"),
                    ConcentrationMaximum = entry.GetNullableInt("concentrationMaximum"),
                    ConcentrationScanTime = entry.GetNullableLong("concentrationScanTime"),
                });
            }
        }

        return snapshot;
    }

    private sealed class LuaTableParser
    {
        private readonly string _text;
        private int _index;

        public LuaTableParser(string text, int index)
        {
            _text = text;
            _index = index;
        }

        public bool TryParseValue(out object? value)
        {
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                value = null;
                return false;
            }

            var c = _text[_index];
            if (c == '{')
            {
                value = ParseTable();
                return true;
            }
            if (c == '"')
            {
                value = ParseString();
                return true;
            }
            if (char.IsDigit(c) || c == '-')
            {
                value = ParseNumber();
                return true;
            }
            if (IsIdentifierStart(c))
            {
                var identifier = ParseIdentifier();
                value = identifier switch
                {
                    "true" => true,
                    "false" => false,
                    "nil" => null,
                    _ => identifier,
                };
                return true;
            }

            value = null;
            return false;
        }

        private LuaTable ParseTable()
        {
            var table = new LuaTable();
            Expect('{');
            SkipWhitespace();

            while (_index < _text.Length && _text[_index] != '}')
            {
                var beforeEntry = _index;
                SkipWhitespace();
                if (_text[_index] == ',')
                {
                    _index++;
                    SkipWhitespace();
                    continue;
                }

                if (_text[_index] == '[')
                {
                    var key = ParseBracketKey();
                    SkipWhitespace();
                    if (_index < _text.Length && _text[_index] == '=')
                    {
                        _index++;
                        if (TryParseValue(out var namedValue) && !string.IsNullOrWhiteSpace(key))
                        {
                            table.Fields[key] = namedValue;
                        }
                    }
                }
                else if (IsIdentifierStart(_text[_index]))
                {
                    var save = _index;
                    var key = ParseIdentifier();
                    SkipWhitespace();
                    if (_index < _text.Length && _text[_index] == '=')
                    {
                        _index++;
                        if (TryParseValue(out var namedValue))
                        {
                            table.Fields[key] = namedValue;
                        }
                    }
                    else
                    {
                        _index = save;
                        if (TryParseValue(out var arrayValue))
                        {
                            table.ArrayValues.Add(arrayValue);
                        }
                    }
                }
                else
                {
                    if (TryParseValue(out var arrayValue))
                    {
                        table.ArrayValues.Add(arrayValue);
                    }
                }

                SkipWhitespace();
                if (_index < _text.Length && (_text[_index] == ',' || _text[_index] == ';'))
                {
                    _index++;
                    SkipWhitespace();
                }
                if (_index == beforeEntry)
                {
                    _index++;
                }
            }

            Expect('}');
            return table;
        }

        private string ParseString()
        {
            Expect('"');
            var buffer = new System.Text.StringBuilder();
            while (_index < _text.Length)
            {
                var c = _text[_index++];
                if (c == '"')
                {
                    break;
                }
                if (c == '\\' && _index < _text.Length)
                {
                    var escaped = _text[_index++];
                    buffer.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        _ => escaped,
                    });
                }
                else
                {
                    buffer.Append(c);
                }
            }
            return buffer.ToString();
        }

        private object ParseNumber()
        {
            var start = _index;
            if (_text[_index] == '-')
            {
                _index++;
            }
            while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
            {
                _index++;
            }
            var numericText = _text[start.._index];
            if (numericText.Contains('.'))
            {
                return double.Parse(numericText, CultureInfo.InvariantCulture);
            }
            return long.Parse(numericText, CultureInfo.InvariantCulture);
        }

        private string ParseIdentifier()
        {
            var start = _index;
            _index++;
            while (_index < _text.Length && IsIdentifierPart(_text[_index]))
            {
                _index++;
            }
            return _text[start.._index];
        }

        private string ParseBracketKey()
        {
            Expect('[');
            SkipWhitespace();

            string key;
            if (_index < _text.Length && _text[_index] == '"')
            {
                key = ParseString();
            }
            else
            {
                var valueStart = _index;
                while (_index < _text.Length && _text[_index] != ']')
                {
                    _index++;
                }
                key = _text[valueStart.._index].Trim();
            }

            Expect(']');
            return key;
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length)
            {
                var c = _text[_index];
                if (!char.IsWhiteSpace(c))
                {
                    break;
                }
                _index++;
            }
        }

        private void Expect(char c)
        {
            SkipWhitespace();
            if (_index < _text.Length && _text[_index] == c)
            {
                _index++;
            }
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        private static bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
    }

    private sealed class LuaTable
    {
        public Dictionary<string, object?> Fields { get; } = new(StringComparer.Ordinal);
        public List<object?> ArrayValues { get; } = [];

        public string GetString(string key)
        {
            return ToStringValue(GetValue(key));
        }

        public int GetInt(string key)
        {
            return ToIntValue(GetValue(key));
        }

        public int? GetNullableInt(string key)
        {
            var value = GetValue(key);
            return value == null ? null : ToIntValue(value);
        }

        public long GetLong(string key)
        {
            return ToLongValue(GetValue(key));
        }

        public long? GetNullableLong(string key)
        {
            var value = GetValue(key);
            return value == null ? null : ToLongValue(value);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetValue(key);
            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                _ => defaultValue,
            };
        }

        public LuaTable? GetTable(string key)
        {
            return GetValue(key) as LuaTable;
        }

        private object? GetValue(string key)
        {
            Fields.TryGetValue(key, out var value);
            return value;
        }

        public static string ToStringValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string s => s,
                bool b => b ? "true" : "false",
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        private static int ToIntValue(object? value)
        {
            return value switch
            {
                null => 0,
                int i => i,
                long l => (int)l,
                double d => (int)Math.Round(d),
                string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble) => (int)Math.Round(parsedDouble),
                _ => 0,
            };
        }

        private static long ToLongValue(object? value)
        {
            return value switch
            {
                null => 0,
                int i => i,
                long l => l,
                double d => (long)Math.Round(d),
                string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble) => (long)Math.Round(parsedDouble),
                _ => 0,
            };
        }
    }
}
