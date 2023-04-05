using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using System.Web;

using IniParser;

namespace IISRequestsFilter
{
    public class RequestsCounterFilter : IHttpModule
    {
        private int CounterInterval { get; set; }
        private int SiteRequestCount { get; set; }
        private int UrlRequestCount { get; set; }
        private int BlockInterval { get; set; }
        private List<IPNetwork> WhiteList { get; set; }
        private bool CrossSiteBlock { get; set; }
        private bool EnableStatusUrl { get; set; }
        private string StatusUrl { get; set; }

        private ConcurrentDictionary<BigInteger, BlockAddress> BlockedIps { get; set; }
        private ConcurrentQueue<RequestQueue> RequestCounterQueue { get; set; }

        private ConcurrentDictionary<RequestQueue, int> SiteRequestCounter { get; set; }
        private ConcurrentDictionary<RequestQueue, int> UrlRequestCounter { get; set; }

        private bool Run { get; set; } = true;

        public void Dispose()
        {
            Run = false;
            GC.SuppressFinalize(this);
        }

        public void Init(HttpApplication context)
        {
            var iniConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RequestsCounterFilter.ini");

            if (File.Exists(iniConfigPath))
            {
                Initialize(iniConfigPath);
                context.BeginRequest += BeginRequest;
            }
        }

        private void Initialize(string configPath)
        {
            var parser = new FileIniDataParser();
            var config = parser.ReadFile(configPath);

            CounterInterval = int.Parse(config["SETTINGS"]["CounterInterval"]);
            UrlRequestCount = int.Parse(config["SETTINGS"]["UrlRequestCount"]);
            SiteRequestCount = int.Parse(config["SETTINGS"]["SiteRequestCount"]);
            BlockInterval = int.Parse(config["SETTINGS"]["BlockInterval"]);
            WhiteList = new List<IPNetwork>(config["SETTINGS"]["WhiteList"].Split(',').Select(a => IPNetwork.Parse(a)));
            CrossSiteBlock = config["SETTINGS"]["BlockInterval"].Equals("true", StringComparison.InvariantCultureIgnoreCase);
            EnableStatusUrl = config["SETTINGS"]["EnableStatusUrl"].Equals("true", StringComparison.InvariantCultureIgnoreCase);
            StatusUrl = config["SETTINGS"]["StatusUrl"];

            BlockedIps = new ConcurrentDictionary<BigInteger, BlockAddress>();
            RequestCounterQueue = new ConcurrentQueue<RequestQueue>();

            SiteRequestCounter = new ConcurrentDictionary<RequestQueue, int>();
            UrlRequestCounter = new ConcurrentDictionary<RequestQueue, int>();

            Task.Run(ManageQueue);
            Task.Run(Block);
            Task.Run(ResetCounter);
        }

        private void BeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Context.Request;

            if (EnableStatusUrl && request.Url.ToString().Contains(StatusUrl))
            {
                DumpStatus(app.Context.Response);
                return;
            }

            var remoteIp = request.UserHostAddress;
            var ipaddress = IPAddress.Parse(remoteIp);

            if (IPNetwork.TryToBigInteger(ipaddress, out var intip) &&
                BlockedIps.TryGetValue(intip.Value, out var _))
            {
                request.Abort();
                return;
            }

            // To minimize any affort to traffic all iteraction with ip going in background
            RequestCounterQueue.Enqueue(new RequestQueue(ipaddress, intip.Value, request.RawUrl));
        }

        private async Task ManageQueue()
        {
            while (Run)
            {
                while (RequestCounterQueue.TryDequeue(out var result))
                {
                    if (WhiteList.Any(a => a.Contains(result.IpAddress)))
                        continue;

                    if (SiteRequestCounter.ContainsKey(result))
                        ++SiteRequestCounter[result];
                    else
                        SiteRequestCounter[result] = 1;

                    if (UrlRequestCounter.ContainsKey(result))
                        ++UrlRequestCounter[result];
                    else
                        UrlRequestCounter[result] = 1;
                }

                await Task.Delay(1000);
            }
        }

        private async Task Block()
        {
            while (Run)
            {
                var now = DateTime.Now;

                foreach (var block in SiteRequestCounter
                    .Where(a => a.Value > UrlRequestCount)
                    .Select(a => a.Key))
                {
                    BlockedIps.TryAdd(block.IntIp, new BlockAddress(block, DateTime.Now.AddSeconds(BlockInterval)));
                }

                foreach (var block in UrlRequestCounter
                    .Where(a => a.Value > SiteRequestCount)
                    .Select(a => a.Key))
                {
                    BlockedIps.TryAdd(block.IntIp, new BlockAddress(block, DateTime.Now.AddSeconds(BlockInterval)));
                }

                // Unblock if unblockdate < date now
                foreach (var block in BlockedIps.Where(a => a.Value.UnblockDate < now))
                    BlockedIps.TryRemove(block.Key, out var _);

                await Task.Delay(1000);
            }
        }

        private async Task ResetCounter()
        {
            while (Run)
            {
                foreach (var key in SiteRequestCounter.Keys)
                    SiteRequestCounter[key] = 0;

                foreach (var key in UrlRequestCounter.Keys)
                    UrlRequestCounter[key] = 0;

                await Task.Delay(TimeSpan.FromSeconds(CounterInterval));
            }
        }

        private void DumpStatus(HttpResponse response)
        {
            response.Write("Blocked ips:<br/>");
            foreach (var block in BlockedIps.Select(a => a.Value))
            {
                var ip = block.IP.IpAddress;
                response.Write($"{ip} until {block.UnblockDate} for {block.IP.Url}<br/>");
            }
            response.Flush();
        }
    }

    public struct RequestQueue
    {
        public IPAddress IpAddress { get; set; }
        public BigInteger IntIp { get; set; }
        public string Url { get; set; }

        public RequestQueue(IPAddress ip, BigInteger intip, string url)
        {
            IpAddress = ip;
            IntIp = intip;
            Url = url;
        }

        public override int GetHashCode()
        {
            return (IpAddress, IntIp, Url).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is RequestQueue queue &&
                   EqualityComparer<IPAddress>.Default.Equals(IpAddress, queue.IpAddress) &&
                   IntIp.Equals(queue.IntIp) &&
                   Url == queue.Url;
        }
    }

    public struct BlockAddress
    {
        public RequestQueue IP { get; set; }
        public DateTime UnblockDate { get; set; }

        public BlockAddress(RequestQueue ip, DateTime unblockDate)
        {
            IP = ip;
            UnblockDate = unblockDate;
        }
    }
}