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
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Neo.Cryptography.ECC;
using Neo.Json;
using Neo.Network.P2P.Payloads.Conditions;
using Neo.Plugins.RestServer.Middleware;
using Neo.Plugins.RestServer.Models.Error;
using Neo.Plugins.RestServer.Providers;
using Neo.Plugins.RestServer.Swagger.Filters;
using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Mime;
using System.Net.Security;
using System.Numerics;

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
                    options.Listen(_settings.BindAddress, unchecked((int)_settings.Port),
                        listenOptions =>
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
                                options.AddPolicy("All", policy =>
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
                                options.AddPolicy("All", policy =>
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
                        .AddControllers(options =>
                        {
                            options.EnableEndpointRouting = false;
                        })
                        .ConfigureApiBehaviorOptions(options =>
                        {
                            options.InvalidModelStateResponseFactory = context =>
                                new BadRequestObjectResult(
                                    new ParameterFormatExceptionModel(string.Join(' ', context.ModelState.Values.SelectMany(s => s.Errors).Select(s => s.ErrorMessage))))
                                {
                                    ContentTypes =
                                        {
                                            MediaTypeNames.Application.Json,
                                        }
                                };
                        })
                        .ConfigureApplicationPartManager(manager =>
                        {
                            var controllerFeatureProvider = manager.FeatureProviders.Single(p => p.GetType() == typeof(ControllerFeatureProvider));
                            var index = manager.FeatureProviders.IndexOf(controllerFeatureProvider);
                            manager.FeatureProviders[index] = new BlackListControllerFeatureProvider(_settings);

                            foreach (var plugin in Plugin.Plugins)
                                manager.ApplicationParts.Add(new AssemblyPart(plugin.GetType().Assembly));
                        })
                        .AddNewtonsoftJson(options =>
                        {
                            options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                            options.SerializerSettings.Formatting = Formatting.None;

                            foreach (var converter in _settings.JsonSerializerSettings.Converters)
                                options.SerializerSettings.Converters.Add(converter);
                        });

                    if (_settings.EnableSwagger)
                    {
                        services.AddSwaggerGen(options =>
                        {
                            options.SwaggerDoc("v1", new OpenApiInfo()
                            {
                                Title = "RestServer Plugin API - V1",
                                Description = "REST Web Sevices for the node.",
                                Version = "v1",
                                License = new OpenApiLicense()
                                {
                                    Name = "MIT",
                                    Url = new Uri("http://www.opensource.org/licenses/mit-license.php"),
                                },
                            });
                            options.MapType<UInt256>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Format = "hash256",
                            });
                            options.MapType<UInt160>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Format = "hash160",
                            });
                            options.MapType<ECPoint>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Format = "hexstring",
                            });
                            options.MapType<BigInteger>(() => new OpenApiSchema()
                            {
                                Type = "integer",
                                Format = "bigint",
                            });
                            options.MapType<byte[]>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Format = "base64",
                            });
                            options.MapType<ReadOnlyMemory<byte>>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Format = "base64",
                            });
                            options.MapType<JObject>(() => new OpenApiSchema()
                            {
                                Type = "object",
                            });
                            options.MapType<StackItem>(() => new OpenApiSchema()
                            {
                                Type = "object",
                            });
                            options.MapType<WitnessCondition>(() => new OpenApiSchema()
                            {
                                Type = "string",
                                Enum =
                                {
                                    new OpenApiString("CalledByEntry"),
                                    new OpenApiString("CalledByContract"),
                                    new OpenApiString("CalledByGroup"),
                                    new OpenApiString("Boolean"),
                                    new OpenApiString("Not"),
                                    new OpenApiString("And"),
                                    new OpenApiString("Or"),
                                    new OpenApiString("ScriptHash"),
                                    new OpenApiString("Group"),
                                }
                            });
                            options.SchemaFilter<SwaggerExcludeFilter>();
                            foreach (var plugin in Plugin.Plugins)
                            {
                                var assemblyName = plugin.GetType().Assembly.GetName().Name;
                                var xmlPathAndFilename = Path.Combine(AppContext.BaseDirectory, "Plugins", assemblyName, $"{assemblyName}.xml");
                                if (File.Exists(xmlPathAndFilename))
                                    options.IncludeXmlComments(xmlPathAndFilename);
                            }
                        });
                        services.AddSwaggerGenNewtonsoftSupport();
                    }

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
                        app.UseCors("All");

                    if (_settings.EnableCompression)
                        app.UseResponseCompression();

                    app.UseMiddleware<RestServerMiddleware>(_settings);

                    app.UseExceptionHandler(config =>
                        config.Run(async context =>
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
                            RestServerMiddleware.SetServerInfomationHeader(context.Response);
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(response);
                        }));

                    if (_settings.EnableSwagger)
                    {
                        app.UseSwagger(options => options.SerializeAsV2 = true);
                        app.UseSwaggerUI(options => options.DefaultModelsExpandDepth(-1));
                    }

                    app.UseMvc();
                })
                .Build();
            _host.Start();
        }
    }
}