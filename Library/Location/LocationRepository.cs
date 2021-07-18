﻿using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library.Location
{
    public interface ILocationRepository
    {
        Task<List<LocationCityEntity>> GetCitiesAsync();
        Task<List<LocationCountryEntity>> GetCountriesAsync();
        Task<List<LocationStateEntity>> GetStatesAsync();
    }

    class LocationRepository : BaseRepository<LocationContext>, ILocationRepository
    {
        public LocationRepository(LocationContext context) : base(context) { }

        public async Task<List<LocationCountryEntity>> GetCountriesAsync() =>
            await _context.Countries
                .Include(country => country.Continent)
                .Include(country => country.SubContinent)
                .ToListAsync();

        public async Task<List<LocationCityEntity>> GetCitiesAsync() =>
            await _context.Cities
                .Include(city => city.State)
                .Include(city => city.Country)
                .ToListAsync();

        public async Task<List<LocationStateEntity>> GetStatesAsync() =>
            await _context.States.ToListAsync();
    }
}