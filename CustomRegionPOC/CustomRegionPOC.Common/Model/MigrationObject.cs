using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_listing_region_v2")]
    public class MigrationObject
    {
        public string AreaID { get; set; }

        public string Name { get; set; }

        public string URLName{ get; set; }

        public string URLPath { get; set; }

        public string StateFIPS { get; set; }
        
        public string FIPS { get; set; }

        public string State { get; set; }

        public string USPSCity { get; set; }

        public string AreaTypeId { get; set; }

        public string SubTypeID { get; set; }

        public string OriginalPolygon { get; set; }

        public string OriginalPolygonArea { get; set; }

        public string Latitude { get; set; }

        public string Longitude { get; set; }

        public string North { get; set; }

        public string South { get; set; }

        public string East { get; set; }

        public string West { get; set; }

        public string TopLevelID { get; set; }

        public string SourceID { get; set; }


        public string SourceKey { get; set; }

        public string Status { get; set; }

        public string Guid { get; set; }

        public List<LocationPoint> Points { get; set; }

        public List<Tile> Tiles { get; set; }
    }
}
