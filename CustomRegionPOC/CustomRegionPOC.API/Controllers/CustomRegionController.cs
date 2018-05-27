using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomRegionPOC.Common.Model;
using CustomRegionPOC.Common.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CustomRegionPOC.API.Controllers
{
    [Produces("application/json")]
    [Route("api/CustomRegion")]
    public class CustomRegionController : Controller
    {
        private IRegionService service;

        public CustomRegionController(IRegionService service)
        {
            this.service = service;
        }

        [HttpGet("{lat}/{lng}", Name = "Get")]
        public async Task<List<Area>> Get(decimal lat, decimal lng)
        {
            return await this.service.Get(lat, lng);
        }

        [HttpPost]
        public async Task<List<Listing>> Post([FromBody]Area area)
        {
            List<Listing> listings = new List<Listing>();
            List<Task> tasks = new List<Task>();


            tasks.Add(Task.Factory.StartNew(() =>
            {
                this.service.Create(area).Wait();
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                listings = this.service.GetListing(area).Result;
            }));

            Task.WaitAll(tasks.ToArray());

            return listings;
        }

        [HttpPost]
        [Route("SaveListing")]
        public async Task SaveListing([FromBody]Listing listing)
        {
            await this.service.SaveListing(listing);
        }

        [HttpPost]
        [Route("GetListings")]
        public async Task<List<Listing>> GetListings([FromBody]Area area)
        {
            return await this.service.GetListing(area);
        }

        [HttpGet]
        [Route("GetArea")]
        public async Task<List<AreaListing>> GetAllAreas()
        {
            return await this.service.GetArea();
        }

        [HttpGet]
        [Route("GetArea/{id}")]
        public async Task<dynamic> GetArea(string id)
        {
            string beds = Request.Query["beds"];
            string bathsFull = Request.Query["bathsFull"];
            string bathsHalf = Request.Query["bathsHalf"];
            string propertyAddressId = Request.Query["propertyAddressId"];
            string averageValue = Request.Query["averageValue"];
            string averageRent = Request.Query["averageRent"];

            return await this.service.GetArea(id, beds, bathsFull, bathsHalf, propertyAddressId, averageValue, averageRent);
        }
    }
}
