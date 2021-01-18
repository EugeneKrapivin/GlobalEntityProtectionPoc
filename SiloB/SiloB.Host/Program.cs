using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Streams;
using Orleans.Streams.Kafka.Config;

namespace SiloB.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private const int PortBase = 50020;

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
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
                            opts.ServiceId = "SystemB";
                        })
                        .Configure<EndpointOptions>(opts =>
                        {
                            opts.AdvertisedIPAddress = IPAddress.Loopback;
                            opts.GatewayPort = PortBase + 2;
                            opts.SiloPort = PortBase + 3;
                        }).UseAzureStorageClustering(opt =>
                        {
                            opt.TableName = "OrleansMembershipSysB";
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                        })
                        .AddAzureTableGrainStorage("business-units", opt =>
                        {
                            opt.ConnectionString = ctx.Configuration["Storage:ConnectionString"];
                            opt.TableName = "BusinessUnits";
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
                                options.ConsumerGroupId = "system-b";

                                options
                                    .AddTopic("unprotection-requests", new TopicCreationConfig { AutoCreate = false })
                                    .AddTopic("protection-requests", new TopicCreationConfig { AutoCreate = false });
                            })
                            .AddJson()
                            .AddLoggingTracker()
                            .Build();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls($"http://localhost:{PortBase}", $"https://localhost:{PortBase+1}");
                });
    }
}
