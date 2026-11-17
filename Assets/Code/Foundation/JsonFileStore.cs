using System;
using System.IO;
using UnityEngine;
namespace TravelApp
{
    public interface ISaveStore { SaveData Load(); void Save(SaveData data); }
    public sealed class JsonFileStore : ISaveStore
    {
        readonly string path;
        public string Warning { get; private set; }
        bool protectUnreadableFiles;
        public JsonFileStore(string path) { this.path = path; }
        public SaveData Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new SaveData();
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var json = File.ReadAllText(candidate);
                    if (!json.TrimStart().StartsWith("{" ) || !json.Contains("\"version\"")) throw new InvalidDataException("Invalid save file.");
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (!Valid(data)) throw new InvalidDataException("Unsupported or damaged save.");
                    if (candidate != path) { Warning = "Recovered your data from the backup save."; protectUnreadableFiles = true; }
                    return data;
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
                { Warning = "The local save could not be read. Original files will be preserved."; }
            }
            protectUnreadableFiles = true;
            return new SaveData();
        }
        static bool Valid(SaveData data)
        {
            if (data == null || data.version != 1 || data.accounts == null || data.reviews == null) return false;
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in data.accounts)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.username) || !names.Add(a.username.Trim()) || a.cart == null || a.bookings == null ||
                    !string.IsNullOrEmpty(a.passwordHash) && string.IsNullOrEmpty(a.salt)) return false;
                if (a.cart.Exists(i => i == null || string.IsNullOrEmpty(i.productId) || !ProductQuery.Date(i.startDate, out _) || !ProductQuery.Date(i.endDate, out _))) return false;
                foreach (var b in a.bookings)
                    if (b == null || string.IsNullOrEmpty(b.id) || b.items == null || b.items.Exists(i => i == null || string.IsNullOrEmpty(i.productId) || i.priceCents < 0)) return false;
            }
            return !data.reviews.Exists(r => r == null || string.IsNullOrEmpty(r.productId) || string.IsNullOrEmpty(r.username) || r.rating < 1 || r.rating > 5);
        }
        public void Save(SaveData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (protectUnreadableFiles)
            {
                var suffix = ".recovered-" + Guid.NewGuid().ToString("N");
                if (File.Exists(path)) File.Copy(path, path + suffix);
                if (File.Exists(path + ".bak")) File.Copy(path + ".bak", path + ".bak" + suffix);
                // The backup is known good when Load recovered from it.
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                protectUnreadableFiles = false;
            }
            else if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}
