using System;
using System.Diagnostics;
using System.IO;

using Newtonsoft.Json;

namespace ForsetiFramework
{
    [Serializable]
    public class Config
    {
        public static bool Debug => Debugger.IsAttached;

        public static string Path => Debug ? @"C:\ForsetiDebug\" : @"C:\Forseti\";
        public static string Prefix => Debug ? "$" : "!";

        [JsonProperty("token")]
        public string Token;
        [JsonProperty("errorsWebhook")]
        public string ErrorWebhookUrl;
        [JsonProperty("databaseConnString")]
        public string DatabaseConnectionString;

        public static Config Load(string filePath) => 
            JsonConvert.DeserializeObject<Config>(File.ReadAllText(filePath));
    }
}
