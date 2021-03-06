﻿using System;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

using SharedLibrary.Objects;
using static SharedLibrary.Server;

namespace SharedLibrary
{
    public static class Utilities
    {
        //Get string with specified number of spaces -- really only for visual output
        public static String GetSpaces(int Num)
        {
            String SpaceString = String.Empty;
            while (Num > 0)
            {
                SpaceString += ' ';
                Num--;
            }

            return SpaceString;
        }

        //Remove words from a space delimited string
        public static String RemoveWords(this string str, int num)
        {
            if (str == null || str.Length == 0)
                return "";

            String newStr = String.Empty;
            String[] tmp = str.Split(' ');

            for (int i = 0; i < tmp.Length; i++)
            {
                if (i >= num)
                    newStr += tmp[i] + ' ';
            }

            return newStr;
        }

        public static List<Player> PlayersFromStatus(String[] Status)
        {
            List<Player> StatusPlayers = new List<Player>();

            foreach (String S in Status)
            {
                String responseLine = S.Trim();

                if (Regex.Matches(responseLine, @"\d+$", RegexOptions.IgnoreCase).Count > 0 && responseLine.Length > 72) // its a client line!
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int cID = -1;
                    int Ping = -1;
                    Int32.TryParse(playerInfo[2], out Ping);
                    String cName = Utilities.StripColors(responseLine.Substring(46, 18)).Trim();
                    string npID = Regex.Match(responseLine, @"([a-z]|[0-9]){16}", RegexOptions.IgnoreCase).Value;
                    int.TryParse(playerInfo[0], out cID);
                    var regex = Regex.Match(responseLine, @"\d+\.\d+\.\d+.\d+\:\d{1,5}");
                    string cIP = regex.Value.Split(':')[0];
                    Player P = new Player() { Name = cName, NetworkId = npID, ClientNumber = cID, IPAddress = cIP, Ping = Ping };
                    StatusPlayers.Add(P);
                }
            }

            return StatusPlayers;
        }

        public static Player.Permission MatchPermission(String str)
        {
            String lookingFor = str.ToLower();

            for (Player.Permission Perm = Player.Permission.User; Perm < Player.Permission.Console; Perm++)
                if (lookingFor.Contains(Perm.ToString().ToLower()))
                    return Perm;

            return Player.Permission.Banned;
        }

        /// <summary>
        /// Remove all IW Engine color codes
        /// </summary>
        /// <param name="str">String containing color codes</param>
        /// <returns></returns>
        public static String StripColors(this string str)
        {
            if (str == null)
                return "";
            return Regex.Replace(str, @"(\^+((?![a-z]|[A-Z]).){0,1})+", "");
        }

        /// <summary>
        /// Get the IW Engine color code corresponding to an admin level
        /// </summary>
        /// <param name="level">Specified player level</param>
        /// <returns></returns>
        public static String ConvertLevelToColor(Player.Permission level)
        {
            switch (level)
            {
                case Player.Permission.Banned:
                    return "^1" + Player.Permission.Banned;
                case Player.Permission.Flagged:
                    return "^9" + Player.Permission.Flagged;
                case Player.Permission.Owner:
                    return "^5" + Player.Permission.Owner;
                case Player.Permission.User:
                    return "^2" + Player.Permission.User;
                case Player.Permission.Trusted:
                    return "^3" + Player.Permission.Trusted;
                default:
                    return "^6" + level;
            }
        }

        public static String ProcessMessageToken(IList<Helpers.MessageToken> tokens, String str)
        {
            MatchCollection RegexMatches = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in RegexMatches)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);

                var found = tokens.FirstOrDefault(t => t.Name.ToLower() == Identifier.ToLower());

                if (found != null)
                    str = str.Replace(Match, found.ToString());
            }

            return str;
        }

        public static bool IsBroadcastCommand(this string str)
        {
            return str[0] == '@';
        }

        /// <summary>
        /// Get the full gametype name
        /// </summary>
        /// <param name="input">Shorthand gametype reported from server</param>
        /// <returns></returns>
        public static String GetLocalizedGametype(String input)
        {
            switch (input)
            {
                case "dm":
                    return "Deathmatch";
                case "war":
                    return "Team Deathmatch";
                case "koth":
                    return "Headquarters";
                case "ctf":
                    return "Capture The Flag";
                case "dd":
                    return "Demolition";
                case "dom":
                    return "Domination";
                case "sab":
                    return "Sabotage";
                case "sd":
                    return "Search & Destroy";
                case "vip":
                    return "Very Important Person";
                case "gtnw":
                    return "Global Thermonuclear War";
                case "oitc":
                    return "One In The Chamber";
                case "arena":
                    return "Arena";
                case "dzone":
                    return "Drop Zone";
                case "gg":
                    return "Gun Game";
                case "snipe":
                    return "Sniping";
                case "ss":
                    return "Sharp Shooter";
                case "m40a3":
                    return "M40A3";
                case "fo":
                    return "Face Off";
                case "dmc":
                    return "Deathmatch Classic";
                case "killcon":
                    return "Kill Confirmed";
                case "oneflag":
                    return "One Flag CTF";
                default:
                    return input;
            }
        }

        public static String DateTimeSQLite(DateTime datetime)
        {
            return datetime.ToString("yyyy-MM-dd H:mm:ss");
        }

        public static String GetTimePassed(DateTime start)
        {
            return GetTimePassed(start, true);
        }

        public static String GetTimePassed(DateTime start, bool includeAgo)
        {
            TimeSpan Elapsed = DateTime.UtcNow - start;
            string ago = includeAgo ? " ago" : "";

            if (Elapsed.TotalSeconds < 30 && includeAgo)
                return "just now";
            if (Elapsed.TotalMinutes < 120)
            {
                if (Elapsed.TotalMinutes < 1.5)
                    return $"1 minute{ago}";
                return Math.Round(Elapsed.TotalMinutes, 0) + $" minutes{ago}";
            }
            if (Elapsed.TotalHours <= 24)
            {
                if (Elapsed.TotalHours < 1.5)
                    return $"1 hour{ago}";
                return Math.Round(Elapsed.TotalHours, 0) + $" hours{ago}";
            }
            if (Elapsed.TotalDays <= 365)
            {
                if (Elapsed.TotalDays < 1.5)
                    return $"1 day{ago}";
                return Math.Round(Elapsed.TotalDays, 0) + $" days{ago}";
            }
            else
                return $"a very long time{ago}";
        }

        public static Game GetGame(string gameName)
        {
            if (gameName.Contains("IW4"))
                return Game.IW4;
            if (gameName.Contains("CoD4"))
                return Game.IW3;
            if (gameName.Contains("WaW"))
                return Game.T4;
            if (gameName.Contains("COD_T5_S"))
                return Game.T5;
            if (gameName.Contains("IW5"))
                return Game.IW5;

            return Game.UKN;
        }

        public static TimeSpan ParseTimespan(this string input)
        {
            var expressionMatch = Regex.Match(input, @"[0-9]+.\b");

            if (!expressionMatch.Success) // fallback to default tempban length of 1 hour
                return new TimeSpan(1, 0, 0);

            char lengthDenote = expressionMatch.Value[expressionMatch.Value.Length - 1];
            int length = Int32.Parse(expressionMatch.Value.Substring(0, expressionMatch.Value.Length - 1));

            switch (lengthDenote)
            {
                case 'm':
                    return new TimeSpan(0, length, 0);
                case 'h':
                    return new TimeSpan(length, 0, 0);
                case 'd':
                    return new TimeSpan(length, 0, 0, 0);
                case 'w':
                    return new TimeSpan(length * 7, 0, 0, 0);
                case 'y':
                    return new TimeSpan(length * 365, 0, 0, 0);
                default:
                    return new TimeSpan(1, 0, 0);
            }
        }

        public static string TimeSpanText(this TimeSpan span)
        {
            if (span.TotalMinutes < 60)
                return $"{span.Minutes} minute(s)";
            else if (span.Hours >= 1 && span.TotalHours < 24)
                return $"{span.Hours} hour(s)";
            else if (span.TotalDays >= 1 && span.TotalDays < 7)
                return $"{span.Days} day(s)";
            else if (span.TotalDays >= 7 && span.TotalDays < 365)
                return $"{Math.Ceiling(span.Days / 7.0)} week(s)";
            else if (span.TotalDays >= 365 && span.TotalDays < 36500)
                return $"{Math.Ceiling(span.Days / 365.0)} year(s)";
            else if (span.TotalDays >= 36500)
                return "Forever";

            return "1 hour";
        }

        public static string EscapeMarkdown(this string markdownString)
        {
            return markdownString.Replace("<", "\\<").Replace(">", "\\>").Replace("|", "\\|");
        }

        public static Player AsPlayer(this Database.Models.EFClient client)
        {
            return client == null ? null : new Player()
            {
                Active = client.Active,
                AliasLink = client.AliasLink,
                AliasLinkId = client.AliasLinkId,
                ClientId = client.ClientId,
                ClientNumber = 0,
                FirstConnection = client.FirstConnection,
                Connections = client.Connections,
                NetworkId = client.NetworkId,
                TotalConnectionTime = client.TotalConnectionTime,
                Masked = client.Masked,
                Name = client.CurrentAlias.Name,
                IPAddress = client.CurrentAlias.IPAddress,
                Level = client.Level,
                LastConnection = DateTime.UtcNow,
                CurrentAlias = client.CurrentAlias
            };
        }
    }
}
