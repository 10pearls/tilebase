using System;

namespace CustomRegionPOC.Common.Model
{
    public struct LocationPoint
    {
        public LocationPoint(decimal lat, decimal lng)
        {
            Lat = lat;
            Lng = lng;
        }

        public LocationPoint(double lat, double lng)
        {
            Lat = Convert.ToDecimal(lat);
            Lng = Convert.ToDecimal(lng);
        }

        public decimal Lat { get; set; }

        public decimal Lng { get; set; }
    }
}
