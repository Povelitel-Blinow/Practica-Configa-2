using System;
using System.Collections.Generic;
using System.IO;

namespace DependencyVisualizer
{
    /// <summary>Самостоятельно читает записи apt в формате deb822.</summary>
    public static class PackageIndex
    {
        /// <summary>Читает индекс один раз, выбирая точную версию корневого пакета.</summary>
        public static Dictionary<string, List<string>> ReadGraph(TextReader reader, string package, string version)
        {
            Dictionary<string, List<string>> nodes = new Dictionary<string, List<string>>();
            bool packageFound = false;
            Dictionary<string, string> fields;
            while ((fields = ReadRecord(reader)) != null)
            {
                if (!fields.ContainsKey("Package") || !fields.ContainsKey("Version"))
                    throw new InvalidDataException("В записи индекса отсутствует Package или Version.");
                string name = fields["Package"];
                if (name == package)
                {
                    packageFound = true;
                    if (fields["Version"] != version) continue;
                }
                if (nodes.ContainsKey(name)) continue;
                List<string> groups = new List<string>();
                AddDependencies(fields, "Pre-Depends", groups);
                AddDependencies(fields, "Depends", groups);
                nodes.Add(name, DependencyNames(groups));
            }
            if (!nodes.ContainsKey(package))
                throw new ArgumentException(packageFound ? "--version: версия не найдена: " + version
                    : "--package: пакет не найден: " + package);
            return nodes;
        }

        /// <summary>Включает все альтернативы как возможные рёбра, убирая ограничения и архитектуру.</summary>
        private static List<string> DependencyNames(List<string> groups)
        {
            List<string> names = new List<string>();
            foreach (string group in groups)
            {
                foreach (string alternative in group.Split('|'))
                {
                    string name = alternative.Trim();
                    int end = name.IndexOfAny(new char[] { ' ', '\t', ':', '(', '[' });
                    if (end >= 0) name = name.Substring(0, end);
                    if (name.Length == 0) throw new InvalidDataException("Пустое имя зависимости.");
                    if (!names.Contains(name)) names.Add(name);
                }
            }
            return names;
        }

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
