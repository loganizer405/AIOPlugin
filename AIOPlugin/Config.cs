using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace AIOPlugin
{
    public class Config
    {
        //variables go here
        public bool ChatToolsEnabled = true;
        public bool AFKEnabled = true;
        public bool GriefReporterEnabled = true;
        public bool SugestionsEnabled = true;
        public bool StaffChatEnabled = false;
        public List<Grief> Griefs = new List<Grief>();
        public List<Suggestion> Suggestions = new List<Suggestion>();

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public static Config Read(string path)
        {
            if (!File.Exists(path))
            {
                return new Config();
            }
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }
    }
}
