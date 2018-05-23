using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_property_v2")]
    public class PropertyMigrationObject: ICloneable
    {
        //properties

        public string PropertyID { get; set; }

        public string PropertyAddressID { get; set; }

        public string APN { get; set; }

        public string UnitNumber { get; set; }

        public string UnitNumberLegal { get; set; }

        public string UnitType { get; set; }

        public string BathsFull { get; set; }

        public string BathsHalf { get; set; }

        public string Beds { get; set; }

        public string SqFt { get; set; }

        public string YearBuilt { get; set; }

        public string Rooms { get; set; }

        public string Units { get; set; }

        public string Buildings { get; set; }

        public string PropertyStories { get; set; }

        public string ParkingSpaces { get; set; }

        public string TaxRate { get; set; }

        public string LastSaleDate { get; set; }

        public string LastSalePrice { get; set; }

        public string AssessedValueLand { get; set; }

        public string AssessedValueImprovements { get; set; }

        public string StoriesType { get; set; }

        public string PropertyType { get; set; }

        public string Construction { get; set; }

        public string ParkingType { get; set; }

        public string Pool { get; set; }

        public string Style { get; set; }

        public string Exterior { get; set; }

        public string Foundation { get; set; }

        public string Roof { get; set; }

        public string Heating { get; set; }

        public string AC { get; set; }

        public string Elevator { get; set; }

        public string Fireplaces { get; set; }

        public string Basement { get; set; }

        public string PrimaryListingInstanceID { get; set; }

        public string PropertyStatus { get; set; }




        //property addresses

        public string StreetNumber { get; set; }

        public string StreetDirPrefixID { get; set; }

        public string StreetNameID { get; set; }

        public string StreetDirSuffixID { get; set; }

        public string StreetSuffixID { get; set; }

        public string CityID { get; set; }

        public string Zip { get; set; }

        public string CountyID { get; set; }

        public string PropertyAddressLatitude { get; set; }

        public string PropertyAddressLongitude { get; set; }

        public string PixelX { get; set; }

        public string PixelY { get; set; }

        public string LotSize { get; set; }

        public string PropertyAddressName { get; set; }

        public string PropertyAddressStories { get; set; }

        public string PropertyAddressStatus { get; set; }

        public string propertyAddressCount { get; set; }

        public string YearBuiltMin { get; set; }

        public string YearBuiltMax { get; set; }

        public string AverageValue { get; set; }

        public string AverageValueLow { get; set; }

        public string AverageValueHigh { get; set; }

        public string AverageRent { get; set; }

        public string AverageSqFt { get; set; }

        public string AverageValuePerSqFt { get; set; }

        public string DefaultParentAreaID { get; set; }

        public string Url { get; set; }

        public string FullStreetAddress { get; set; }



        public string Name { get; set; }

        public string Tile { get; set; }

        public RecordType Type { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string Guid { get; set; }

        public List<LocationPoint> Points { get; set; }

        public List<Tile> Tiles { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    [DynamoDBTable("tile_area_v2")]
    public class AreaMigrationObject: ICloneable
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

        public string AreaLatitude { get; set; }

        public string AreaLongitude { get; set; }

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
