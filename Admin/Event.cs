﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace IW4MAdmin
{
    class Event
    {
        public enum GType
        {
            //FROM SERVER
            Connect,
            Disconnect,
            Say,
            Kill,
            Death,
            MapChange,
            MapEnd,
        
            //FROM ADMIN
            Broadcast,
            Tell,
            Kick,
            Ban, 

            Unknown,
        }

        public Event(GType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d;
            Origin = O;
            Target = T;
            Owner = S;
        }

        //This needs to be here
        public Command isValidCMD(List<Command> list)
        {
            if (this.Data.Substring(0, 1) == "!")
            {
                string[] cmd = this.Data.Substring(1, this.Data.Length - 1).Split(' ');

                foreach (Command C in list)
                {
                    if (C.getName() == cmd[0].ToLower() || C.getAlias() == cmd[0].ToLower())
                        return C;
                }

                return null;
            }

            else
                return null;
        }

        public static Event requestEvent(String[] line, Server SV)
        {
            try
            {
                String eventType = line[0].Substring(line[0].Length - 1);
                eventType = eventType.Trim();

                if (eventType == "J")
                    return new Event(GType.Connect, null, SV.clientFromLine(line, 3, true), null, SV);

                if (eventType == "Q")
                    return new Event(GType.Disconnect, null, SV.clientFromLine(line, 3, false), null, null);

                if (line[0].Substring(line[0].Length - 3).Trim() == "say")
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -! -_]");
                    string message = rgx.Replace(line[4], "");
                    if (message.Length < 2)
                        message = " ";
                    return new Event(GType.Say, Utilities.removeNastyChars(message), SV.clientFromLine(line, 3, false), null, null);
                }

                if (eventType == "d")
                    return new Event(GType.MapEnd, null, null, null, null);

                if (line[0].Length > 400) // blaze it
                    return new Event(GType.MapChange, null, null, null, null);


                return null;
            }
            catch (Exception E)
            {
                SV.Log.Write("Error requesting event " + E.Message, Log.Level.Debug);
                return null;
            }
        }


        public GType Type;
        public string Data; // Data is usually the message sent by player
        public Player Origin;
        public Player Target;
        public Server Owner;

    }
}
