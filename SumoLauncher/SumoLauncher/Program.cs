using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace SumoScenarios
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var time = 75000;
            var startTime = 0;
            
            var argsList = args.ToList();
            argsList = new List<string> 
            {
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "18",//!
                "18",//!
                "18",//!
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",
                "28",

            };
               
            new SumoTLSettingsUpdater().Process(argsList);
            Thread.Sleep(20);
           
            var configFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "osm.khm*.sumocfg", SearchOption.TopDirectoryOnly).Select(f => Path.GetFileName(f));
            var results = new Dictionary<string, int> { };

            var simulation = new Simulation() {
                States = new List<SimulationState>()
            };

            foreach (var configFile in configFiles) {
                var res = new SumoTraci().Start(time + startTime, SumoTLSettingsUpdater._trafficLights.Keys.ToList(), configFile);
                simulation.States.AddRange(res.States);
            }
                    
            Console.ReadKey();
        }       
    }
}
