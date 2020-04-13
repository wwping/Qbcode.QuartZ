using Bumblebee;
using Bumblebee.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Qbcode.QuartZ
{
    [RouteBinder(ApiLoader = false)]
    public class Plugin : IPlugin, IPluginStatus, IPluginInfo, IGatewayLoader
    {
        public string Name => "qbcode.quartz";

        public string Description => "quartz定时发送请求";

        public PluginLevel Level => PluginLevel.None;

        private bool _Enabled = false;

        public bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
                ResetJob();
            }
        }

        public string IconUrl => string.Empty;

        public string EditorUrl => string.Empty;

        public string InfoUrl => string.Empty;

        public void Init(Gateway gateway, Assembly assembly)
        {
            this.mGateway = gateway;
            gateway.HttpServer.ResourceCenter.LoadManifestResource(assembly);
            if (scheduler == null)
            {
                scheduler = new StdSchedulerFactory().GetScheduler().Result;
                scheduler.Start();
            }
        }

        public void LoadSetting(JToken setting)
        {
            if (setting != null)
            {
                List<SettingItem> s = setting.ToObject<List<SettingItem>>();
                this.setting = s;
                ResetJob();
            }
        }

        public object SaveSetting()
        {
            return this.setting;
        }

        private void ResetJob()
        {
            if (this.Enabled)
            {
                scheduler.Clear();
                this.setting.ForEach(c =>
                {
                    var trigger = TriggerBuilder.Create()
                           .WithCronSchedule(c.Corn)
                           .UsingJobData("url", $"http://localhost:{mGateway.HttpServer.Options.Port}{c.Url}")
                           .Build();
                    var jobDetail = JobBuilder.Create<MyJob>()
                          .WithIdentity(c.Url, c.Url)
                          .Build();

                    scheduler.ScheduleJob(jobDetail, trigger).Wait();
                });
            }
        }

        private Gateway mGateway;
        private IScheduler scheduler;
        private List<SettingItem> setting { get; set; } = new List<SettingItem>();
    }

    internal class SettingItem
    {
        public string Url { get; set; } = string.Empty;
        public string Corn { get; set; } = string.Empty;
    }

    internal class MyJob : IJob
    {
        private readonly IHttpClientFactory httpClientFactory;

        public MyJob()
        {

            var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        }

        public Task Execute(IJobExecutionContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var triggerData = context.Trigger.JobDataMap;
                    var url = triggerData.GetString("url");

                    HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(new { }));
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                    {
                        CharSet = "utf-8"
                    };
                    httpClientFactory.CreateClient().PostAsync(url, httpContent).Wait();
                }
                catch (Exception)
                {
                }
            });
        }
    }
}
