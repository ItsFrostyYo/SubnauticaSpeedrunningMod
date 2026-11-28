using System;
using System.Collections.Generic;
using System.IO;
using SNSRModLoader;

namespace ItsFrosti.SpeedrunningMod
{
    internal static class RaceService
    {
        internal const string ApiUrl = "https://subnautica-race-api.geonautica.workers.dev";

        internal static void Initialize(PluginContext context)
        {
            // Retire configuration from the username/direct-server prototype.
            // Account sessions remain encrypted in their separate session file.
            string path = context.GetSettingsPath("settings.ini");
            if (File.Exists(path))
            {
                var retained = new List<string>();
                bool changed = false;
                foreach (string line in File.ReadAllLines(path))
                {
                    int separator = line.IndexOf('=');
                    string key = separator < 0 ? "" : line.Substring(0, separator).Trim();
                    if (key == "AccountApiUrl" || key == "ServerUrl" || key == "EnableNetworking" || key == "DisplayName" || key == "PlayerId")
                        changed = true;
                    else retained.Add(line);
                }
                if (changed) File.WriteAllLines(path, retained.ToArray());
            }
            context.Logger.Info("Automatic race service: " + new Uri(ApiUrl).Host);
        }
    }
}
