using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_area_listing_v2")]
    public class AreaListing : ICloneable
    {
        //area

        public string GUID { get;   set; }
        
        public string AreaID { get; set; }

        public string AreaName { get; set; }

        public List<LocationPoint> Points { get; set; }
        
        public string OriginalPolygon { get; set; }
        
        public object Clone()
        {
            return this.MemberwiseClone();
        }

    }
    
}
