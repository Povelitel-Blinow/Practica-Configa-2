using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace DependencyVisualizer
{
    /// <summary>Загружает индекс Ubuntu средствами стандартной библиотеки.</summary>
    public static class Repository
    {
        private const string DefaultIndex = "dists/noble/main/binary-amd64/Packages.gz";

        /// <summary>Определяет URL индекса; корень архива означает noble/main/amd64.</summary>
        public static Uri GetIndexAddress(string repository)
        {
            Uri address = new Uri(repository);
            string path = address.AbsolutePath;
            if (path.EndsWith("/Packages") || path.EndsWith("/Packages.gz")) return address;
            Uri directory = new Uri(repository.TrimEnd('/') + "/");
            return new Uri(directory, DefaultIndex);
        }

        /// <summary>Загружает и читает индекс; клиент передаётся также для автономных тестов.</summary>
        public static List<string> GetDependencies(Settings settings, HttpClient client)
        {
            Uri address = GetIndexAddress(settings.Repository);
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, address))
            using (HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (Stream source = response.Content.ReadAsStream())
                {
                    if (address.AbsolutePath.EndsWith(".gz"))
                    {
                        using (GZipStream archive = new GZipStream(source, CompressionMode.Decompress))
                        using (StreamReader reader = new StreamReader(archive))
                        {
                            return PackageIndex.Read(reader, settings.Package, settings.Version);
                        }
                    }
                    using (StreamReader reader = new StreamReader(source))
                    {
                        return PackageIndex.Read(reader, settings.Package, settings.Version);
                    }
                }
            }
        }
    }
}
