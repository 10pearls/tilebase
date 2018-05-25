using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    [DynamoDBTable("tile_property_v2")]
    public class Property : ICloneable
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

        public string AreaID { get; set; }

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

        public static Property ConvertToEntity(Dictionary<string, AttributeValue> item)
        {
            Property tempObj = new Property();
            Type type = tempObj.GetType();

            foreach (string attr in item.Keys)
            {
                if (attr == "Longitude")
                {
                    tempObj.Longitude = Convert.ToDecimal(item[attr].N);
                }
                else if (attr == "Latitude")
                {
                    tempObj.Longitude = Convert.ToDecimal(item[attr].N);
                }
                else
                {
                    PropertyInfo prop = type.GetProperty(attr);
                    prop.SetValue(tempObj, item[attr].S, null);
                }
            }

            return tempObj;
        }

        public static List<Property> ConvertToEntity(List<Dictionary<string, AttributeValue>> item)
        {
            List<Property> listings = new List<Property>();

            foreach (Dictionary<string, AttributeValue> currentItem in item)
            {
                listings.Add(ConvertToEntity(currentItem));
            }
            return listings;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
