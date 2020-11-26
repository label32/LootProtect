﻿#define DEBUG
#region License (GPL v3)
/*
    Loot Protection - Prevent access to player containers
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License (GPL v3)

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Loot Protection", "RFC1920", "1.0.0")]
    [Description("Prevent access to player containers")]
    internal class LootProtect : RustPlugin
    {
       #region vars
        private Dictionary<string,bool> rules = new Dictionary<string, bool>();
        private const string permLootProtAdmin = "lootprotect.admin";
        private const string permLootProtAll = "lootprotect.all";
        private const string permLootProtected = "lootprotect.player";

        private ConfigData configData;
        private string connStr;

        [PluginReference]
        private readonly Plugin ZoneManager, Friends, Clans, RustIO;

        private readonly string logfilename = "log";
        private bool dolog = false;
        private bool logtofile = false;
        private bool enabled = true;
        private Timer scheduleTimer;
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region init
        private void Init()
        {
            LoadConfigVariables();
            AddCovalenceCommand("lp", "CmdLootProt");
            permission.RegisterPermission(permLootProtected, this);
            permission.RegisterPermission(permLootProtAdmin, this);
            permission.RegisterPermission(permLootProtAll, this);
            if(configData.Options.useSchedule) RunSchedule(true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["enabled"] = "LootProtect enabled.",
                ["disabled"] = "LootProtect disabled.",
                ["status"] = "LootProtect enable is set to {0}.",
                ["logging"] = "Logging set to {0}",
                ["settings"] = "{0} Settings:\n{1}",
                ["notauthorized"] = "You don't have permission to use this command.",
            }, this);
        }

        private void RunSchedule(bool refresh = false)
        {
            TimeSpan ts = new TimeSpan();

            if (configData.Options.useRealTime)
            {
                ts = new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0).Add(DateTime.Now.TimeOfDay);
            }
            else
            {
                try
                {
                    ts = TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;
                }
                catch
                {
                    DoLog("TOD_Sky failure...");
                    refresh = true;
                }
            }

            Schedule parsed;
            bool doenable = false;
            if (ParseSchedule(configData.Schedule, out parsed))
            {
                foreach (var x in parsed.day)
                {
                    DoLog($"Schedule day == {x.ToString()} {parsed.dayName[0]} {parsed.starthour} to {parsed.endhour}");
                    if (ts.Days == x)
                    {
                        DoLog($"Day matched.  Comparing {ts.Hours.ToString()}:{ts.Minutes.ToString().PadLeft(2,'0')} to start time {parsed.starthour}:{parsed.startminute} and end time {parsed.endhour}:{parsed.endminute}");
                        if (ts.Hours >= Convert.ToInt32(parsed.starthour) && ts.Hours <= Convert.ToInt32(parsed.endhour))
                        {
                            // Hours matched for activating ruleset, check minutes
                            DoLog($"Hours matched", 1);
                            if (ts.Hours == Convert.ToInt32(parsed.starthour) && ts.Minutes >= Convert.ToInt32(parsed.startminute))
                            {
                                DoLog("Matched START hour and minute.", 2);
                                doenable = true;
                            }
                            else if (ts.Hours == Convert.ToInt32(parsed.endhour) && ts.Minutes <= Convert.ToInt32(parsed.endminute))
                            {
                                DoLog("Matched END hour and minute.", 2);
                                doenable = true;
                            }
                            else if (ts.Hours > Convert.ToInt32(parsed.starthour) && ts.Hours < Convert.ToInt32(parsed.endhour))
                            {
                                DoLog("Between start and end hours.", 2);
                                doenable = true;
                            }
                            else
                            {
                                DoLog("Minute mismatch for START OR END.", 2);
                                doenable = false;
                            }
                        }
                        else
                        {
                            DoLog($"Hours NOT matched", 1);
                            doenable = false;
                        }
                    }
                }
            }
            enabled = doenable;

            scheduleTimer = timer.Once(configData.Options.useRealTime ? 30f : 5f, () => RunSchedule(refresh));
        }
        #endregion

        #region commands
        [Command("lp")]
        private void CmdLootProt(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permLootProtAdmin)) { Message(player, "notauthorized"); return; }
#if DEBUG
            string debug = string.Join(",", args); Puts($"{debug}");
#endif
            if(args.Length == 0)
            {
                ShowStatus(player);
                return;
            }
            switch (args[0])
            {
                case "1":
                case "e":
                case "true":
                case "enable":
                    enabled = true;
                    Message(player, "enabled");
                    break;
                case "0":
                case "d":
                case "false":
                case "disable":
                    enabled = false;
                    Message(player, "disabled");
                    break;
                case "status":
                    ShowStatus(player);
                    break;
                case "l":
                case "log":
                case "logging":
                    dolog = !dolog;
                    Message(player, "logging", dolog.ToString());
                    break;
            }
        }
        #endregion

        #region hooks
        private object CanPickupEntity(BasePlayer player, BaseCombatEntity ent)
        {
            if (player == null || ent == null) return null;
            DoLog($"Player {player.displayName} looting {ent.ShortPrefabName}");
            if (CanAccess(ent.ShortPrefabName, player.userID, ent.OwnerID)) return null;

            return true;
        }
        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;
            var ent = container.GetComponentInParent<BaseEntity>();
            DoLog($"Player {player.displayName} looting {ent.ShortPrefabName}");
            if (CanAccess(ent.ShortPrefabName, player.userID, ent.OwnerID)) return null;

            return true;
        }
        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            if (player == null || container == null) return null;
            var ent = container.GetComponentInParent<BaseEntity>();
            DoLog($"Player {player.displayName} looting {ent.ShortPrefabName}");
            if (CanAccess(ent.ShortPrefabName, player.userID, ent.OwnerID)) return null;

            return true;
        }
        private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            if (player == null || corpse == null) return null;
            DoLog($"Player {player.displayName} looting {corpse.name}");
            if (CanAccess(corpse.ShortPrefabName, player.userID, corpse.OwnerID)) return null;

            return true;
        }
        private object CanLootPlayer(BasePlayer target, BasePlayer player)
        {
            if (player == null || target == null) return null;
            DoLog($"Player {player.displayName} looting {target.displayName}");
            if (CanAccess(target.ShortPrefabName, player.userID, target.userID)) return null;

            return true;
        }
		private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (player == null || oven == null) return null;
            DoLog($"Player {player.displayName} toggling {oven.ShortPrefabName}");
            if (configData.Options.OverrideOven) return null;
            if (CanAccess(oven.ShortPrefabName, player.userID, oven.OwnerID)) return null;

            return true;
        }
		private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (player == null || privilege == null) return null;
            DoLog($"Player {player.displayName} attempting to authenticate to a TC.");
            if (configData.Options.OverrideTC) return null;
            if (CanAccess(privilege.ShortPrefabName, player.userID, privilege.OwnerID)) return null;

            return true;
        }
        #endregion

        #region helpers
        // Main access check function
        private bool CanAccess(string prefab, ulong source, ulong target)
        {
            if (!enabled) return true;
            bool inzone = false;

            if(configData.Options.RequirePermission)
            {
                var tgt = BasePlayer.FindByID(target);
                try
                {
                    if (!tgt.IPlayer.HasPermission(permLootProtected)) return true;
                }
                catch
                {
                    DoLog("Failed to check target owner permissions!");
                }
            }

            BasePlayer player = BasePlayer.FindByID(source);
            if (configData.Options.useZoneManager)
            {
                if (configData.Zones.Length == 0)
                {
                    DoLog("Admin set useZoneManager but didn't list any zones...");
                    inzone = true;
                }
                else
                {
                    // Check that the player (source) is in a zone where we are configured to take action.
                    // If no zones are set, this will be skipped altogether.
                    if (player != null)
                    {
                        var pzones = GetPlayerZones(player);

                        if (configData.Zones.Length > 0 && pzones.Length > 0)
                        {
                            // Compare player's zones to our zone list
                            foreach (string z in pzones)
                            {
                                if (configData.Zones.Contains(z))
                                {
                                    DoLog($"Player {player.displayName} is in zone {z}, which we control.");
                                    inzone = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                DoLog($"Player {player.displayName} not in a zone we control.");
                if (!inzone) return true;
            }

            DoLog($"Checking access to {prefab}");
            if (target == 0)
            {
                DoLog($"Server-owned.  Access allowed.");
                return true;
            }
            if (source == target)
            {
                DoLog($"Player-owned.  Access allowed.");
                return true;
            }
            if (IsFriend(source, target))
            {
                DoLog($"Friend-owned.  Access allowed.");
                return true;
            }

            try
            {
                // Wrapped in case an NPC is looting and otherwise appears as a player object with no IPlayer.
                if (player.IPlayer.HasPermission(permLootProtAll))
                {
                    DoLog($"User has ALL permission.  Access allowed.");
                    return true;
                }
            }
            catch { }

            // Check protection rules since there is no relationship to the target owner.
            if(configData.Rules.ContainsKey(prefab))
            {
                if (configData.Rules[prefab])
                {
                    DoLog($"Rule found for type {prefab}.  Access BLOCKED!");
                    return false;
                }
                DoLog($"Rule found for type {prefab}.  Access allowed.");
                return true;
            }

            return false;
        }

        private bool ParseSchedule(string dbschedule, out Schedule parsed)
        {
            int day = 0;
            parsed = new Schedule();

            string[] lootprotschedule = Regex.Split(dbschedule, @"(.*)\;(.*)\:(.*)\;(.*)\:(.*)");
            if (lootprotschedule.Length < 6) return false;

            parsed.starthour = lootprotschedule[2];
            parsed.startminute = lootprotschedule[3];
            parsed.endhour = lootprotschedule[4];
            parsed.endminute = lootprotschedule[5];

            parsed.day = new List<int>();
            parsed.dayName = new List<string>();

            string tmp = lootprotschedule[1];
            string[] days = tmp.Split(',');

            if (tmp == "*")
            {
                for (int i = 0; i < 7; i++)
                {
                    parsed.day.Add(i);
                    parsed.dayName.Add(Enum.GetName(typeof(DayOfWeek), i));
                }
            }
            else if (days.Length > 0)
            {
                foreach (var d in days)
                {
                    int.TryParse(d, out day);
                    parsed.day.Add(day);
                    parsed.dayName.Add(Enum.GetName(typeof(DayOfWeek), day));
                }
            }
            else
            {
                int.TryParse(tmp, out day);
                parsed.day.Add(day);
                parsed.dayName.Add(Enum.GetName(typeof(DayOfWeek), day));
            }
            return true;
        }

        private void ShowStatus(IPlayer player)
        {
            string settings = "";
            foreach(FieldInfo field in typeof(Options).GetFields())
            {
                settings += "\t" + field.Name + ": " + field.GetValue(configData.Options) + "\n";
            }
            settings += Lang("status", null, enabled.ToString());
            Message(player, "settings", Title, settings);
        }

        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (!configData.Options.HonorRelationships) return false;

            if (configData.Options.useFriends && Friends != null)
            {
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if (configData.Options.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan != null && ownerclan != null)
                {
                    if (playerclan == ownerclan)
                    {
                        return true;
                    }
                }
            }
            if (configData.Options.useTeams)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player != null)
                {
                    if (player.currentTeam != 0)
                    {
                        RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                        if (playerTeam != null)
                        {
                            if (playerTeam.members.Contains(ownerid))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private string[] GetPlayerZones(BasePlayer player)
        {
            if (ZoneManager && configData.Options.useZoneManager)
            {
                return (string[])ZoneManager?.Call("GetPlayerZoneIDs", new object[] { player });
            }
            return null;
        }
        private string[] GetEntityZones(BaseEntity entity)
        {
            if (ZoneManager && configData.Options.useZoneManager)
            {
                if (entity.IsValid())
                {
                    return (string[])ZoneManager?.Call("GetEntityZoneIDs", new object[] { entity });
                }
            }
            return null;
        }

        private void DoLog(string message, int indent = 0)
        {
            if (!enabled) return;
            if (dolog)
            {
                if (logtofile) LogToFile(logfilename, "".PadLeft(indent, ' ') + message, this);
                else Puts(message);
            }
        }
        #endregion

        #region config
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            enabled = configData.Options.StartEnabled;
            dolog = configData.Options.StartLogging;
            logtofile = configData.Options.LogToFile;

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
                Version = Version,
                Rules = new Dictionary<string, bool>
                {
                    { "box.wooden.large", true },
                    { "woodbox_deployed", true },
                    { "bbq.deployed",     true },
                    { "fridge.deployed",  true },
                    { "workbench1.deployed", true },
                    { "workbench2.deployed", true },
                    { "workbench3.deployed", true },
                    { "cursedcauldron.deployed", true },
                    { "campfire",      true },
                    { "furnace.small", true },
                    { "furnace.large", true },
                    { "player",        true },
                    { "player_corpse", true },
                    { "recycler_static", false },
                    { "repairbench_deployed", false },
                    { "refinery_small_deployed", false },
                    { "researchtable_deployed", false },
                    { "mixingtable.deployed", false }
                },
                Schedule = ""
            };

            SaveConfig(config);
        }
        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        public class ConfigData
        {
            public Options Options = new Options();
            public Dictionary<string, bool> Rules = new Dictionary<string, bool>();
            public string[] Zones;
            public string Schedule = "";
            public VersionNumber Version;
        }

        public class Options
        {
            public bool RequirePermission = false;
            public bool useZoneManager = false;
            public bool useSchedule = false;
            public bool useRealTime = false;
            public bool useFriends = false;
            public bool useClans = false;
            public bool useTeams = false;
            public bool HonorRelationships = false;
            public bool OverrideOven = false;
            public bool OverrideTC = false;
            public bool StartEnabled = true;
            public bool StartLogging = false;
            public bool LogToFile = false;
        }

        public class Schedule
        {
            public List<int> day;
            public List<string> dayName;
            public string starthour;
            public string startminute;
            public string endhour;
            public string endminute;
            public bool enabled = true;
        }
        #endregion
    }
}