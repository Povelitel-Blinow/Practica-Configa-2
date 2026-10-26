using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace DependencyVisualizer.Tests
{
    /// <summary>Консольные проверки этапов 1–4 без сторонних библиотек.</summary>
    internal static class Program
    {
        private static int failures;
        private static int checks;

        /// <summary>Возвращает индекс без сети для проверки полного пути приложения.</summary>
        private sealed class IndexHandler : HttpMessageHandler
        {
            /// <summary>Формирует обычный или сжатый HTTP-ответ.</summary>
            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken token)
            {
                byte[] data = Encoding.UTF8.GetBytes("Package: bash\nVersion: 1.0-1\n"
                    + "Depends: libc6 (>= 2.38) | libc6-alt\n\n"
                    + "Package: libc6\nVersion: 2.38\nDepends: loader\n\n"
                    + "Package: loader\nVersion: 1.0\nDepends: libc6\n");
                if (request.RequestUri.AbsolutePath.EndsWith(".gz"))
                {
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        using (GZipStream archive = new GZipStream(buffer, CompressionMode.Compress, true))
                        {
                            archive.Write(data);
                        }
                        data = buffer.ToArray();
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(data)
                };
            }

            /// <summary>Запрещает незапланированный асинхронный путь в тестах.</summary>
            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken token)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>Проверяет выбор версии, переносы, альтернативы и отсутствие зависимостей.</summary>
        private static void CheckIndex()
        {
            const string index = "Package: bash\nVersion: 0.1\nDepends: wrong\n\n"
                + "Package: bash\nVersion: 1.0-1\nPre-Depends: base\n"
                + "Depends: libc6:any (>= 2.38),\n libtinfo6 | fallback\nRecommends: ignored\n\n"
                + "Package: empty\nVersion: 1.0\n";
            List<string> result = PackageIndex.Read(new StringReader(index), "bash", "1.0-1");
            Assert(string.Join(";", result) == "base;libc6:any (>= 2.38);libtinfo6 | fallback", "deb822");
            Assert(PackageIndex.Read(new StringReader(index), "empty", "1.0").Count == 0, "empty");
            CheckIndexError(index, "bash", "9.0", "--version");
            CheckIndexError(index, "missing", "1.0", "--package");
            CheckIndexError("broken", "bash", "1.0", "Packages");
            Assert(Repository.GetIndexAddress("https://example.org/ubuntu").AbsolutePath
                == "/ubuntu/dists/noble/main/binary-amd64/Packages.gz", "root URL");
            Assert(Repository.GetIndexAddress("https://example.org/Packages").AbsolutePath
                == "/Packages", "index URL");
        }

        /// <summary>Проверяет диагностические сообщения парсера.</summary>
        private static void CheckIndexError(string index, string package, string version, string expected)
        {
            try
            {
                PackageIndex.Read(new StringReader(index), package, version);
                Assert(false, expected);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
            {
                Assert(exception.Message.Contains(expected), expected);
            }
        }

        /// <summary>Учитывает результат проверки.</summary>
        private static void Assert(bool condition, string name)
        {
            checks++;
            if (!condition)
            {
                failures++;
                Console.WriteLine("FAIL: " + name);
            }
        }

        /// <summary>Запускает проверки успешного ввода и ошибок всех параметров.</summary>
        private static int Main()
        {
            Check(ValidArgs(), 0, "Порядок загрузки:");
            string[] plain = ValidArgs();
            plain[3] = "https://example.org/Packages";
            Check(plain, 0, "найден цикл");
            CheckIndex();
            CheckGraphs();
            CheckDownloadOrders();
            Check(new string[] { "--help" }, 0, "--package");
            Check(new string[0], 1, "--package");
            Check(new string[] { "--unknown", "value" }, 1, "Неизвестная");
            Check(new string[] { "--package" }, 1, "--package");
            Check(new string[] { "--package", "--version" }, 1, "--package");
            Check(new string[] { "--package", "bash", "--package", "curl" }, 1, "Повтор");
            CheckOptions();
            CheckValue(1, "Bad Name", "--package");
            CheckValue(1, "a", "--package");
            CheckValue(3, "ftp://example.org", "--repository");
            CheckValue(3, "relative/path", "--repository");
            CheckValue(5, "yes", "--test-mode");
            CheckValue(7, "latest", "--version");
            CheckValue(7, "1.0 bad", "--version");
            CheckValue(7, "1.0-", "--version");
            CheckTestRepository();
            Console.WriteLine("Проверок: " + checks + "; ошибок: " + failures);
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Проверяет пропуск, пустое значение и повтор каждой опции.</summary>
        private static void CheckOptions()
        {
            const int PairLength = 2;
            string[] valid = ValidArgs();
            for (int i = 0; i < valid.Length; i += PairLength)
            {
                string[] missing = new string[valid.Length - PairLength];
                Array.Copy(valid, 0, missing, 0, i);
                Array.Copy(valid, i + PairLength, missing, i, missing.Length - i);
                Check(missing, 1, valid[i]);
                CheckValue(i + 1, "", valid[i]);
                CheckValue(i + 1, "  ", valid[i]);
                string[] duplicate = new string[valid.Length + PairLength];
                Array.Copy(valid, duplicate, valid.Length);
                duplicate[valid.Length] = valid[i];
                duplicate[valid.Length + 1] = valid[i + 1];
                Check(duplicate, 1, "Повтор");
            }
        }

        /// <summary>Проверяет локальный файл, путь с пробелами и отсутствующий файл.</summary>
        private static void CheckTestRepository()
        {
            string path = Path.Combine(Path.GetTempPath(), "repository " + Guid.NewGuid() + ".txt");
            try
            {
                File.WriteAllText(path, "A: B\nB:\n");
                string[] args = ValidArgs();
                args[1] = "A";
                args[3] = path;
                args[5] = "true";
                Check(args, 0, "Порядок загрузки:" + Environment.NewLine + "B" + Environment.NewLine + "A");
                args[1] = "bash";
                Check(args, 1, "--package");
                args[1] = "A";
                args[3] = path + ".missing";
                Check(args, 1, "--repository");
                args[3] = Path.GetDirectoryName(path);
                Check(args, 1, "--repository");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Проверяет DFS, цикл, одиночную вершину и ошибочную ссылку.</summary>
        private static void CheckGraphs()
        {
            const string source = "A: B C\nB: D\nC: D\nD: A\nZ:\n";
            DependencyGraph graph = DependencyGraph.Build(TestRepository.Read(new StringReader(source)), "A");
            StringWriter output = new StringWriter();
            graph.Write(output);
            Assert(output.ToString().Replace("\r", "") == "A: B C\nB: D\nD: A\nC: D\n", "DFS and cycle");
            graph = DependencyGraph.Build(TestRepository.Read(new StringReader("A: A\n")), "A");
            Assert(graph.Nodes.Count == 1 && graph.Nodes["A"][0] == "A", "self cycle");
            graph = DependencyGraph.Build(TestRepository.Read(new StringReader("A:\n")), "A");
            Assert(graph.Nodes.Count == 1 && graph.Nodes["A"].Count == 0, "single node");
            try
            {
                TestRepository.Read(new StringReader("A: B\n"));
                Assert(false, "undefined node");
            }
            catch (InvalidDataException)
            {
                Assert(true, "undefined node");
            }
            const string index = "Package: aa\nVersion: 1\nDepends: wrong\n\n"
                + "Package: aa\nVersion: 2\nDepends: bb\n\nPackage: bb\nVersion: 3\nDepends: cc\n";
            graph = DependencyGraph.Build(PackageIndex.ReadGraph(new StringReader(index), "aa", "2"), "aa");
            Assert(graph.Nodes.Count == 3 && graph.Nodes["bb"][0] == "cc"
                && graph.Missing.Contains("cc"), "apt version, transitivity and missing node");
        }

        /// <summary>Проверяет порядок, общие зависимости, циклы и неполный индекс.</summary>
        private static void CheckDownloadOrders()
        {
            CheckOrder("A: B C\nB: D\nC: D\nD:\nZ:\n", "D B C A", false);
            CheckOrder("A: B\nB: C\nC:\n", "C B A", false);
            CheckOrder("A:\n", "A", false);
            CheckOrder("A: B\nB: C\nC: A\n", "C B A", true);
            CheckOrder("A: A\n", "A", true);
            Dictionary<string, List<string>> source = new Dictionary<string, List<string>>();
            source.Add("A", new List<string> { "B" });
            DependencyGraph graph = DependencyGraph.Build(source, "A");
            DownloadOrder order = DownloadOrder.Build(graph, "A");
            StringWriter output = new StringWriter();
            order.Write(output, graph);
            Assert(string.Join(" ", order.Packages) == "B A" && !order.HasCycle, "missing order");
            Assert(output.ToString().Contains("порядок неполный")
                && output.ToString().Contains("B (нет данных в выбранном индексе)"), "missing warning");
        }

        /// <summary>Сравнивает порядок и проверяет предупреждение о цикле.</summary>
        private static void CheckOrder(string source, string expected, bool cycle)
        {
            DependencyGraph graph = DependencyGraph.Build(TestRepository.Read(new StringReader(source)), "A");
            DownloadOrder order = DownloadOrder.Build(graph, "A");
            StringWriter output = new StringWriter();
            order.Write(output, graph);
            Assert(string.Join(" ", order.Packages) == expected, "order: " + expected);
            Assert(order.HasCycle == cycle && output.ToString().Contains("найден цикл") == cycle, "cycle warning");
        }

        /// <summary>Создаёт корректный набор параметров.</summary>
        private static string[] ValidArgs()
        {
            return new string[]
            {
                "--package", "bash", "--repository", "https://archive.ubuntu.com/ubuntu/",
                "--test-mode", "false", "--version", "1.0-1"
            };
        }

        /// <summary>Проверяет некорректное значение одной настройки.</summary>
        private static void CheckValue(int index, string value, string message)
        {
            string[] args = ValidArgs();
            args[index] = value;
            Check(args, 1, message);
        }

        /// <summary>Сравнивает код завершения, сообщение и поток вывода.</summary>
        private static void Check(string[] args, int expectedCode, string expectedText)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            int code = DependencyVisualizer.Program.Run(args, output, error, new HttpClient(new IndexHandler()));
            string result = expectedCode == 0 ? output.ToString() : error.ToString();
            string other = expectedCode == 0 ? error.ToString() : output.ToString();
            checks++;
            if (code != expectedCode || !result.Contains(expectedText) || other.Length != 0)
            {
                failures++;
                Console.WriteLine("FAIL: " + string.Join(" ", args));
                Console.WriteLine("Код: " + code + "; stdout: " + output + "; stderr: " + error);
            }
        }
    }
}
