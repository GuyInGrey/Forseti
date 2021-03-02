using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace ForsetiFramework.Modules
{
    public static class Database
    {
        public static MySqlConnection Connection;

        public static void Initialize() { }
        static Database()
        {
            Connection = new MySqlConnection(BotManager.Instance.Config.DatabaseConnectionString);
            try { Connection.Open(); }
            catch (Exception e) { Console.WriteLine(e); return; }
        }
    }
}
