using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Streams;
using Orleans.Streams.Kafka.Config;

namespace Protectorate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private const int PortBase = 50000;

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseOrleans((ctx, siloBuilder) =>
                {
                    siloBuilder
                        .UseLocalhostClustering()
                        .ConfigureApplicationParts(parts => parts.AddFromApplicationBaseDirectory())
                        .UseDashboard(options =>
                        {
                            options.Port = PortBase + 4;
                        })
                        .Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1))
                        .Configure<ClusterOptions>(opts =>
                        {
                            opts.ClusterId = "dev";
                            opts.ServiceId = "ProtectorService";
                        })
                        .Configure<EndpointOptions>(opts =>
                        {
                            opts.AdvertisedIPAddress = IPAddress.Loopback;
                            opts.GatewayPort = PortBase + 2;
                            opts.SiloPort = PortBase + 3;
                        })
                        .UseAzureStorageClustering(opt =>
                        {
                            opt.TableName = "OrleansMembershipProtector";
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];

                        })
                        .AddAzureTableGrainStorage("protected-resources", opt =>
                        {
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                            opt.TableName = "ProtectedResources";
                            opt.DeleteStateOnClear = true;
                            opt.UseJson = true;
                        })
                        .AddAzureTableGrainStorage("PubSubStore", opt =>
                        {
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                            opt.TableName = "PubSubStore";
                            opt.DeleteStateOnClear = true;
                            opt.UseJson = true;
                        })
                        .AddAzureTableGrainStorageAsDefault(opt =>
                        {
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                            opt.DeleteStateOnClear = true;
                            opt.UseJson = true;
                            opt.TableName = "defualt";
                        })
                        .UseAzureTableReminderService(opt =>
                        {
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                            opt.TableName = "OrleansReminders";
                        })
                        .AddKafka("stream-provider")
                            .WithOptions(options =>
                            {
                                var kfk = new KafkaBrokersConfig();
                                ctx.Configuration.GetSection("Kafka").Bind(kfk);
                                
                                options.BrokerList = kfk.Brokers;
                                options.ConsumerGroupId = "Protector";
                                options.ConsumeMode = ConsumeMode.StreamEnd;

                                options
                                    .AddTopic("unprotection-requests", new TopicCreationConfig { AutoCreate = true, Partitions = 2, ReplicationFactor = 1 })
                                    .AddTopic("protection-requests", new TopicCreationConfig { AutoCreate = true, Partitions = 2, ReplicationFactor = 1 });
                            })
                            .AddJson()
                            .AddLoggingTracker()
                            .Build();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    //webBuilder.ConfigureKestrel(options =>
                    //{
                    //    options.Listen(IPAddress.Loopback, PortBase + 5, listenOptions =>
                    //    {
                    //        listenOptions.Protocols = HttpProtocols.Http2;
                    //        listenOptions.UseHttps(@"C:\Users\eugene.krapivin@sap.com\Documents\localhost-cert\cert.pfx",
                    //            "12345");
                    //    });
                    //    options.ListenAnyIP(PortBase);
                    //    options.ListenAnyIP(PortBase+1);
                    //});
                    webBuilder.UseUrls($"http://localhost:{PortBase}", $"https://localhost:{PortBase + 1}");
                });
    }
}
