using System;
using System.Collections.Generic;
using System.IO;

namespace DependencyVisualizer
{
    /// <summary>Достижимая часть графа в порядке обхода DFS.</summary>
    public class DependencyGraph
    {
        public Dictionary<string, List<string>> Nodes = new Dictionary<string, List<string>>();
        public HashSet<string> Missing = new HashSet<string>();

        /// <summary>Обходит граф стеком; посещённые вершины не раскрываются повторно.</summary>
        public static DependencyGraph Build(Dictionary<string, List<string>> source, string root)
        {
            if (!source.ContainsKey(root)) throw new ArgumentException("--package: пакет не найден: " + root);
            DependencyGraph graph = new DependencyGraph();
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string package = pending.Pop();
                if (graph.Nodes.ContainsKey(package)) continue;
                List<string> dependencies;
                if (!source.TryGetValue(package, out dependencies))
                {
                    dependencies = new List<string>();
                    graph.Missing.Add(package);
                }
                graph.Nodes.Add(package, dependencies);
                for (int i = dependencies.Count - 1; i >= 0; i--)
                {
                    if (!graph.Nodes.ContainsKey(dependencies[i])) pending.Push(dependencies[i]);
                }
            }
            return graph;
        }

        /// <summary>Печатает списки смежности, сохраняя рёбра циклов.</summary>
        public void Write(TextWriter output)
        {
            foreach (KeyValuePair<string, List<string>> node in Nodes)
            {
                string value = string.Join(" ", node.Value);
                if (Missing.Contains(node.Key)) value = "(нет данных в выбранном индексе)";
                output.WriteLine(node.Key + ":" + (value.Length == 0 ? "" : " " + value));
            }
        }
    }
}
