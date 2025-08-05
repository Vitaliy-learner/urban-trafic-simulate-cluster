using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

namespace SumoScenarios
{
    public class SumoResultReader
    {
        public int Process(string dataFolder = null)
        {
            XmlDocument tripsDoc = new XmlDocument();
            tripsDoc.Load(dataFolder + "osm.khm.taz.trips.xml");

            XmlDocument rndTripsDoc = new XmlDocument();
            rndTripsDoc.Load("osm.khm.random.trips.xml");

            XmlNodeList trips = tripsDoc.GetElementsByTagName("trip");
            XmlNodeList rndTrips = rndTripsDoc.GetElementsByTagName("trip");

            var carInfos = trips.Cast<XmlNode>().Select(x => new
            {
                id = x.Attributes?.GetNamedItem("id")?.Value,
                from = x.Attributes?.GetNamedItem("fromTaz")?.Value,
                to = x.Attributes?.GetNamedItem("toTaz")?.Value
            }).ToList();

            carInfos.AddRange(rndTrips.Cast<XmlNode>().Select(x => new
            {
                id = x.Attributes?.GetNamedItem("id")?.Value,
                from = x.Attributes?.GetNamedItem("from")?.Value,
                to = x.Attributes?.GetNamedItem("to")?.Value
            }).ToList());

            XmlDocument resInfoDoc = TryLoadTripInfo(dataFolder + "osm.khm.output.tripinfo.xml");

            XmlNodeList tripInfos = resInfoDoc.GetElementsByTagName("tripinfo");
            File.Copy("osm.khm.output.tripinfo.xml", "osm.khm.output.tripinfo.sumolauncher.xml", true);
            var tripDurations = tripInfos.Cast<XmlNode>()              
                .Select(x =>
                {
                    var emissions = x.FirstChild;
                    double.TryParse(emissions?.Attributes?.GetNamedItem("CO2_abs")?.Value, out double CO2_abs);
                    double.TryParse(x.Attributes?.GetNamedItem("duration")?.Value, out var duration);
                    return new
                    {
                        id = x.Attributes?.GetNamedItem("id")?.Value,
                        duration,
                        CO2_abs,
                    };
                }).ToList();

            var result = carInfos.Select(x => new
            {
                x.id,
                x.from,
                x.to,
                duration = tripDurations.FirstOrDefault(t => t.id == x.id)?.duration ?? 0,
                CO2_abs = tripDurations.FirstOrDefault(t => t.id == x.id)?.CO2_abs ?? 0,
            }).OrderBy(x => x.id, new StringNaturalComparer()).ToList();

            return -(int)result.Average(x => x.CO2_abs);
        }

        private XmlDocument TryLoadTripInfo(string filePath)
        {
            XmlDocument resInfoDoc = new XmlDocument();

            bool opened = false;
            while (!opened)
            {
                try
                {
                    resInfoDoc.Load(filePath);
                    opened = true;
                }
                catch (Exception ex)
                {
                    Thread.Sleep(100);
                }
            }

            return resInfoDoc;
        }
    }
}
