using CustomRegionPOC.Common.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CustomRegionPOC.Common.Service
{
    public interface IRegionService
    {
        Task Create(Area region);

        Task<List<Area>> Get(decimal lat, decimal lng);

        Task SaveListing(Listing listing);

        Task<List<Listing>> GetListing(Area listing);

        Task<List<AreaListing>> GetAllAreas();
    }
}
