using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLAPI.CertificateGeneratorCommon;

namespace MLAPI.CertificateGeneratorAPI
{
    public class Program
    {
        public static int GENERATION_THREADS = Environment.ProcessorCount;
        public static int KEY_SIZE = 4096;
        public static string GITHUB_GIST_TOKEN = null;
        public static bool USE_GIST = false;
        
        public static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-gist-token")
                {
                    GITHUB_GIST_TOKEN = args[i + 1];
                }
                
                if (args[i].ToLower() == "-generation-threads")
                {
                    GENERATION_THREADS = int.Parse(args[i + 1]);
                }
                
                if (args[i].ToLower() == "-key-size")
                {
                    KEY_SIZE = int.Parse(args[i + 1]);
                }

                if (args[i].ToLower() == "-gist")
                {
                    USE_GIST = true;
                }
            }
            
            KeyGenerator.Start(KEY_SIZE, GENERATION_THREADS);
            
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://localhost:5000")
                .UseStartup<Startup>();
    }
}