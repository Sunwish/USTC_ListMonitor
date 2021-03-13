using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sunwish.Notifier;

namespace USTC_ListMonitor
{
    class Program
    {
        static WechatNotifier wechatNotifier;
        readonly static string SCKEYFileName = "SCKEY.ini";
        readonly static string USTCListAddress = @"https://yz.ustc.edu.cn/list_1.htm";
        static List<string> CurrentList = new List<string>();

        static async Task Main(string[] args)
        {
            // Register for GB2312 encoding
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Configurate notifier
            if (!File.Exists(SCKEYFileName) || File.ReadAllText(SCKEYFileName).Trim().Equals(""))
            {
                File.Create(SCKEYFileName);
                Console.WriteLine(@"Please configurate your SCKEY in file """ + SCKEYFileName + @""", you can generate your unique SCKEY from http://sc.ftqq.com/.");
                return;
            }
            string SCKEY = File.ReadAllText(SCKEYFileName).Trim();
            wechatNotifier = new WechatNotifier(SCKEY);
            Console.WriteLine($"{DateTime.Now} | SCKEY = {SCKEY}");

            // Start
            while (true)
            {
                List<string> USTCList = await GetUSTCList();
                if (USTCList.Count == 0) continue;
                UpdateListAndNotify(USTCList);
                System.Threading.Thread.Sleep(15000);
            }
        }

        static async Task<string> GetFullHtml(string url)
        {
            // Local test
            // return await File.ReadAllTextAsync(@"F:\USTCList.txt");
            
            HttpClient client = new HttpClient()
            {
                BaseAddress = new Uri(url)
            };

            HttpResponseMessage httpResponseMessage = await client.GetAsync("");

            // Read stream with GB2312 encoding
            Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync();
            StreamReader streamReader = new StreamReader(stream, Encoding.GetEncoding("GB2312"));
            
            return await streamReader.ReadToEndAsync();
        }

        static async Task<string> GetUSTCListFullHTML()
        {
            return await GetFullHtml(USTCListAddress);
        }

        static async Task<List<string>> GetUSTCList()
        {
            // Get list area html
            string html = await GetUSTCListFullHTML();
            MatchCollection matches = Regex.Matches(html, @"(?<=<td class=""bt02"">)([\w\W]*?)(?=</td>)");
            if (matches.Count < 2) return new List<string>(); // Match failed.
            string listHTML = matches[1].Value; // Match successed.

            // Get list item and build list
            List<string> list = new List<string>();
            foreach (Match match in Regex.Matches(listHTML, @"(?<=class=""bt02"">)(.*?)(?=</a>)"))
            {
                list.Add(match.Value);
            }

            return list;
        }

        static void UpdateListAndNotify(List<string> newestList)
        {
            if (newestList == null || newestList.Count == 0) return;
            if (CurrentList == null || CurrentList.Count == 0) { CurrentList = newestList; return; }

            int newCount = 0;
            foreach(string item in newestList)
            {
                if (!CurrentList.Exists(s => s == item))
                {
                    bool isSendSuccess = false; int retry = 10;
                    for (int i = 0; i < retry && isSendSuccess == false; i++)
                    {
                        // Notify new item
                        isSendSuccess = wechatNotifier.SendNotifier(item, $"详情见官网公告（{USTCListAddress}）");
                        newCount++;
                        Console.WriteLine($"[New] {DateTime.Now} | {item} (Notify Status: {isSendSuccess})");
                        if (!isSendSuccess) System.Threading.Thread.Sleep(500);
                    }
                }
            }

            if (newCount > 0)
                // Update current list
                CurrentList = newestList;
            else
            {
                Console.WriteLine($"{DateTime.Now} | No new list item");
            }
        }
    }
}
