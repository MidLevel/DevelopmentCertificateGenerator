using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite.Internal.UrlMatches;
using Microsoft.Extensions.DependencyInjection;
using MLAPI.CertificateGeneratorCommon;
using Octokit;

namespace MLAPI.CertificateGeneratorAPI
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async (context) =>
            {                
                await Task.Factory.StartNew(() =>
                {
                    DateTime startTime = DateTime.UtcNow;
                    DateTime endTime = DateTime.UtcNow.AddDays(30);

                    Console.WriteLine("Getting issuer key pair...");
                    RSAParameters issuerKeyPair = KeyGenerator.Get();
                
                    Console.WriteLine("Getting certificate key pair...");
                    RSAParameters certificateKeyPair = KeyGenerator.Get();

                    byte[] serialNumber = new byte[20];

                    Console.WriteLine("Generating serial number...");
                    using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                    {
                        // Should ensure non negativity and make it smaller
                        // See https://tools.ietf.org/html/rfc3280#section-4.1.2.2
                        rng.GetBytes(serialNumber);
                    }

                    string issuerName = context.Request.Query.ContainsKey("issuer") ? context.Request.Query["issuer"].ToString() : "Unnamed Issuer";
                    string certificateName = context.Request.Query.ContainsKey("name") ? context.Request.Query["name"].ToString() : "Unnamed MLAPI Development Certificate";
                
                    Console.WriteLine("Creating empire...");
                    CertificateEmpire empire = Generator.GenerateCertificateEmpire(issuerKeyPair, certificateKeyPair, issuerName, certificateName, startTime, endTime, serialNumber);
                    
                    GitHubClient github = new GitHubClient(new ProductHeaderValue("MLAPI.Certificate.Generator"));
                    github.Credentials = new Credentials(Program.GITHUB_GIST_TOKEN);
                    
                    Console.WriteLine("Uploading results...");
                    Gist gist = github.Gist.Create(new NewGist()
                    {
                        Description = "http://cert.midlevel.io/ Generated on " + DateTime.UtcNow + " by " + context.Connection.RemoteIpAddress,
                        Files =
                        {
                            {
                                "quickstart.md",
                                Generator.GetMarkdownInstructions(empire)
                            }
                        },
                        Public = false
                    }).Result;

                    context.Response.Redirect(gist.HtmlUrl);
                });
            });
        }
    }
}