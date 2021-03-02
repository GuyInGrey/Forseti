using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ForsetiFramework.Modules
{
    public static class Tags
    {
        public static async Task<Tag[]> GetTags()
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(Config.Path + "tags.json")) { return null; }
                return JsonConvert.DeserializeObject<Tag[]>(File.ReadAllText(Config.Path + "tags.json"));
            });
        }

        public static async Task<Tag> GetTag(string name)
        {
            var tags = await GetTags();
            return tags?.FirstOrDefault(t => t.Name == name.ToLower());
        }

        public static async Task SetTag(params Tag[] t)
        {
            var tagsArr = await GetTags();
            var tags = tagsArr is null ? new List<Tag>() : tagsArr.ToList();
            // Remove old
            t.ToList().ForEach(t2 => tags.RemoveAll(t3 => t3.Name == t2.Name));
            t.ToList().ForEach(t2 => tags.Add(t2));
            File.WriteAllText(Config.Path + "tags.json", JsonConvert.SerializeObject(tags.ToArray(), Formatting.Indented));
        }

        public static async Task<bool> RemoveTag(string name)
        {
            var tagsArr = await GetTags();
            if (tagsArr is null) { return false; }
            var tags = tagsArr.ToList();
            var removeCount = tags.RemoveAll(t => t.Name == name);
            File.WriteAllText(Config.Path + "tags.json", JsonConvert.SerializeObject(tags.ToArray(), Formatting.Indented));
            return removeCount > 0;
        }
    }

    [Serializable]
    public class Tag
    {
        public string Name;
        public string Content;
        public string[] AttachmentURLs;
    }
}
