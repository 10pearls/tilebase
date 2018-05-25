using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_area_listing_v2")]
    public class AreaListing : ICloneable
    {
        //area

        public string GUID { get; set; }

        public string AreaID { get; set; }

        public string AreaName { get; set; }

        public List<LocationPoint> Points { get; set; }

        public string OriginalPolygon { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public static AreaListing ConvertToEntity(Dictionary<string, AttributeValue> item)
        {
            AreaListing tempObj = new AreaListing();
            Type type = tempObj.GetType();

            foreach (string attr in item.Keys)
            {
                if (attr == "Points")
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

        public static List<AreaListing> ConvertToEntity(List<Dictionary<string, AttributeValue>> item)
        {
            List<AreaListing> listings = new List<AreaListing>();

            foreach (Dictionary<string, AttributeValue> currentItem in item)
            {
                listings.Add(ConvertToEntity(currentItem));
            }
            return listings;
        }

    }

}
