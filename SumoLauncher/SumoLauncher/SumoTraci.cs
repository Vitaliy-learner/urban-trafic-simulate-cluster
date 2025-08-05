using CodingConnected.TraCI.NET.Types;
using CodingConnected.TraCI.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Xml;

namespace SumoScenarios
{
    public class SumoTraci
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
        
        //Path.Combine("..", "..", "sumo-scenarios", "usage-example", "cross.sumocfg");
        //Path.Combine("..", "..", "sumo-scenarios", "usage-example", "osm.khm.sumocfg");

        /* The Variables used for Variable and Context Subscription for this example */
        private static List<byte> variablesToSubscribeTo = new List<byte>()
        {
            TraCIConstants.VAR_SPEED,
            TraCIConstants.VAR_ANGLE,
            TraCIConstants.VAR_ACCEL,
            TraCIConstants.VAR_ROUTE_ID
        };

        private static List<byte> globlVariablesToSubscribeTo = new List<byte>
        {
            TraCIConstants.ID_COUNT
        };

        private static List<byte> trafficLightVariablesToSubscribeTo = new List<byte>
        {
            //TraCIConstants.TL_EXTERNAL_STATE,
            TraCIConstants.TL_NEXT_SWITCH,
            TraCIConstants.TL_CURRENT_PHASE
        };

        private static int NumberOfVehcicles;
        private static List<string> vehicleIds;

        #endregion

        private static string ByteToHex(byte? b)
        {
            return $"0x{((byte)b).ToString("X2")}";
        }

        #region subscription listeners

        private static void Client_VehicleSubscriptionUsingResponses(object sender, SubscriptionEventArgs e)
        {
            var objectID = e.ObjectId;
            Console.WriteLine("*** Vehicle Variable Subscription OLD WAY for compatability. (using Responses) ***");
            Console.WriteLine("Subscription Object Id: " + objectID);
            Console.WriteLine("Variable Count        : " + e.VariableCount); // Prints the number of variables that were subscribed to

            foreach (var r in e.Responses)
            {
                /* Responses are object that can be casted to IResponseInfo so we can retrieve 
                 the variable type. */
                var respInfo = (r as IResponseInfo);
                var variableCode = respInfo.Variable;

                /*We can then cast to TraCIResponse to get the Content
                 We can also use IResponseInfo.GetContentAs<> ()s*/
                // WARNING using TraCIResponse<> we must use the exact type (i.e for speed, accel, angle, is double and not float)
                switch (variableCode)
                {
                    case TraCIConstants.ID_COUNT:
                        NumberOfVehcicles = respInfo.GetContentAs<int>();
                        break;
                    case TraCIConstants.VAR_SPEED:
                        Console.WriteLine(" VAR_SPEED  " + (r as TraCIResponse<double>).Content);
                        break;
                    case TraCIConstants.VAR_ANGLE:
                        Console.WriteLine(" VAR_ANGLE  " + respInfo.GetContentAs<float>());
                        break;
                    case TraCIConstants.VAR_ROUTE_ID:
                        Console.WriteLine(" VAR_ROUTE_ID  " + (r as TraCIResponse<string>).Content);
                        break;
                    default:
                        /* Intentionaly ommit VAR_ACCEL*/
                        Console.WriteLine($" Variable with code {ByteToHex(variableCode)} not handled ");
                        break;
                }
            }

        }

        /// <summary>
        /// Event Args are still SubscriptionEventArgs for backwards compatability but 
        /// can be casted to VariableSubscriptionEventArgs for ResponseByVariableCode support.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Client_VehicleSubscriptionUsingDictionary(object sender, SubscriptionEventArgs e)
        {
            Console.WriteLine("*** Vehicle Variable Subscription using dictionary ***");
            /* Get the subscribed object id */
            var objectID = e.ObjectId;

            /* We can cast to VariableSubscriptionEventArgs to use the new features where 
             we can get the response by the Variable Type */
            var eventArgsNew = e as VariableSubscriptionEventArgs;

            Console.WriteLine("Subscription Object Id: " + objectID);
            Console.WriteLine("Variable Count        : " + e.VariableCount);

            var responseInfo = eventArgsNew.ResponseByVariableCode[TraCIConstants.VAR_SPEED];
            Console.WriteLine(" VAR_SPEED  " + responseInfo.GetContentAs<float>());

            responseInfo = eventArgsNew.ResponseByVariableCode[TraCIConstants.VAR_ACCEL];
            Console.WriteLine(" VAR_SPEED  " + responseInfo.GetContentAs<float>());

            // We can Can still retrieve using TraCIResult. 
            // TraCIResult implements IResponseInfo. 
            // WARNING using TraCIResponse<> we must use the exact type (i.e for angle is double)
            var traCIResponse = (TraCIResponse<double>)eventArgsNew.ResponseByVariableCode?[TraCIConstants.VAR_ANGLE];
            Console.WriteLine(" VAR_ANGLE  " + traCIResponse?.Content);

            Console.WriteLine(" VAR_ROUTE_ID " + eventArgsNew.ResponseByVariableCode?[TraCIConstants.VAR_ROUTE_ID].GetContentAs<string>());

        }

        private static void Client_VehicleContextSubscriptionUsingDictionary(object sender, ContextSubscriptionEventArgs e)
        {
            Console.WriteLine("*** Vehicle Context Subscription using Dictionaries ***");
            Console.WriteLine("EGO Object id              : " + e.ObjectId);
            Console.WriteLine("Context Domain             : " + ByteToHex(e.ContextDomain));
            Console.WriteLine("Variable Count             : " + e.VariableCount);
            Console.WriteLine("Number of objects in range : " + e.ObjectCount);

            var egoObjectID = e.ObjectId;
            Console.WriteLine("EGO Object " + " id: " + egoObjectID);

            Console.WriteLine("Iterating responses...");
            Console.WriteLine("Objects inside Context Range:");
            foreach (var r in e.Responses) /* Responses are TraCIVariableSubscriptionResponse */
            {
                var variableSubscriptionResponse = r as TraCIVariableSubscriptionResponse;
                var vehicleID = variableSubscriptionResponse.ObjectId;
                Console.WriteLine(" Object id: " + vehicleID);
                Console.WriteLine("     VAR_SPEED  " +
                    (variableSubscriptionResponse.ResponseByVariableCode[TraCIConstants.VAR_SPEED]).GetContentAs<float>());
                Console.WriteLine("     VAR_ACCEL  " +
                    (variableSubscriptionResponse.ResponseByVariableCode[TraCIConstants.VAR_ACCEL]).GetContentAs<float>());
                Console.WriteLine("     VAR_ANGLE  " +
                    /* We can also use TraCIResult<> (). Warning using TraCIResponse<> we must use the exact type (i.e for angle is double) */
                    (variableSubscriptionResponse.ResponseByVariableCode[TraCIConstants.VAR_ANGLE] as TraCIResponse<double>).Content);
                Console.WriteLine("     VAR_ROUTE  " +
                    (variableSubscriptionResponse.ResponseByVariableCode[TraCIConstants.VAR_ROUTE_ID]).GetContentAs<string>());
            }

            //We can also get TraCIVariableSubscriptionResponse by objectID
            Console.WriteLine("Iterating objectIds of dictionary with objects inside context range:" +
                "\n Printing VAR_SPEED for demonstration");
            foreach (var id in e.VariableSubscriptionByObjectId.Keys)
            {
                Console.WriteLine(" Object inside ego object range id: " + id);
                var varResp = e.VariableSubscriptionByObjectId[id];
                /* We can handle variable responses like before either iterating responses or
                 by using value by variable type */
                //*Printing VAR_SPEED just for demostration 
                Console.WriteLine("     VAR_SPEED" + varResp.ResponseByVariableCode[TraCIConstants.VAR_SPEED].GetContentAs<float>());
            }
        }

        private static void Client_VehicleContextSubscriptionUsingResponses(object sender, ContextSubscriptionEventArgs e)
        {
            Console.WriteLine("*** Vehicle Context Subscription using responses ***");
            Console.WriteLine("EGO Object id              : " + e.ObjectId);
            Console.WriteLine("Context Domain             : " + ByteToHex(e.ContextDomain));
            Console.WriteLine("Variable Count             : " + e.VariableCount);
            Console.WriteLine("Number of objects in range : " + e.ObjectCount);

            Console.WriteLine("Objects inside Context Range:");
            foreach (var r in e.Responses) /* Responses are TraCIVariableSubscriptionResponse */
            {
                var variableSubscriptionResponse = r as TraCIVariableSubscriptionResponse;
                var vehicleID = variableSubscriptionResponse.ObjectId;

                Console.WriteLine(" Object id: " + vehicleID);
                foreach (var response in variableSubscriptionResponse.Responses)
                {
                    var variableResponse = ((IResponseInfo)response);
                    var variableCode = variableResponse.Variable;

                    switch (variableCode)
                    {
                        case TraCIConstants.VAR_SPEED: // Returns the speed of the named vehicle within the last step [m/s]; error value: -2^30
                            Console.WriteLine("     VAR_SPEED  " + ((TraCIResponse<double>)response).Content);
                            break;
                        case TraCIConstants.VAR_ANGLE: // Returns the angle of the named vehicle within the last step [°]; error value: -2^30
                            Console.WriteLine("     VAR_ANGLE  " + ((TraCIResponse<double>)variableResponse).Content);
                            break;
                        case TraCIConstants.VAR_ROUTE_ID:
                            Console.WriteLine("     VAR_ROUTE_ID " + ((TraCIResponse<string>)variableResponse).Content);
                            break;
                        default:
                            /* Intentionaly ommit VAR_ACCEL*/
                            Console.WriteLine($"    Variable with code {ByteToHex(variableCode)} not handled ");
                            break;
                    }
                }
            }
        }

        #endregion

        #region Printing vehicle ids methods

        private static void PrintActiveVehicles(TraCIClient client)
        {
            vehicleIds = client.Vehicle.GetIdList().Content;
            Console.Write("Active Vehicles: [");
            foreach (var id in vehicleIds)
            {
                Console.Write($"{id} ,");
            }
            Console.WriteLine("]");
        }

        private static void PrintArrivedVehicles(TraCIClient client)
        {
            vehicleIds = client.Simulation.GetArrivedIDList("ignored").Content;
            Console.Write("Arrived Vehicles: [");
            foreach (var id in vehicleIds)
            {
                Console.Write($"{id} ,");
            }
            Console.WriteLine("]");
        }

        private static void PrintLoadedVehicles(TraCIClient client)
        {
            vehicleIds = client.Simulation.GetLoadedIDList("ignored").Content;
            Console.Write("Loaded Vehicles: [");
            foreach (var id in vehicleIds)
            {
                Console.Write($"{id} ,");
            }
            Console.WriteLine("]");
        }

        private static void PrintDepartedVehicles(TraCIClient client)
        {
            vehicleIds = client.Simulation.GetDepartedIDList("ignored").Content;
            Console.Write("Departed Vehicles: [");
            foreach (var id in vehicleIds)
            {
                Console.Write($"{id} ,");
            }
            Console.WriteLine("]");
        }

        #endregion Printing vehicle ids methods

        private static void SubscriptionTrafficLight(object sender, SubscriptionEventArgs args)
        {
            Console.WriteLine("SubscriptionTrafficLight");
        }

        private List<int> GetTLPhases(TraCIClient client, List<string> tlIDs) {
            var res = new List<int>();

            foreach (var tl in tlIDs) {
                res.Add(client.TrafficLight.GetCurrentPhase(tl).Content);
            }

            return res;
        }
        public Simulation Start(int steps, List<string> tlIDs, string sumoCfgFile = null)
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

           
            //var tlResetTimes = new List<int>() { 3600, 7200, 10800 };

            var tlList = new List<TrafficLight>();

            foreach (var tlID in tlIDs) {
                var tl = client.TrafficLight.GetControlledLanes(tlID);
                
                if (tl != null && tl.Content != null)
                {
                    tlList.Add(new TrafficLight { tlId = tlID, ControlledLanes = tl.Content.Distinct().ToList() });
                }
                else {
                    Console.WriteLine($"ERROR: TL {tlID} - controlled lanes not found");
                }
            }

            XmlDocument tripsDoc = new XmlDocument();
            tripsDoc.Load("osm.khm.taz.trips.xml");

            XmlNodeList trips = tripsDoc.GetElementsByTagName("trip");
           
            var carInfos = trips.Cast<XmlNode>().Select(x => new
            {
                id = x.Attributes?.GetNamedItem("id")?.Value,
                fromTaz = x.Attributes?.GetNamedItem("fromTaz")?.Value,
                toTaz = x.Attributes?.GetNamedItem("toTaz")?.Value,
                from = x.Attributes?.GetNamedItem("from")?.Value,
                to = x.Attributes?.GetNamedItem("to")?.Value
            }).ToList();

            XmlDocument rndTripsDoc = new XmlDocument();
            rndTripsDoc.Load("osm.khm.random.trips.xml");

            XmlNodeList rndTrips = rndTripsDoc.GetElementsByTagName("trip");

            carInfos.AddRange(rndTrips.Cast<XmlNode>().Select(x => new
            {
                id = x.Attributes?.GetNamedItem("id")?.Value,
                fromTaz = "",
                toTaz = "",
                from = x.Attributes?.GetNamedItem("from")?.Value,
                to = x.Attributes?.GetNamedItem("to")?.Value
            }).ToList());

            var simulation = new Simulation { States = new List<SimulationState>() };

            int time = 0;
            var tlVehicles = new List<LaneVehicle>();

            var laneVehiclesCount = new List<LaneVehiclesCount>();
            do
            {
                time = (int)client.Simulation.GetTime("").Content;

             
                var vehsToRemove = new List<LaneVehicle>();

                foreach (var veh in tlVehicles)
                {

                    var laneId = client.Vehicle.GetLaneID(veh.vehId);

                    var lane = "-";

                    if (laneId != null && !string.IsNullOrWhiteSpace(laneId.Content))
                    {
                        lane = laneId.Content;
                    }

                    var isAdd = true;
                    
                    if (veh.laneId != lane)
                    {
                        var tl = tlList.FirstOrDefault(x => x.ControlledLanes.Contains(lane));

                        if (tl != null)
                        {
                            if (tl.tlId == veh.tlId) {
                                isAdd = false;
                                veh.laneId = lane;
                            }
                        }

                        if (isAdd) { 
                            if (!laneVehiclesCount.Any(x => x.laneId == veh.laneId))
                            {
                                laneVehiclesCount.Add(new LaneVehiclesCount { laneId = veh.laneId, vehiclesCount = 1 });
                            }
                            else
                            {
                                laneVehiclesCount.First(x => x.laneId == veh.laneId).vehiclesCount++;
                            }

                            vehsToRemove.Add(veh);
                        }
                    }
                }

                foreach (var veh in vehsToRemove) {
                    tlVehicles.Remove(tlVehicles.FirstOrDefault(x => x.vehId == veh.vehId));
                }


                var vehicleIds = client.Vehicle.GetIdList();
                                
                if (vehicleIds != null && vehicleIds.Content != null)
                {
                    foreach (var vehicleId in vehicleIds.Content)
                    {
                        var lanePos = client.Vehicle.GetLanePosition(vehicleId);
                        if (lanePos.Content > 0)
                        {
                            var laneId = client.Vehicle.GetLaneID(vehicleId);

                            var lane = "";
                            var road = "";

                            if (laneId != null && !string.IsNullOrWhiteSpace(laneId.Content))
                            {
                                lane = laneId.Content;
                                road = lane.Remove(lane.IndexOf("_"));
                            }

                      
                            var tl = tlList.FirstOrDefault(x => x.ControlledLanes.Contains(lane));

                            if (tl != null) { 
                                var tlLane = tl.ControlledLanes.FirstOrDefault(x => x == lane);

                                if (tlLane != null)
                                {

                                    var route = client.Vehicle.GetRoute(vehicleId);
                                    var nextEdgeId = "";

                                    var routeIndex = client.Vehicle.GetRouteIndex(vehicleId);

                                    if (routeIndex.Content >= 0)
                                    {

                                        if (routeIndex != null && routeIndex.Content >= 0)
                                        {
                                            nextEdgeId = routeIndex.Content < (route.Content.Count - 1) ? route.Content[routeIndex.Content + 1] : "END";
                                        }
                                    }

                                    if (!tlVehicles.Any(x => x.vehId == vehicleId))
                                    {
                                        tlVehicles.Add(new LaneVehicle
                                        {
                                            vehId = vehicleId,
                                            tlId = tl.tlId,
                                            laneId = lane
                                        });
                                    }
                                    else {
                                        if (tlVehicles.Any(x => x.vehId == vehicleId && x.tlId == tl.tlId && x.laneId  != lane))
                                        {
                                            var changedLineVehs = tlVehicles.Where(x => x.vehId == vehicleId && x.tlId == tl.tlId && x.laneId != lane);
                                            foreach (var veh in changedLineVehs) {
                                                veh.laneId = lane;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (time > 0 && time % 30 == 0)
                {
                    var state = new SimulationState { Time = time, tlVehicles = laneVehiclesCount };
                    simulation.States.Add(state);

                    laneVehiclesCount = new List<LaneVehiclesCount>();

                    Console.WriteLine($"Step = {time} | Vehicles: {client.Vehicle.GetIdCount().Content} | {client.Vehicle.GetIdList().Content.Count()}");
                }
             
                client.Control.SimStep();
                

            } while (time <= steps);
            
            File.WriteAllText($"State.json", JsonConvert.SerializeObject(simulation), Encoding.UTF8);
                        
            client.Control.Close();
            client.Dispose();
        
            return simulation;
        }

        private static RoadsVehicleCount GetDetectedVehicles(TraCIClient client)
        {
            return new RoadsVehicleCount
            {
                NorthCount = client.LaneAreaDetector.GetLastStepVehicleNumber("e2_6").Content + client.LaneAreaDetector.GetLastStepVehicleNumber("e2_7").Content,
                SouthCount = client.LaneAreaDetector.GetLastStepVehicleNumber("e2_2").Content,
                WestCount = client.LaneAreaDetector.GetLastStepVehicleNumber("e2_0").Content + client.LaneAreaDetector.GetLastStepVehicleNumber("e2_1").Content,
                EastCount = client.LaneAreaDetector.GetLastStepVehicleNumber("e2_3").Content + client.LaneAreaDetector.GetLastStepVehicleNumber("e2_4").Content + client.LaneAreaDetector.GetLastStepVehicleNumber("e2_5").Content
            };
        }

        public class RoadsVehicleCount
        {
            public int NorthCount { get; set; }
            public int SouthCount { get; set; }
            public int WestCount { get; set; }
            public int EastCount { get; set; }

        }
        
        public Dictionary<string, string> streets = new Dictionary<string, string> {
            { "Ð\u009aÐ°Ð¼Ê¼Ñ\u008fÐ½ÐµÑ\u0086Ñ\u008cÐºÐ° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Камʼянецька вулиця" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð\u0093Ñ\u0080Ñ\u0083Ñ\u0088ÐµÐ²Ñ\u0081Ñ\u008cÐºÐ¾Ð³Ð¾", "вулиця Грушевського" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð¡Ð²Ñ\u008fÑ\u0082Ð¾Ñ\u0081Ð»Ð°Ð²Ð° Ð¥Ð¾Ñ\u0080Ð¾Ð±Ñ\u0080Ð¾Ð³Ð¾", "вулиця Святослава Хороброго" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð\u0093ÐµÑ\u0080Ð¾Ñ\u0097Ð² Ð\u009cÐ°Ñ\u0080Ñ\u0096Ñ\u0083Ð¿Ð¾Ð»Ñ\u008f", "вулиця Героїв Маріуполя" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð¡Ð²Ð¾Ð±Ð¾Ð´Ð¸", "вулиця Свободи" },
            { "Ð\u009fÑ\u0080Ð¸Ð±Ñ\u0083Ð·Ñ\u008cÐºÐ° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Прибузька вулиця" },
            { "Ð¡Ñ\u0082Ð°Ñ\u0080Ð¾ÐºÐ¾Ñ\u0081Ñ\u0082Ñ\u008fÐ½Ñ\u0082Ð¸Ð½Ñ\u0096Ð²Ñ\u0081Ñ\u008cÐºÐµ Ñ\u0088Ð¾Ñ\u0081Ðµ", "Старокостянтинівське шосе" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð\u009fÐ°Ð½Ð°Ñ\u0081Ð° Ð\u009cÐ¸Ñ\u0080Ð½Ð¾Ð³Ð¾", "вулиця Панаса Мирного" },
            { "Ð¿Ñ\u0080Ð¾Ñ\u0081Ð¿ÐµÐºÑ\u0082 Ð\u009cÐ¸Ñ\u0080Ñ\u0083", "проспект Миру" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð\u009fÑ\u0080Ð¾Ñ\u0081ÐºÑ\u0083Ñ\u0080Ñ\u0096Ð²Ñ\u0081Ñ\u008cÐºÐ¾Ð³Ð¾ Ð¿Ñ\u0096Ð´Ð¿Ñ\u0096Ð»Ð»Ñ\u008f", "вулиця Проскурівського підпілля" },
            { "Ð¢ÐµÑ\u0080Ð½Ð¾Ð¿Ñ\u0096Ð»Ñ\u008cÑ\u0081Ñ\u008cÐºÐ° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Тернопільська вулиця" },
            { "Ð\u0086Ð½Ñ\u0081Ñ\u0082Ð¸Ñ\u0082Ñ\u0083Ñ\u0082Ñ\u0081Ñ\u008cÐºÐ° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Інститутська вулиця" },
            { "Ð\u0097Ð°Ñ\u0080Ñ\u0096Ñ\u0087Ð°Ð½Ñ\u0081Ñ\u008cÐºÐ° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Зарічанська вулиця" },
            { "Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f Ð\u009cÐµÐ»Ñ\u008cÐ½Ð¸ÐºÐ¾Ð²Ð°", "вулиця Мельникова" },
            { "Ð\u0092Ð¾ÐºÐ·Ð°Ð»Ñ\u008cÐ½Ð° Ð²Ñ\u0083Ð»Ð¸Ñ\u0086Ñ\u008f", "Вокзальна вулиця" }
        };

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

    public class LaneVehiclesCount {
        public string laneId { get; set; }
        public int vehiclesCount { get; set; }
    }

    public class LaneVehicle { 
        public string vehId { get; set; }
        public string tlId { get; set; }
        public string laneId { get; set; }
    }
}
