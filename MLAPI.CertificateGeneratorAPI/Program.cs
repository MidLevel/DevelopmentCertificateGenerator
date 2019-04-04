using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluffySpoon.AspNet.LetsEncrypt;
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
        public static bool GIST_REDIRECT = false;
        public static int KEY_QUEUE_SIZE = 20;
        
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

                if (args[i].ToLower() == "-gist-redirect")
                {
                    GIST_REDIRECT = true;
                }

                if (args[i].ToLower() == "-key-queue-size")
                {
                    KEY_QUEUE_SIZE = int.Parse(args[i + 1]);
                }
            }
            
            KeyGenerator.Start(KEY_SIZE, GENERATION_THREADS, KEY_QUEUE_SIZE);
            
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging(l => l.AddConsole(x => x.IncludeScopes = true))
                .UseKestrel(kestrelOptions => kestrelOptions.ConfigureHttpsDefaults(
                    httpsOptions => httpsOptions.ServerCertificateSelector = 
                        (c, s) => LetsEncryptRenewalService.Certificate))
                .UseUrls("http://cert.midlevel.io", 
                    "https://cert.midlevel.io")
                .UseStartup<Startup>();
    }
}