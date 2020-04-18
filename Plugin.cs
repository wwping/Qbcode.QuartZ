using Bumblebee;
using Bumblebee.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
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

            store = new Store(gateway);

            store.Load();
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


        public PluginRunInfo RunInfo(int p = 1, int ps = 10)
        {
            List<PluginRunInfoColumn> columns = new List<PluginRunInfoColumn> {
                new PluginRunInfoColumn{ Key = "Url", Name = "Url" },
                new PluginRunInfoColumn{ Key = "Corn", Name = "Corn" },
                new PluginRunInfoColumn{ Key = "Code", Name = "Code" },
                new PluginRunInfoColumn{ Key = "Time", Name="Time" },
                new PluginRunInfoColumn{ Key = "Res", Name=  "Res" }
            };

            return new PluginRunInfo
            {
                Columns = columns,
                Data = new PluginRunInfoPage
                {
                    Count = store.Data.Count(),
                    Data = store.Data.Skip((p - 1) * ps).Take(ps),
                    PageIndex = p,
                    PageSize = ps
                }
            };
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
                           .UsingJobData("corn", c.Corn)
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
        internal static Store store;
        internal static bool isChanged = false;
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
                    var corn = triggerData.GetString("corn");

                    HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(new { }));
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                    {
                        CharSet = "utf-8"
                    };
                    HttpResponseMessage res = httpClientFactory.CreateClient().PostAsync(url, httpContent).Result;

                    Plugin.store.Add(new RunInfo
                    {
                        Code = res.StatusCode.ToString(),
                        Corn = corn,
                        Url = url,
                        Res = res.Content.ReadAsStringAsync().Result
                    });

                }
                catch (Exception)
                {
                }
            });
        }
    }

    internal class RunInfo
    {
        public string Url { get; set; } = string.Empty;
        public string Corn { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public string Time { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string Res { get; set; } = string.Empty;
    }

    internal class Store
    {
        public List<RunInfo> Data { get; private set; } = new List<RunInfo>();

        private Timer mTimer;
        private Gateway mGateway;

        private readonly int maxLength = 100;
        private readonly string DirName = "plugin_runinfo";

        public Store(Gateway gateway)
        {
            mGateway = gateway;
            this.mTimer = new Timer(new TimerCallback(this.OnTrack), null, 1000, 1000);
        }

        public string Path
        {
            get
            {
                if (!Directory.Exists(DirName))
                {
                    Directory.CreateDirectory(DirName);
                }

                return string.Format("{0}{1}{2}", new object[]
                {
                    DirName,
                    System.IO.Path.DirectorySeparatorChar,
                    "qbcode.quartz.json"
                });
            }
        }

        public void Load()
        {
            if (File.Exists(Path))
            {
                string str = File.ReadAllText(Path);
                if (!string.IsNullOrWhiteSpace(str))
                {
                    try
                    {
                        Data = JsonConvert.DeserializeObject<List<RunInfo>>(str);
                    }
                    catch (Exception ex)
                    {
                        if (this.mGateway.HttpServer.EnableLog(BeetleX.EventArgs.LogType.Error))
                        {
                            this.mGateway.HttpServer.Log(BeetleX.EventArgs.LogType.Error, string.Concat(new string[]
                            {
                        "qbcode.quartz load data error",
                        ex.Message,
                        "@",
                        ex.StackTrace
                            }));
                        }
                    }
                }
            }
        }

        public void Add(RunInfo model)
        {
            Plugin.isChanged = true;
            Data.Insert(0, model);
            if (Data.Count() > maxLength)
            {
                Data.RemoveRange(maxLength - 1, Data.Count() - maxLength);
            }
        }

        private void OnTrack(object state)
        {
            if (!Plugin.isChanged)
            {
                return;
            }
            Plugin.isChanged = false;

            this.mTimer.Change(-1, -1);
            try
            {
                File.WriteAllText(Path, JsonConvert.SerializeObject(Data));
            }
            catch (Exception ex)
            {
                if (this.mGateway.HttpServer.EnableLog(BeetleX.EventArgs.LogType.Error))
                {
                    this.mGateway.HttpServer.Log(BeetleX.EventArgs.LogType.Error, string.Concat(new string[]
                    {
                        "qbcode.quartz save data error",
                        ex.Message,
                        "@",
                        ex.StackTrace
                    }));
                }
            }
            finally
            {
                this.mTimer.Change(999, 999);
            }
        }
    }

}
