﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignService.SigningTools;
using SignService.Utils;

namespace SignService
{
    public class Startup
    {
        readonly IHostingEnvironment environment;

        public Startup(IHostingEnvironment env)
        {
            environment = env;
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                //builder.AddUserSecrets();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.

            services.AddOptions();
            services.AddSingleton<IConfiguration>(Configuration);

            services.Configure<Settings>(Configuration);
            // If not specified, default to the tools\sdk directory
            services.Configure<Settings>(s => s.WinSdkBinDirectory = string.IsNullOrWhiteSpace(s.WinSdkBinDirectory) ? 
                Path.Combine(environment.ContentRootPath, @"tools\SDK") : 
                s.WinSdkBinDirectory);

            services.Configure<AadOptions>(Configuration.GetSection("Authentication:AzureAd"));
            
            services.AddSingleton<IAppxFileFactory, AppxFileFactory>();
            services.AddSingleton<ICodeSignService, SigntoolCodeSignService>();
            services.AddSingleton<ICodeSignService, PowerShellCodeSignService>();
            services.AddSingleton<ICodeSignService, VsixSignService>();

            services.AddSingleton<ISigningToolAggregate, SigningToolAggregate>(sp => new SigningToolAggregate(sp.GetServices<ICodeSignService>().ToList(), sp.GetService<ILogger<SigningToolAggregate>>(), sp.GetService<IOptionsSnapshot<Settings>>()));

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
            

            app.UseJwtBearerAuthentication(new JwtBearerOptions
            {
                Authority = Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"],
                Audience = Configuration["Authentication:AzureAd:Audience"]
            });

            app.UseMvc();
        }
    }
}
