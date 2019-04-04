using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Certes;
using FluffySpoon.AspNet.LetsEncrypt;
using Markdig;
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
            // The following line adds the automatic renewal service.
            services.AddFluffySpoonLetsEncryptRenewalService(new LetsEncryptOptions()
            {
                Email = "2108U9@gmail.com", //LetsEncrypt will send you an e-mail here when the certificate is about to expire
                UseStaging = false, //switch to true for testing
                Domains = new[] {"cert.midlevel.io"},
                TimeUntilExpiryBeforeRenewal = TimeSpan.FromDays(30), //renew automatically 30 days before expiry
                CertificateSigningRequest = new CsrInfo() //these are your certificate details
                {
                    CountryName = "Sweden",
                    Locality = "SE",
                    Organization = "MidLevel",
                    OrganizationUnit = "MLAPI",
                    State = "SE"
                }
            });

            // The following line tells the library to persist the certificate to a file, so that if the server restarts, the certificate can be re-used without generating a new one.
            services.AddFluffySpoonLetsEncryptFileCertificatePersistence();

            // The following line tells the library to persist challenges in-memory. challenges are the "/.well-known" URL codes that LetsEncrypt will call.
            services.AddFluffySpoonLetsEncryptMemoryChallengePersistence();
            
            // File persistance
            services.AddFluffySpoonLetsEncryptFileCertificatePersistence();
            services.AddFluffySpoonLetsEncryptFileChallengePersistence();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseFluffySpoonLetsEncryptChallengeApprovalMiddleware();

            app.Run(async (context) =>
            {                
                await Task.Factory.StartNew(() =>
                {
                    GenerationLog log = new GenerationLog();
                    log.Entries.Add(new LogEntry("Serving " + context.Connection.RemoteIpAddress));
                    
                    DateTime startTime = DateTime.UtcNow;
                    DateTime endTime = DateTime.UtcNow.AddDays(30);

                    log.Entries.Add(new LogEntry("Generating issuer key pair..."));
                    RSAParameters issuerKeyPair = KeyGenerator.Get();
                
                    log.Entries.Add(new LogEntry("Generating certificate key pair..."));
                    RSAParameters certificateKeyPair = KeyGenerator.Get();

                    log.Entries.Add(new LogEntry("Generating certificate serial number..."));
                    byte[] serialNumber = new byte[20];
                    using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                    {
                        // Should ensure non negativity and make it smaller
                        // See https://tools.ietf.org/html/rfc3280#section-4.1.2.2
                        rng.GetBytes(serialNumber);
                    }

                    string issuerName = context.Request.Query.ContainsKey("issuer") ? context.Request.Query["issuer"].ToString() : "Unnamed Issuer";
                    string certificateName = context.Request.Query.ContainsKey("name") ? context.Request.Query["name"].ToString() : "Unnamed MLAPI Development Certificate";
                
                    log.Entries.Add(new LogEntry("Generating certificate serial number..."));
                    CertificateEmpire empire = Generator.GenerateCertificateEmpire(issuerKeyPair, certificateKeyPair, issuerName, certificateName, startTime, endTime, serialNumber, log);

                    Gist gist = null;
                    
                    if (Program.GITHUB_GIST_TOKEN != null)
                    {
                        log.Entries.Add(new LogEntry("Uploading gist..."));
                        GitHubClient github = new GitHubClient(new ProductHeaderValue("MLAPI.Certificate.Generator"));
                        github.Credentials = new Credentials(Program.GITHUB_GIST_TOKEN);
                        
                        gist = github.Gist.Create(new NewGist()
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

                        log.EndTime = DateTime.UtcNow;
                        
                        if (Program.GIST_REDIRECT)
                        {
                            context.Response.Redirect(gist.HtmlUrl);
                        }
                    }
                    
                    if (!Program.GIST_REDIRECT)
                    {
                        log.EndTime = DateTime.UtcNow;
                        
                        using (StreamWriter writer = new StreamWriter(context.Response.Body))
                        {
                            MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
                            
                            writer.Write(Markdown.ToHtml(Generator.GetMarkdownInstructions(empire, gist == null ? null : gist.HtmlUrl), pipeline));
                            writer.Write(Markdown.ToHtml(log.Serialize()), pipeline);
                        }
                    }
                });
            });
        }
    }
}