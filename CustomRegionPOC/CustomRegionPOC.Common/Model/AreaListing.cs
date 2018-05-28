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
    [DynamoDBTable("tile_area_master_v2")]
    public class AreaMaster : ICloneable
    {
        //area

        public string GUID { get; set; }

        public string AreaID { get; set; }

        public string AreaName { get; set; }

        public List<LocationPoint> Points { get; set; }

        public string OriginalPolygon { get; set; }

        public bool IsPredefine { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public static AreaMaster ConvertToEntity(Dictionary<string, AttributeValue> item)
        {
            AreaMaster tempObj = new AreaMaster();
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

        public static List<AreaMaster> ConvertToEntity(List<Dictionary<string, AttributeValue>> items)
        {
            List<AreaMaster> retItems = new List<AreaMaster>();

            Parallel.ForEach(items, currentItem => { retItems.Add(ConvertToEntity(currentItem)); });

            return retItems;
        }

    }

}
