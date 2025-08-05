using CodingConnected.TraCI.NET.Types;
using CodingConnected.TraCI.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace DataTransformation
{
   

    public class Transform
    {
        #region Creating SMO process and serving it

        static Process ServeSumo(string sumoCfgFile, int remotePort,
            bool useSumoGui = true, bool quitOnEnd = true, bool redirectOutputToConsole = false)
        {
            Process sumoProcess;

            /* Serve Sumo Gui or Sumo */
            try
            {
                var args = " -c " + sumoCfgFile +
                    " --remote-port " + remotePort.ToString() +
                    (useSumoGui ? " --start " : " ") + // this arguments only makes sense if using gui
                    (quitOnEnd ? " --quit-on-end  " : " ")
                    + " --step-length 1 "
                    + " --threads 12 "
                    + " --tripinfo-output.write-unfinished"
                    + " --no-warnings true" 
                    + " --random false "
                    + " --default.speeddev 0";
              
                // Assumes that bin is in PATHs
                var sumoExecutable = useSumoGui ? @"sumo-gui" : "sumo";

                sumoProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        Arguments = args,
                        FileName = sumoExecutable, // The executable for the sumo

                        /* Ignore the rest if you don't care for redirecting the output to console */
                        CreateNoWindow = redirectOutputToConsole || true,
                        UseShellExecute = false,//!redirectOutputToConsole,
                        ErrorDialog = false,
                        RedirectStandardOutput = redirectOutputToConsole,
                        RedirectStandardError = redirectOutputToConsole,
                    },

                    EnableRaisingEvents = redirectOutputToConsole //Not importand if not redirecting output
                };

                if (redirectOutputToConsole)
                {
                    sumoProcess.ErrorDataReceived += SumoProcess_ErrorDataReceived;
                    sumoProcess.OutputDataReceived += SumoProcess_OutputDataReceived;
                }

                sumoProcess.Start();

                if (redirectOutputToConsole)
                {
                    sumoProcess.BeginErrorReadLine();
                    sumoProcess.BeginOutputReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception:" + e);
                Console.WriteLine("Please enter a correct path to sumocfg file");
                return null;
            }

            return sumoProcess;
        }

        /// <summary>
        /// Ignore if you don't care about redirecting output
        /// </summary>
        private static void SumoProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("SUMO stdout : " + e.Data);
        }

        /// <summary>
        /// Ignore if you don't care about redirecting output
        /// </summary>
        private static void SumoProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("SUMO stderr : " + e.Data);
        }

        #endregion

        #region static variables

        private static string DEFAULT_SUMOCFG = "osm.khm.sumocfg";

        private static readonly Dictionary<string, int> _trafficLights = new Dictionary<string, int>()
        {
            { "367834710", 4 },
            { "435727413", 4 },
            { "313334802", 4 },
            { "cluster_9814553094_9852473311_9852473312", 4 },
            { "435717983", 6 },
            { "435717987", 4 },
            { "3385881497", 4 },
            { "3770135561", 4 },
            { "313334769", 4 },
            { "2906105650", 4 },
        };
        #endregion


        private class Obj { 
            public int timestamp { get; set; }
            public List<List<int>> vehicles { get; set; }
        }

        private class ObjOutput
        {
            public string timestamp { get; set; }
            public List<List<int>> vehicles { get; set; }
        }
        private class TimeWindow
        {
            public int starttime { get; set; }
            public int endtime { get; set; }
        }

        private class Data { 
            public string starttime { get; set; }
            public string endtime { get; set; }
            public List<ObjOutput> values { get; set; }
        }

        public void Start(string sumoCfgFile = null)
        {
            /* Create a TraCIClient for the commands */
            var client = new TraCIClient();

            var sumoCfgPath = string.IsNullOrWhiteSpace(sumoCfgFile) ? DEFAULT_SUMOCFG : sumoCfgFile;

            /* Create a new sumo process so the client can connect to it. 
             * This step is optional if a sumo server is already running. */
            var sumoProcess = ServeSumo(sumoCfgPath, 4321, useSumoGui: false, redirectOutputToConsole: false);
            if (sumoProcess == null)
            {
                Console.WriteLine("Something went wrong launching SUMO server. Maybe .sumocfg path is wrong" +
                    "or sumo executables not defined in PATH.\n Sumo Configuration Path provided " + sumoCfgPath);
            }

            /* Connecting to Sumo Server is async but we wait for the task to complete for simplicity */
            var task = client.ConnectAsync("127.0.0.1", 4321);
            while (!task.IsCompleted) { /*  Wait for task to be completed before using traci commands */ }

            var input = new { data = new List<Obj>()};
          
            var tlList = new List<TrafficLight>();

            foreach (var tlID in _trafficLights.Keys.ToList()) {
                var tl = client.TrafficLight.GetControlledLanes(tlID);
                if (tl != null && tl.Content != null)
                {
                    tlList.Add(new TrafficLight { tlId = tlID, ControlledLanes = tl.Content.Distinct().ToList() });
                }
                else {
                    Console.WriteLine($"ERROR: TL {tlID} - controlled lanes not found");
                }
            }

            var maxLanesCount = tlList.Select(x => x.ControlledLanes.Count).Max();

            foreach (var tl in tlList)
            {
                if (tl.ControlledLanes.Count < maxLanesCount) { 
                    while (tl.ControlledLanes.Count < maxLanesCount) {
                        tl.ControlledLanes.Add("-");
                    }
                }
            }

            Simulation simulation = JsonConvert.DeserializeObject<Simulation>(File.ReadAllText($"State.json", Encoding.UTF8));

            foreach (var state in simulation.States) {
                var summVehsCount = new List<List<int>>();

                
                foreach (var tl in tlList) {
                    var tlLanes = tl.ControlledLanes;

                    var tlVehsCount = new List<int>();

                    foreach (var lane in tlLanes) {
                        var laneVehsCount = state.tlVehicles.FirstOrDefault(x => x.laneId == lane);

                        if (laneVehsCount != null)
                        {
                            tlVehsCount.Add(laneVehsCount.vehiclesCount);
                        }
                        else {
                            tlVehsCount.Add(0);
                        }                        
                    }

                    summVehsCount.Add(tlVehsCount);
                }
                               
                input.data.Add(new Obj { timestamp = state.Time, vehicles = summVehsCount });
            }

            var windowSize = 1800;
            var windowStep = 600;

            var finalInput = new { data = new List<Data>() };

            var timestamps = input.data.Select(x => x.timestamp).ToArray();
            var timeWindows = new List<TimeWindow>();

            for (int ind = 0; ind < timestamps.Count(); ind++) {
                var start_time = timestamps[ind];

                if ((start_time - timestamps[0]) % windowStep != 0) {
                    continue;
                }

                var end_time = start_time + windowSize;

                if (end_time < timestamps[timestamps.Count() - 1]) {
                    timeWindows.Add(new TimeWindow { starttime = start_time, endtime = end_time });
                }
            }

            foreach (var tw in timeWindows) {
                finalInput.data.Add(new Data { 
                    starttime = $"{new DateTime(2025, 1, 1, 0, 0, 0).AddSeconds(tw.starttime):yyyy-MM-dd HH:mm:ss}", 
                    endtime = $"{new DateTime(2025, 1, 1, 0, 0, 0).AddSeconds(tw.endtime):yyyy-MM-dd HH:mm:ss}", 
                    values = input.data.Where(x => x.timestamp >= tw.starttime && x.timestamp < tw.endtime)
                    .Select(x => new ObjOutput { 
                        timestamp = $"{new DateTime(2025, 1, 1, 0, 0, 0).AddSeconds(x.timestamp):yyyy-MM-dd HH:mm:ss}",
                        vehicles = x.vehicles
                    }).ToList() });
            }

            File.WriteAllText($"State_SKLearn.json", JsonConvert.SerializeObject(finalInput), Encoding.UTF8);

            client.Control.Close();
            client.Dispose();
        }       
    }

    public class Simulation
    {
        public List<SimulationState> States { get; set; }
    }

    public class SimulationState
    {
        public int Time { get; set; }
        public List<LaneVehiclesCount> tlVehicles { get; set; }
    }

    public class TrafficLightVehicles
    {
        public string tlId { get; set; }
        public List<Vehicle> Vehicles { get; set; }
    }

    public class Vehicle
    {
        public string vehId { get; set; }
        public string fromLane { get; set; }
        public string fromRoad { get; set; }
        public string toRoad { get; set; }
        public string fromStreet { get; set; }
        public string toStreet { get; set; }
        public string fromTaz { get; set; }
        public string toTaz { get; set; }
        public string fromTrip { get; set; }
        public string toTrip { get; set; }
    }


    public class TrafficLight
    {
        public string tlId { get; set; }

        public List<string> ControlledLanes { get; set; }
    }

    public class LaneVehiclesCount
    {
        public string laneId { get; set; }
        public int vehiclesCount { get; set; }
    }

    public class LaneVehicle
    {
        public string vehId { get; set; }
        public string tlId { get; set; }
        public string laneId { get; set; }
    }

}
