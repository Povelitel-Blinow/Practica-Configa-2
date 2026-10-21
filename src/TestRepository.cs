using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DependencyVisualizer
{
    /// <summary>Читает тестовый граф из строк вида A: B C.</summary>
    public static class TestRepository
    {
        /// <summary>Проверяет формат, повторы вершин и ссылки на отсутствующие пакеты.</summary>
        public static Dictionary<string, List<string>> Read(TextReader reader)
        {
            Dictionary<string, List<string>> nodes = new Dictionary<string, List<string>>();
            string line;
            int number = 0;
            while ((line = reader.ReadLine()) != null)
            {
                number++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(':');
                if (parts.Length != 2)
                    throw new InvalidDataException("--repository: ожидается A: B C, строка " + number);
                string name = parts[0].Trim();
                ValidateName(name);
                List<string> dependencies = new List<string>();
                foreach (string dependency in parts[1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    ValidateName(dependency);
                    if (!dependencies.Contains(dependency)) dependencies.Add(dependency);
                }
                if (!nodes.TryAdd(name, dependencies))
                    throw new InvalidDataException("--repository: повтор пакета " + name);
            }
            foreach (List<string> dependencies in nodes.Values)
            {
                foreach (string dependency in dependencies)
                {
                    if (!nodes.ContainsKey(dependency))
                        throw new InvalidDataException("--repository: нет описания пакета " + dependency);
                }
            }
            return nodes;
        }

        /// <summary>Разрешает только большие латинские буквы.</summary>
        private static void ValidateName(string name)
        {
            if (!Regex.IsMatch(name, @"\A[A-Z]+\z"))
                throw new InvalidDataException("--repository: некорректное имя пакета: " + name);
        }
    }
}
