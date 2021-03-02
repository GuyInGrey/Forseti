using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace ForsetiFramework.Modules
{
    public static class Tags
    {
        static Tags()
        {
            @"CREATE TABLE IF NOT EXISTS `forseti`.`tags` (
  `name` VARCHAR(255) NOT NULL,
  `content` TEXT NULL,
  `attachmentURLs` TEXT NULL,
  PRIMARY KEY (`name`),
  UNIQUE INDEX `name_UNIQUE` (`name` ASC) VISIBLE);".NonQuery();
        }

        public static async Task<Tag[]> GetTags()
        {
            var q = "SELECT * FROM `forseti`.`tags`;".Query();
            try
            {
                var toReturn = new List<Tag>();
                while (q.Read())
                {
                    toReturn.Add(Tag.FromQuery(q));
                }
                return toReturn.ToArray();
            }
            finally
            {
                q.Close();
            }
        }

        public static async Task<Tag> GetTag(string name)
        {
            var q = "SELECT * FROM `forseti`.`tags` WHERE name=@p0;".Query(name);
            try
            {
                if (q.HasRows)
                {
                    q.Read();
                    return Tag.FromQuery(q);
                }
                return null;
            }
            finally
            {
                q.Close();
            }
        }

        public static async Task SetTag(Tag t)
        {
            "DELETE FROM `forseti`.`tags` WHERE name=@p0".NonQuery(t.Name);
            "INSERT INTO `forseti`.`tags` (name, content, attachmentURLs) VALUES (@p0, @p1, @p2);"
                .NonQuery(t.Name, t.Content, string.Join("\n", t.AttachmentURLs));
        }

        public static async Task<bool> RemoveTag(string name)
        {
            return "DELETE FROM `forseti`.`tags` WHERE name=@p0;".NonQuery(name) > 0;
        }
    }

    [Serializable]
    public class Tag
    {
        public string Name;
        public string Content;
        public string[] AttachmentURLs;

        public static Tag FromQuery(MySqlDataReader q)
        {
            var aS = q["attachmentURLs"].ToString();

            return new Tag()
            {
                Name = q["name"].ToString(),
                Content = q["content"].ToString(),
                AttachmentURLs = aS == "" ? new string[0] : q["attachmentURLs"].ToString().Split('\n')
            };
        }
    }
}
