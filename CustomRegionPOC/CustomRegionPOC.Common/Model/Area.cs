using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

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



        public string Name { get; set; }

        public string Tile { get; set; }

        public RecordType Type { get; set; }

        public string Guid { get; set; }

        public List<LocationPoint> Points { get; set; }

        public List<Tile> Tiles { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
