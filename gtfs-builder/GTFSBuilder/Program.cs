using GTFS.Entities;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTFSBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var geoJsonConvertor = new NetTopologySuite.IO.GeoJsonSerializer();
            FeatureCollection features = null;
            using (var stream = new JsonTextReader(File.OpenText(args[0])))
            {
                features = geoJsonConvertor.Deserialize<FeatureCollection>(stream);
            }

            var stopPoints = new List<IFeature>(features.Features.Where(x => x.Geometry is Point));
            var lineGeometry = features.Features.Where(x => x.Geometry is LineString).First();

            var feed = new GTFS.GTFSFeed();
            feed.Agencies.Add(new GTFS.Entities.Agency()
            {
                Id = "DL",
                Name = "De Lijn"
            });
            
            var stops = new List<Stop>();
            foreach(var stopPoint in stopPoints)
            {
                var stop_id = string.Empty;
                var stop_name = string.Empty;
                object value;
                if (stopPoint.Attributes.TryGetValue("stop_id", out value))
                {
                    stop_id = value.ToString();
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
            //stops.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
            foreach (var stop in stops)
            {
                feed.Stops.Add(stop);
            }



            //var point = NetTopologySuite.Operation.Distance.DistanceOp.NearestPoints(lineGeometry.Geometry, stopPoints[0].Geometry)[0];
        }
    }
}
