﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Anetta.Extensions;
using App.Metrics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Sillycore.Infrastructure;
using Sillycore.Web.Abstractions;
using Sillycore.Web.Filters;
using Sillycore.Web.Configuration;

namespace Sillycore.Web
{
    public class SillycoreStartup
    {
        public IConfiguration Configuration { get; }

        public SillycoreStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IServiceProvider ServiceProvider { get; set; }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            foreach (ServiceDescriptor descriptor in SillycoreAppBuilder.Instance.Services)
            {
                services.Add(descriptor);
            }

            MetricsElasticSearchOptions metricsElasticSearchOptions = new MetricsElasticSearchOptions();
            Configuration.Bind("MetricsElasticSearchOptions", metricsElasticSearchOptions);

            IMetricsBuilder metricsBuilder = AppMetrics.CreateDefaultBuilder();

            metricsBuilder.Configuration.Configure(o =>
            {
                o.Enabled = true;
                o.ReportingEnabled = true;
                o.AddAppTag(SillycoreAppBuilder.Instance.DataStore.Get<string>(Constants.ApplicationName));
            });

            if (metricsElasticSearchOptions.Enabled)
            {
                metricsBuilder.Report.ToElasticsearch(o =>
                {
                    o.Elasticsearch.BaseUri = new Uri(metricsElasticSearchOptions.BaseUri);
                    o.Elasticsearch.Index = metricsElasticSearchOptions.Index;
                    o.FlushInterval = TimeSpan.FromSeconds(metricsElasticSearchOptions.FlushIntervalInSeconds);
                });
            }

            services.AddMetrics(metricsBuilder);
            services.AddMetricsTrackingMiddleware();
            services.AddMetricsReportScheduler();

            services.AddMvc()
                .AddMetrics()
                .AddApplicationPart(Assembly.GetEntryAssembly())
                .AddApplicationPart(GetType().Assembly)
                .AddMvcOptions(o =>
                {
                    o.InputFormatters.RemoveType<XmlDataContractSerializerInputFormatter>();
                    o.InputFormatters.RemoveType<XmlSerializerInputFormatter>();

                    o.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                    o.OutputFormatters.RemoveType<StreamOutputFormatter>();
                    o.OutputFormatters.RemoveType<StringOutputFormatter>();
                    o.OutputFormatters.RemoveType<XmlDataContractSerializerOutputFormatter>();
                    o.OutputFormatters.RemoveType<XmlSerializerOutputFormatter>();

                    o.Filters.Add<GlobalExceptionFilter>();
                })
                .AddJsonOptions(o =>
                {
                    o.SerializerSettings.ContractResolver = SillycoreApp.JsonSerializerSettings.ContractResolver;
                    o.SerializerSettings.Formatting = SillycoreApp.JsonSerializerSettings.Formatting;
                    o.SerializerSettings.NullValueHandling = SillycoreApp.JsonSerializerSettings.NullValueHandling;
                    o.SerializerSettings.DefaultValueHandling = SillycoreApp.JsonSerializerSettings.DefaultValueHandling;
                    o.SerializerSettings.ReferenceLoopHandling = SillycoreApp.JsonSerializerSettings.ReferenceLoopHandling;
                    o.SerializerSettings.DateTimeZoneHandling = SillycoreApp.JsonSerializerSettings.DateTimeZoneHandling;
                    o.SerializerSettings.Converters.Clear();

                    foreach (JsonConverter converter in SillycoreApp.JsonSerializerSettings.Converters)
                    {
                        o.SerializerSettings.Converters.Add(converter);
                    }
                });

            SillycoreAppBuilder.Instance.Services = services;
            ServiceProvider = services.BuildAnettaServiceProvider();
            SillycoreAppBuilder.Instance.DataStore.Set(Sillycore.Constants.ServiceProvider, ServiceProvider);
            return ServiceProvider;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var forwardOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All,
                RequireHeaderSymmetry = false
            };

            forwardOptions.KnownNetworks.Clear();
            forwardOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardOptions);

            List<IApplicationConfigurator> configurators = new List<IApplicationConfigurator>();

            foreach (Type type in AssemblyScanner.GetAllTypesOfInterface<IApplicationConfigurator>())
            {
                IApplicationConfigurator configurator = (IApplicationConfigurator)Activator.CreateInstance(type);
                configurators.Add(configurator);
            }

            foreach (IApplicationConfigurator configurator in configurators.OrderBy(c => c.Order))
            {
                configurator.Configure(app, env, SillycoreAppBuilder.Instance.Configuration, app.ApplicationServices);
            }

            app.UseMetricsAllMiddleware();
            app.UseMvc(r =>
            {
                if (SillycoreAppBuilder.Instance.DataStore.Get<bool>(Constants.RedirectRootToSwagger))
                {
                    r.MapRoute(name: "Default",
                        template: "",
                        defaults: new { controller = "Help", action = "Index" });
                }
                else
                {
                    r.MapRoute(name: "Default",
                        template: "",
                        defaults: new { controller = "Home", action = "Index" });
                }
            });
        }
    }
}