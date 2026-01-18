namespace CmlLib.Core.Installer.Modpack;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class YamlConfiguration
{
    private readonly FileInfo _file;
    private Dictionary<string, object?> _root = new();

    private static readonly IDeserializer _deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    private static readonly ISerializer _serializer =
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

    public YamlConfiguration(string path)
    {
        _file = new FileInfo(path);
    }

   

    // =========================
    // Load / Save
    // =========================
    
    public static YamlConfiguration LoadConfiguration(string path)
    {
        var config = new YamlConfiguration(path);
        config.Load();
        return config;
    }
    
    public void Load()
    {
        if (!_file.Exists)
        {
            _file.Directory?.Create();
            _root = new Dictionary<string, object?>();
            return;
        }

        using var reader = new StreamReader(_file.FullName);
        var raw = _deserializer.Deserialize<object>(reader);

        _root = raw is Dictionary<object, object> dict
            ? NormalizeDictionary(dict)
            : new Dictionary<string, object?>();
    }

    public void Save()
    {
        using var writer = new StreamWriter(_file.FullName);
        _serializer.Serialize(writer, _root);
    }

    // =========================
    // Path Logic
    // =========================

    private Dictionary<string, object?>? GetSectionInternal(string path, bool create)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = _root;

        foreach (var key in parts)
        {
            if (!current.TryGetValue(key, out var next))
            {
                if (!create) return null;
                var section = new Dictionary<string, object?>();
                current[key] = section;
                current = section;
                continue;
            }

            if (next is Dictionary<string, object?> dict)
            {
                current = dict;
            }
            else
            {
                if (!create) return null;
                var section = new Dictionary<string, object?>();
                current[key] = section;
                current = section;
            }
        }

        return current;
    }

    private static (string parent, string key) SplitPath(string path)
    {
        var idx = path.LastIndexOf('.');
        return idx == -1
            ? ("", path)
            : (path[..idx], path[(idx + 1)..]);
    }

    // =========================
    // Section API
    // =========================

    public Dictionary<string, object?>? GetSection(string path)
        => GetSectionInternal(path, false);

    public Dictionary<string, object?> CreateSection(string path)
        => GetSectionInternal(path, true)!;

    // =========================
    // Set / Get
    // =========================

    public void Set(string path, object? value)
    {
        var (parentPath, key) = SplitPath(path);
        var parent = string.IsNullOrEmpty(parentPath)
            ? _root
            : GetSectionInternal(parentPath, true);

        if (parent == null) return;
        parent[key] = value;
    }

    public object? Get(string path)
    {
        var (parentPath, key) = SplitPath(path);
        var parent = string.IsNullOrEmpty(parentPath)
            ? _root
            : GetSectionInternal(parentPath, false);

        return parent != null && parent.TryGetValue(key, out var v) ? v : null;
    }

    public bool Contains(string path) => Get(path) != null;

    // =========================
    // Typed Getters
    // =========================

    public string? GetString(string path, string? def = null)
        => Get(path)?.ToString() ?? def;

    public int GetInt(string path, int def = 0)
        => Get(path) is int i ? i :
           int.TryParse(Get(path)?.ToString(), out var v) ? v : def;

    public bool GetBoolean(string path, bool def = false)
        => Get(path) is bool b ? b :
           bool.TryParse(Get(path)?.ToString(), out var v) ? v : def;

    public double GetDouble(string path, double def = 0)
        => Get(path) is double d ? d :
           double.TryParse(Get(path)?.ToString(), out var v) ? v : def;

    public List<T> GetList<T>(string path)
    {
        var obj = Get(path);
        if (obj is IEnumerable list)
        {
            var result = new List<T>();
            foreach (var item in list)
            {
                if (item is T t)
                    result.Add(t);
            }
            return result;
        }
        return new List<T>();
    }

    // =========================
    // Utility
    // =========================

    public IEnumerable<string> GetKeys(string path)
    {
        var section = GetSection(path);
        return section != null
            ? section.Keys
            : Enumerable.Empty<string>();
    }

    public void Remove(string path)
    {
        var (parentPath, key) = SplitPath(path);
        var parent = string.IsNullOrEmpty(parentPath)
            ? _root
            : GetSectionInternal(parentPath, false);

        parent?.Remove(key);
    }

    // =========================
    // Normalization
    // =========================

    private static Dictionary<string, object?> NormalizeDictionary(Dictionary<object, object> input)
    {
        var result = new Dictionary<string, object?>();

        foreach (var (key, value) in input)
        {
            var k = key.ToString()!;
            result[k] = NormalizeValue(value);
        }

        return result;
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            Dictionary<object, object> dict => NormalizeDictionary(dict),
            IList list => NormalizeList(list),
            _ => value
        };
    }

    private static List<object?> NormalizeList(IList input)
    {
        var result = new List<object?>();
        foreach (var item in input)
            result.Add(NormalizeValue(item));
        return result;
    }
}
