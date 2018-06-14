using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    public class GetListingWrapper
    {
        public int PropertyCount { get; set; }
        public int ScanCount { get; set; }
        public double ConsumedCapacityCount { get; set; }
        public double TotalQueryExecutionTime { get; set; }
        public double TotalLambdaExecutionTime { get; set; }
        public double TotalContainsExecutionTime { get; set; }
        public List<ListingMaster> Properties { get; set; }
    }

    public class GetAreaListingWrapper : GetListingWrapper
    {
        public double TotalAreaQueryTime { get; set; }
        public List<AreaMaster> Area { get; set; }
    }

    public class GetRegionByPropertyWrapper : GetListingWrapper
    {
        public List<Property> PartialProperties { get; set; }
        public List<Property> CompleteProperties { get; set; }
        public int TotalRecordCount { get; set; }
        public double ConsumedCapacityCount { get; set; }
        public int ScanCount { get; set; }
    }
}
