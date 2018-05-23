using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_listing_region_shahbaz")]
    public class Region
    {
        public string Name { get; set; }

        public string Tile { get; set; }

        public RecordType Type { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string LocationPoints { get; set; }

        public DateTime CreateDate { get; set; }

        public List<LocationPoint> Points { get; set; }

        public List<Tile> Tiles { get; set; }

        public Boolean IsPartial {get; set;} //whether the region passes partially or completely through the region
    }

    public enum RecordType
    {
        Region = 1,
        Listing = 2,
    }
}
