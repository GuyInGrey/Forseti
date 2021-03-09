using System;
using System.Data;

using MySql.Data.MySqlClient;

namespace ForsetiFramework.Modules
{
    public static class Database
    {
        public static MySqlConnection Connection;

        public static void Init() { }
        static Database()
        {
            Connection = new MySqlConnection(BotManager.Config.DatabaseConnectionString);
            Connection.StateChange += Connection_StateChange;
            try { Connection.Open(); }
            catch (Exception e) { Console.WriteLine(e); return; }
        }

        private static void Connection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            if (e.CurrentState == ConnectionState.Closed || e.CurrentState == ConnectionState.Broken)
            {
                ForceReconnect();
            }
        }

        public static void ForceReconnect()
        {
            if (Connection.State == ConnectionState.Open || Connection.State == ConnectionState.Executing || Connection.State == ConnectionState.Fetching)
            {
                try
                {
                    Connection.Close();
                } catch { }
            }
            Connection.Dispose();
            Connection = new MySqlConnection(BotManager.Config.DatabaseConnectionString);
            try { Connection.Open(); }
            catch (Exception ex) { Console.WriteLine(ex); return; }
        }
    }
}
