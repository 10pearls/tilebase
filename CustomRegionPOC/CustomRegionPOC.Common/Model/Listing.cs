using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    public class ListingMaster
    {
        public decimal Lat { get; set; }

        public decimal Lng { get; set; }

        public string Name { get; set; }
    }

    public class Listing : ListingMaster
    {
        public string Beds { get; set; }

        public string BathsFull { get; set; }

        public string BathsHalf { get; set; }

        public string PropertyAddressId { get; set; }

        public string AverageValue { get; set; }

        public string AverageRent { get; set; }
    }
}