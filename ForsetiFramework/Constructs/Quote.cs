﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ForsetiFramework.Constructs
{
    [Serializable]
    public class Quote
    {
        [JsonProperty("text")]
        public string Text;
        [JsonProperty("author")]
        public string Author;

        public override string ToString() => $"\"{Text}\" - {Author}".Trim();

        public static List<Quote> GetQuotes()
        {
            var json = File.ReadAllText(Config.Path + "quotes.json");
            return JsonConvert.DeserializeObject<Quote[]>(json).ToList();
        }

        public static Quote Random(int maxLength = int.MaxValue)
        {
            var q = GetQuotes().Where(s => s.ToString().Length <= maxLength).ToArray();
            return q[Extensions.Random.Next(q.Length)];
        }
    }
}
