using System;
using System.Collections.Generic;
using System.Data;
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
            Connection.StateChange += Connection_StateChange;
            try { Connection.Open(); }
            catch (Exception e) { Console.WriteLine(e); return; }
        }

        private static void Connection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            if (e.CurrentState == ConnectionState.Closed || e.CurrentState == ConnectionState.Broken)
            {
                Connection.Dispose();
                Connection = new MySqlConnection(BotManager.Instance.Config.DatabaseConnectionString);
                try { Connection.Open(); }
                catch (Exception ex) { Console.WriteLine(ex); return; }
            }
        }
    }
}
