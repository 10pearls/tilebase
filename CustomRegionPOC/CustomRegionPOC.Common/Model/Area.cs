using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_area_v2")]
    public class Area : ICloneable
    {
        //area

        public string AreaID { get; set; }

        public string AreaName { get; set; }

        public string URLName { get; set; }

        public string URLPath { get; set; }

        public string StateFIPS { get; set; }

        public string FIPS { get; set; }

        public string State { get; set; }

        public string USPSCity { get; set; }

        public string AreaTypeID { get; set; }

        public string SubTypeID { get; set; }

        public string OriginalPolygon { get; set; }

        public string OriginalPolygonArea { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string North { get; set; }

        public string South { get; set; }

        public string East { get; set; }

        public string West { get; set; }

        public string TopLeveeaID { get; set; }

        public string SourceID { get; set; }

        public string SourceKey { get; set; }

        public string AreaStatus { get; set; }

        public string EncodedPolygon { get; set; }



        public string Tile { get; set; }

        public RecordType Type { get; set; }

        public bool IsPartialTiles { get; set; }

        public List<LocationPoint> Points { get; set; }

        public List<Tile> Tiles { get; set; }

        public static Area ConvertToEntity(Dictionary<string, AttributeValue> item)
        {
            Area tempObj = new Area();
            Type type = tempObj.GetType();

            foreach (string attr in item.Keys)
            {
                if (attr == "Longitude")
                {
                    tempObj.Longitude = Convert.ToDecimal(item[attr].N);
                }
                else if (attr == "Latitude")
                {
                    tempObj.Latitude = Convert.ToDecimal(item[attr].N);
                }
                else if (attr == "Points")
                {
                    tempObj.Points = item[attr].L.Select(x => new LocationPoint() { Lat = Convert.ToDecimal(x.M["Lat"].N), Lng = Convert.ToDecimal(x.M["Lng"].N) }).ToList();
                }
                else
                {
                    PropertyInfo prop = type.GetProperty(attr);
                    prop.SetValue(tempObj, item[attr].S, null);
                }
            }

            return tempObj;
        }

        public static List<Area> ConvertToEntity(List<Dictionary<string, AttributeValue>> items)
        {
            List<Area> retItems = new List<Area>();

            Parallel.ForEach(items, currentItem => { retItems.Add(ConvertToEntity(currentItem)); });

            return retItems;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
