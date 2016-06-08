using GTFS;
using GTFS.Entities;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTFSBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var feed = new GTFS.GTFSFeed();
            feed.Agencies.Add(new GTFS.Entities.Agency()
            {
                Id = "DL",
                Name = "De Lijn"
            });

            var directoryInfo = new DirectoryInfo(args[0]);
            foreach(var file in directoryInfo.GetFiles("*.geojson"))
            {
                AddFile(feed, file.FullName);
            }

            var feedWriter = new GTFS.GTFSWriter<GTFS.GTFSFeed>();
            var directoryTarget = new GTFS.IO.GTFSDirectoryTarget(new DirectoryInfo(args[1]));
            feedWriter.Write(feed, directoryTarget);

            //var point = NetTopologySuite.Operation.Distance.DistanceOp.NearestPoints(lineGeometry.Geometry, stopPoints[0].Geometry)[0];
        }

        /// <summary>
        /// Adds a new route from a geosjon file.
        /// </summary>
        static void AddFile(GTFSFeed feed, string geoJsonFile)
        {
            var geoJsonConvertor = new NetTopologySuite.IO.GeoJsonSerializer();
            FeatureCollection features = null;
            using (var stream = new JsonTextReader(File.OpenText(geoJsonFile)))
            {
                features = geoJsonConvertor.Deserialize<FeatureCollection>(stream);
            }

            var stopPoints = new List<IFeature>(features.Features.Where(x => x.Geometry is Point));
            var lineGeometry = features.Features.Where(x => x.Geometry is LineString).First();

            // build route.
            var route = new Route();
            object value;
            if (lineGeometry.Attributes.TryGetValue("route_short_name", out value))
            {
                route.ShortName = value.ToString();
            }
            if (lineGeometry.Attributes.TryGetValue("route_long_name", out value))
            {
                route.LongName = value.ToString();
            }
            if (lineGeometry.Attributes.TryGetValue("route_desc", out value))
            {
                route.Description = value.ToString();
            }
            if (lineGeometry.Attributes.TryGetValue("route_id", out value))
            {
                route.Id = value.ToString();
            }
            if (lineGeometry.Attributes.TryGetValue("stroke", out value))
            {
                route.Color = value.ToString().ToArgbInt();
            }
            if (lineGeometry.Attributes.TryGetValue("route_color", out value))
            {
                route.Color = value.ToString().ToArgbInt();
            }
            route.Type = GTFS.Entities.Enumerations.RouteTypeExtended.TramService;
            route.AgencyId = feed.Agencies.First().Id;
            feed.Routes.Add(route);

            // build stops.
            var stops = new List<Stop>();
            foreach (var stopPoint in stopPoints)
            {
                var stop_id = string.Empty;
                var stop_name = string.Empty;
                if (stopPoint.Attributes.TryGetValue("stop_id", out value))
                {
                    var id = int.Parse(value.ToString());
                    stop_id = route.Id + id.ToString("D3");
                }
                if (stopPoint.Attributes.TryGetValue("stop_name", out value))
                {
                    stop_name = value.ToString();
                }
                var point = stopPoint.Geometry as Point;
                stops.Add(new GTFS.Entities.Stop()
                {
                    Id = stop_id,
                    Code = stop_id,
                    Name = stop_name,
                    Latitude = point.Coordinate.Y,
                    Longitude = point.Coordinate.X,
                    LocationType = GTFS.Entities.Enumerations.LocationType.Stop
                });
            }
            stops.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
            foreach (var stop in stops)
            {
                feed.Stops.Add(stop);
            }

            // build calendar 01/01/2016 -> 31/12/2016
            for (var d = new DateTime(2016, 1, 1); d <= new DateTime(2016, 12, 31); d = d.AddDays(1))
            {
                feed.CalendarDates.Add(new CalendarDate()
                {
                    ServiceId = route.Id,
                    Date = d,
                    ExceptionType = GTFS.Entities.Enumerations.ExceptionType.Added
                });
            }

            // build forward trips.
            // 06:00 -> 22:00 every 15 mins
            for (var t = 6 * 60; t <= 22 * 60; t += 15)
            {
                var trip = new Trip();
                trip.Id = route.Id + "_" + string.Format("F_{0}", t.ToString());
                trip.AccessibilityType = GTFS.Entities.Enumerations.WheelchairAccessibilityType.NoInformation;
                trip.Headsign = route.ShortName;
                trip.RouteId = route.Id;
                trip.ShortName = route.ShortName;
                trip.ServiceId = route.Id;
                feed.Trips.Add(trip);
                var localTime = t;
                Stop previous = null;
                for (var s = 0; s < stops.Count; s++)
                {
                    var stop = stops[s];

                    if (previous != null)
                    {
                        var distance = Itinero.LocalGeo.Coordinate.DistanceEstimateInMeter(
                            (float)previous.Latitude, (float)previous.Longitude, (float)stop.Latitude, (float)stop.Longitude);
                        var time = System.Math.Ceiling((distance / 1000) / (60) * 60); // 60km/h.
                        time += 2; // 2 mins extra.

                        localTime += (int)time;
                    }

                    var stopTime = new StopTime();
                    stopTime.StopId = stop.Id;
                    stopTime.StopSequence = (uint)s + 1;
                    stopTime.PickupType = GTFS.Entities.Enumerations.PickupType.Regular;
                    stopTime.TripId = trip.Id;
                    stopTime.ArrivalTime = TimeOfDay.FromTotalSeconds(localTime * 60);
                    stopTime.DepartureTime = stopTime.ArrivalTime;

                    previous = stop;

                    feed.StopTimes.Add(stopTime);
                }
            }

            // build backward trips.
            // 06:00 -> 22:00 every 15 mins
            for (var t = 6 * 60; t <= 22 * 60; t += 15)
            {
                var trip = new Trip();
                trip.Id = route.Id + "_" + string.Format("B_{0}", t.ToString());
                trip.AccessibilityType = GTFS.Entities.Enumerations.WheelchairAccessibilityType.NoInformation;
                trip.Headsign = route.ShortName;
                trip.RouteId = route.Id;
                trip.ShortName = route.ShortName;
                trip.ServiceId = route.Id;
                feed.Trips.Add(trip);
                var localTime = t;
                Stop previous = null;
                for (var s = stops.Count - 1; s >= 0; s--)
                {
                    var stop = stops[s];

                    if (previous != null)
                    {
                        var distance = Itinero.LocalGeo.Coordinate.DistanceEstimateInMeter(
                            (float)previous.Latitude, (float)previous.Longitude, (float)stop.Latitude, (float)stop.Longitude);
                        var time = System.Math.Ceiling((distance / 1000) / (60) * 60); // 60km/h.
                        time += 2; // 2 mins extra.

                        localTime += (int)time;
                    }

                    var stopTime = new StopTime();
                    stopTime.StopId = stop.Id;
                    stopTime.StopSequence = (uint)s + 1;
                    stopTime.PickupType = GTFS.Entities.Enumerations.PickupType.Regular;
                    stopTime.TripId = trip.Id;
                    stopTime.ArrivalTime = TimeOfDay.FromTotalSeconds(localTime * 60);
                    stopTime.DepartureTime = stopTime.ArrivalTime;

                    previous = stop;

                    feed.StopTimes.Add(stopTime);
                }
            }
        }
    }
}
