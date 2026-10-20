using System;
using System.Collections.Generic;
using System.IO;

namespace DependencyVisualizer
{
    /// <summary>Самостоятельно читает записи apt в формате deb822.</summary>
    public static class PackageIndex
    {
        /// <summary>Находит точную версию и возвращает группы прямых зависимостей.</summary>
        public static List<string> Read(TextReader reader, string package, string version)
        {
            bool packageFound = false;
            Dictionary<string, string> fields;
            while ((fields = ReadRecord(reader)) != null)
            {
                if (!fields.ContainsKey("Package") || fields["Package"] != package)
                {
                    continue;
                }
                packageFound = true;
                if (fields.ContainsKey("Version") && fields["Version"] == version)
                {
                    List<string> dependencies = new List<string>();
                    AddDependencies(fields, "Pre-Depends", dependencies);
                    AddDependencies(fields, "Depends", dependencies);
                    return dependencies;
                }
            }
            string message = packageFound ? "--version: версия не найдена: " + version
                : "--package: пакет не найден: " + package;
            throw new ArgumentException(message);
        }

        /// <summary>Читает одну запись, объединяя строки продолжения полей.</summary>
        private static Dictionary<string, string> ReadRecord(TextReader reader)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string current = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (fields.Count != 0) return fields;
                    continue;
                }
                if (char.IsWhiteSpace(line[0]))
                {
                    if (current == null) throw new InvalidDataException("Продолжение поля без заголовка.");
                    fields[current] += " " + line.Trim();
                    continue;
                }
                int separator = line.IndexOf(':');
                if (separator <= 0) throw new InvalidDataException("Некорректная строка индекса Packages.");
                current = line.Substring(0, separator);
                if (!fields.TryAdd(current, line.Substring(separator + 1).Trim()))
                {
                    throw new InvalidDataException("Повтор поля индекса: " + current);
                }
            }
            return fields.Count == 0 ? null : fields;
        }

        /// <summary>Разделяет группы по запятым, сохраняя альтернативы и ограничения версий.</summary>
        private static void AddDependencies(Dictionary<string, string> fields, string name, List<string> result)
        {
            if (!fields.ContainsKey(name) || fields[name].Length == 0) return;
            string[] groups = fields[name].Split(',');
            foreach (string group in groups)
            {
                string dependency = group.Trim();
                if (dependency.Length == 0) throw new InvalidDataException("Пустая группа зависимостей.");
                if (!result.Contains(dependency)) result.Add(dependency);
            }
        }
    }
}
