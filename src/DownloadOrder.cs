using System.Collections.Generic;
using System.IO;

namespace DependencyVisualizer
{
    /// <summary>Порядок загрузки: зависимости перед использующим их пакетом.</summary>
    public class DownloadOrder
    {
        public List<string> Packages = new List<string>();
        public bool HasCycle;

        /// <summary>Обходит граф стеком и добавляет пакет после его зависимостей.</summary>
        public static DownloadOrder Build(DependencyGraph graph, string root)
        {
            DownloadOrder result = new DownloadOrder();
            Stack<string> pending = new Stack<string>();
            Dictionary<string, int> positions = new Dictionary<string, int>();
            HashSet<string> completed = new HashSet<string>();
            pending.Push(root);
            positions.Add(root, 0);
            while (pending.Count > 0)
            {
                string package = pending.Peek();
                List<string> dependencies = graph.Nodes[package];
                int position = positions[package];
                if (position == dependencies.Count)
                {
                    pending.Pop();
                    completed.Add(package);
                    result.Packages.Add(package);
                    continue;
                }
                string dependency = dependencies[position];
                positions[package]++;
                if (completed.Contains(dependency)) continue;
                if (positions.ContainsKey(dependency)) result.HasCycle = true;
                else
                {
                    positions.Add(dependency, 0);
                    pending.Push(dependency);
                }
            }
            return result;
        }

        /// <summary>Выводит порядок и предупреждения о цикле или неполных данных.</summary>
        public void Write(TextWriter output, DependencyGraph graph)
        {
            if (HasCycle)
                output.WriteLine("Предупреждение: найден цикл, строгий порядок загрузки невозможен.");
            if (graph.Missing.Count > 0)
                output.WriteLine("Предупреждение: порядок неполный, для части пакетов нет данных в индексе.");
            output.WriteLine("Порядок загрузки:");
            foreach (string package in Packages)
            {
                string note = graph.Missing.Contains(package) ? " (нет данных в выбранном индексе)" : "";
                output.WriteLine(package + note);
            }
        }
    }
}
