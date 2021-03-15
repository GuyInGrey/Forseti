using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ForsetiFramework.Constructs
{
    [Serializable]
    public class PartyInfo
    {
        public ulong Host;
        public ulong[] Guests;
        public ulong TextChannel;
        public ulong VoiceChannel;
        public DateTime CreationTime;

        public static string Path => Config.Path + @"Parties\";

        public static List<PartyInfo> GetAll()
        {
            var toReturn = new List<PartyInfo>();

            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
                return toReturn;
            }

            foreach (var f in Directory.GetFiles(Path))
            {
                toReturn.Add(JsonConvert.DeserializeObject<PartyInfo>(File.ReadAllText(f)));
            }

            return toReturn;
        }

        public static void SaveParties(List<PartyInfo> parties)
        {
            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
            }

            foreach (var f in Directory.GetFiles(Path))
            {
                File.Delete(f);
            }

            foreach (var p in parties)
            {
                File.WriteAllText(Path + DateTime.Now.Ticks.ToString() + ".json", JsonConvert.SerializeObject(p));
            }
        }
    }
}
