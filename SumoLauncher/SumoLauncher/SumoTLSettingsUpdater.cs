using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace SumoScenarios
{
    public class SumoTLSettingsUpdater
    {
        public static readonly Dictionary<string, int> _trafficLights = new Dictionary<string, int>()
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

        public void Process(List<string> args)
        {
            var settings = PrepareArgs(args);

            XmlDocument tripsDoc = new XmlDocument();
            tripsDoc.Load("osm.khm.net.xml");

            XmlNodeList trafficLights = tripsDoc.GetElementsByTagName("tlLogic");
            var actualTrafficLights = trafficLights.Cast<XmlNode>().Where(x => settings.Keys.Contains(x?.Attributes?.GetNamedItem("id")?.Value ?? ""));
            foreach (XmlNode node in actualTrafficLights)
            {
                int i = 0;
                var phases = settings.FirstOrDefault(x => x.Key == node.Attributes?.GetNamedItem("id")?.Value).Value;
                foreach (XmlNode childNode in node.ChildNodes) 
                {
                    childNode.Attributes.GetNamedItem("duration").Value = phases[i++].ToString();
                }
            }

            tripsDoc.Save("osm.khm.net.xml");
        }

        private Dictionary<string, List<double>> PrepareArgs(List<string> args)
        {
            if (_trafficLights.Values.Sum() / 2 != args.Count)
                throw new Exception("args count mismatch");

            Dictionary<string, List<double>> settings = new Dictionary<string, List<double>>();
            int skip = 0;
            foreach (var tl in _trafficLights)
            {
                List<double> phases = new List<double>();
                for (int i = 0; i < tl.Value; i++)
                {
                    if (i % 2 == 0)
                        phases.Add(double.Parse(args[skip + i / 2]));
                    else 
                        phases.Add(2);
                }
                settings.Add(tl.Key, phases);
                skip += phases.Count / 2;
            }

            return settings;
        }
    }
}
