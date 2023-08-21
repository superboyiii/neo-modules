// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Neo.Plugins.RestServer.Middleware;
using Neo.Plugins.RestServer.Models.Error;
using Neo.Plugins.RestServer.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Mime;
using System.Net.Security;
using System.Reflection;

namespace Neo.Plugins.RestServer
{
    internal class RestWebServer
    {
        #region Globals

        private readonly RestServerSettings _settings;
        private IWebHost _host;

        #endregion

        public static bool IsRunning { get; private set; }

        public RestWebServer()
        {
            _settings = RestServerSettings.Current;
        }

        public void Start()
        {
            if (IsRunning) return;

            IsRunning = true;

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    // Web server configuration
                    options.AddServerHeader = false;
                    options.Limits.MaxConcurrentConnections = _settings.MaxConcurrentConnections;
                    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(_settings.KeepAliveTimeout);
                    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
                    options.Listen(_settings.BindAddress, unchecked((int)_settings.Port), listenOptions =>
                    {
                        if (string.IsNullOrEmpty(_settings.SslCertFile)) return;
                        listenOptions.UseHttps(_settings.SslCertFile, _settings.SslCertPassword, httpsOptions =>
                        {
                            if (_settings.TrustedAuthorities.Length == 0)
                            {
                                httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                                httpsOptions.ClientCertificateValidation = (cert, chain, err) =>
                                {
                                    if (err != SslPolicyErrors.None)
                                        return false;
                                    var authority = chain.ChainElements[^1].Certificate;
                                    return _settings.TrustedAuthorities.Any(a => a.Equals(authority.Thumbprint, StringComparison.OrdinalIgnoreCase));
                                };
                            }
                        });
                    });
                })
                .ConfigureServices(services =>
                {
                    // Server configuration
                    if (_settings.EnableCors)
                    {
                        if (_settings.AllowOrigins.Length == 0)
                            services.AddCors(options =>
                            {
                                options.AddDefaultPolicy(policy =>
                                {
                                    policy.AllowAnyOrigin()
                                    .AllowAnyHeader()
                                    .WithMethods("GET", "POST");
                                    // The CORS specification states that setting origins to "*" (all origins)
                                    // is invalid if the Access-Control-Allow-Credentials header is present.
                                    //.AllowCredentials() 
                                });
                            });
                        else
                            services.AddCors(options =>
                            {
                                options.AddDefaultPolicy(policy =>
                                {
                                    policy.WithOrigins(_settings.AllowOrigins)
                                    .AllowAnyHeader()
                                    .AllowCredentials()
                                    .WithMethods("GET", "POST");
                                });
                            });
                    }

                    services.AddRouting(options => options.LowercaseUrls = options.LowercaseQueryStrings = true);

                    if (_settings.EnableCompression)
                        services.AddResponseCompression(options =>
                        {
                            options.EnableForHttps = false;
                            options.Providers.Add<GzipCompressionProvider>();
                            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append(MediaTypeNames.Application.Json);
                        });

                    var controllers = services
                        .AddControllers(options => options.EnableEndpointRouting = false)
                        .ConfigureApplicationPartManager(manager =>
                        {
                            var controllerFeatureProvider = manager.FeatureProviders.Single(p => p.GetType() == typeof(ControllerFeatureProvider));
                            var index = manager.FeatureProviders.IndexOf(controllerFeatureProvider);
                            manager.FeatureProviders[index] = new BlackListControllerFeatureProvider(_settings);
                        }).ConfigureApiBehaviorOptions(options =>
                        {
                            options.InvalidModelStateResponseFactory = context =>
                                new BadRequestObjectResult(new ParameterFormatExceptionModel(string.Join(' ', context.ModelState.Keys)))
                                {
                                    ContentTypes =
                                    {
                                        MediaTypeNames.Application.Json,
                                    }
                                };
                        });

                    // Load all plugins Controllers
                    foreach (var plugin in Plugin.Plugins)
                        controllers.AddApplicationPart(Assembly.GetAssembly(plugin.GetType()));

                    // Json Binding for http server
                    controllers.AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        options.SerializerSettings.Formatting = Formatting.None;

                        foreach (var converter in _settings.JsonSerializerSettings.Converters)
                            options.SerializerSettings.Converters.Add(converter);
                    });

                    if (_settings.EnableSwagger)
                        services.AddSwaggerGen();

                    // Service configuration
                    if (_settings.EnableForwardedHeaders)
                        services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.All);

                    if (_settings.EnableCompression)
                        services.Configure<GzipCompressionProviderOptions>(options => options.Level = _settings.CompressionLevel);
                })
                .Configure(app =>
                {
                    if (_settings.EnableForwardedHeaders)
                        app.UseForwardedHeaders();

                    app.UseRouting();

                    if (_settings.EnableCors)
                        app.UseCors();

                    if (_settings.EnableCompression)
                        app.UseResponseCompression();

                    app.UseMiddleware<RestServerMiddleware>(_settings);

                    if (_settings.EnableSwagger)
                    {
                        app.UseSwagger();
                        app.UseSwaggerUI(options => options.DefaultModelsExpandDepth(-1));
                    }

                    app.UseExceptionHandler(c => c.Run(async context =>
                    {
                        var exception = context.Features
                            .Get<IExceptionHandlerPathFeature>()
                            .Error;
                        var response = new ErrorModel()
                        {
                            Code = exception.HResult,
                            Name = exception.GetType().Name,
                            Message = exception.Message,
                        };
                        await context.Response.WriteAsJsonAsync(response);
                    }));
                    app.UseMvc();
                })
                .Build();
            _host.Start();
        }
    }
}