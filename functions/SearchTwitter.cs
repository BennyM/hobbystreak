using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Microsoft.Extensions.Primitives;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Collections;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace HobbyStreak.Functions
{


    public class SearchTwitter
    {


        public async static Task Run(TimerInfo myTimer, Binder binder, TraceWriter log)
        {

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("hobbystreakblob", EnvironmentVariableTarget.Process));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable searchMetaDataTable = tableClient.GetTableReference("SearchMetaData");
            await searchMetaDataTable.CreateIfNotExistsAsync();
            CloudTable settingsTable = tableClient.GetTableReference("Settings");
            await settingsTable.CreateIfNotExistsAsync();
            TableOperation retrieveOperation = TableOperation.Retrieve<Settings>("Settings", "RefreshUrl");
            TableResult result = await settingsTable.ExecuteAsync(retrieveOperation);
            Settings refreshUrl = result.Result as Settings;
            if (refreshUrl == null)
            {
                refreshUrl = new Settings();
                refreshUrl.PartitionKey = "Settings";
                refreshUrl.RowKey = "RefreshUrl";
                refreshUrl.Value = "?f=tweets&vertical=default&q=%23hobbystreak&src=typd";
            }



            string consumerkey = System.Net.WebUtility.UrlEncode(System.Environment.GetEnvironmentVariable("TwitterKey", EnvironmentVariableTarget.Process));
            string consumersecret = System.Net.WebUtility.UrlEncode(System.Environment.GetEnvironmentVariable("TwitterSecret", EnvironmentVariableTarget.Process));
            string combined = $"{consumerkey}:{consumersecret}";
            byte[] encodedBytes = System.Text.Encoding.UTF8.GetBytes(combined);
            string encodedTxt = Convert.ToBase64String(encodedBytes);
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", $"{encodedTxt}");

                var authResponse = await client.PostAsync("https://api.twitter.com/oauth2/token", new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"));
                authResponse.EnsureSuccessStatusCode();
                var responseToken = await authResponse.Content.ReadAsStringAsync();
                var access_token = Newtonsoft.Json.Linq.JObject.Parse(responseToken)["access_token"];
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{access_token}");
                int counter = 0;
                var currentRunTime = DateTime.UtcNow.ToString("yyyy-MM-dd-hh:mm:ss");
                IEnumerable<dynamic> statusses = null;
                string nextResultsUrl = refreshUrl.Value;
                do
                {
                    var searchedTweetsResponse = await client.GetAsync("https://api.twitter.com/1.1/search/tweets.json" + nextResultsUrl);
                    var tweets = JObject.Parse(await searchedTweetsResponse.Content.ReadAsStringAsync());
                    statusses = tweets["statuses"];
                    string fileName = $"tweets/{currentRunTime}-{counter}.json";
                    if (statusses != null && statusses.Any())
                    {
                        var attributes = new Attribute[]
                        {
                        new BlobAttribute(fileName),
                        new StorageAccountAttribute("hobbystreakblob")
                        };
                        using (var writer = await binder.BindAsync<TextWriter>(attributes))
                        {
                            writer.Write(statusses);
                            writer.Flush();
                        }
                    }
                    else
                    {
                        fileName = string.Empty;
                    }
                    dynamic metadata = tweets["search_metadata"];

                    if (counter == 0)
                    {
                        refreshUrl.Value = metadata["refresh_url"];
                        await Ops.InsertOrMerge(settingsTable, refreshUrl);
                    }
                    SearchMetaData meta = new SearchMetaData();
                    meta.PartitionKey = currentRunTime;
                    meta.RowKey = counter.ToString();
                    meta.CompletedIn = metadata["completed_in"];
                    meta.Count = metadata["count"];
                    meta.FileName = fileName;
                    meta.MaxId = metadata["max_id"];
                    meta.MaxIdStr = metadata["max_id_str"];
                    meta.NextResults = metadata["next_results"];
                    meta.Query = metadata["query"];
                    meta.RefreshUrl = metadata["refresh_url"];
                    meta.SinceId = metadata["since_id"];
                    meta.SinceIdStr = metadata["since_id_str"];
                    nextResultsUrl = metadata["next_results"];
                    await Ops.InsertOrMerge(searchMetaDataTable, meta);
                    if (!string.IsNullOrWhiteSpace(nextResultsUrl))
                    {
                        counter++;
                    }
                    else
                    {
                        statusses = null;
                    }
                }
                while (statusses != null && statusses.Any());
            }
        }
    }
}