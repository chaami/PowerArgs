﻿using Newtonsoft.Json;
using PowerArgs.Cli.Physics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleZombies
{
    public class LevelDefinition : List<ThingDefinition>
    {
        public static readonly int Width = 78;
        public static readonly int Height = 20;

        public static string LevelBuilderLevelsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Levels", "Local");
        
        public void Save(string levelName)
        {
            string fileName = levelName;
            if(Path.IsPathRooted(levelName) == false && File.Exists(levelName) == false)
            {
                fileName = System.IO.Path.Combine(LevelBuilderLevelsPath, levelName + ".czl");
            }

            var dir = Path.GetDirectoryName(fileName);
            if(Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            var defContents = JsonConvert.SerializeObject(this, Formatting.Indented);
            System.IO.File.WriteAllText(fileName, defContents);
        }

        public static List<string> GetLevelDefinitionFiles()
        {
            if (System.IO.Directory.Exists(LevelBuilderLevelsPath) == false)
            {
                return new List<string>();
            }
            return System.IO.Directory.GetFiles(LevelBuilderLevelsPath).Where(f => f.EndsWith(".czl")).ToList();
        }

        public static LevelDefinition Load(string file)
        {
            if(System.IO.File.Exists(file) == false)
            {
                file = System.IO.Path.Combine(LevelBuilderLevelsPath, file + ".czl");
            }

            var defContents = System.IO.File.ReadAllText(file);
            if (defContents == string.Empty)
            {
                return new LevelDefinition();
            }
            else
            {
                var def = JsonConvert.DeserializeObject<LevelDefinition>(defContents);
                return def;
            }
        }

        public void Populate(Scene scene, bool builderMode)
        {
            List<Door> doors = new List<Door>();
            foreach (var thingDef in this)
            {
                var thingType = Assembly.GetExecutingAssembly().GetType(thingDef.ThingType);
                var thing = Activator.CreateInstance(thingType) as PowerArgs.Cli.Physics.Thing;
                thing.Bounds = new PowerArgs.Cli.Physics.Rectangle(thingDef.InitialBounds.X, thingDef.InitialBounds.Y, thingDef.InitialBounds.W, thingDef.InitialBounds.H);

                
                if(thing is MainCharacter)
                {
                    (thing as MainCharacter).IsInLevelBuilder = builderMode;
                }
                else if(thing is Door)
                {
                    doors.Add(thing as Door);
                    var closedRect = new Rectangle(
                        float.Parse(thingDef.InitialData["ClosedX"]),
                        float.Parse(thingDef.InitialData["ClosedY"]),
                        float.Parse(thingDef.InitialData["W"]),
                        float.Parse(thingDef.InitialData["H"]));

                    var openLocation = new Location(
                        float.Parse(thingDef.InitialData["OpenX"]),
                        float.Parse(thingDef.InitialData["OpenY"]));

                    (thing as Door).Initialize(closedRect, openLocation);
                    (thing as Door).IsOpen = thingDef.InitialData.ContainsKey("IsOpen") && thingDef.InitialData["IsOpen"].ToLower() == "true";
                }
                else if(thing is Ammo)
                {
                    int amount = int.Parse(thingDef.InitialData["Amount"]);
                    (thing as Ammo).Amount = amount;
                }
                else if(thing is Portal)
                {
                    (thing as Portal).DestinationId = thingDef.InitialData["DestinationId"];
                }


                scene.Add(thing);
            }

            doors.ForEach(d => d.IsOpen = d.IsOpen);
        }
    }

    public class ThingDefinition
    {
        public string ThingType { get; set; }
        public Rectangle InitialBounds { get; set; }
        public Dictionary<string, string> InitialData { get; set; } = new Dictionary<string, string>();
    }
}
