﻿using Library.Location;
using Library.User;
using Microsoft.Extensions.Caching.Memory;
using NodaTime;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.Weather
{
    public interface IWeatherService
    {
        Task<WeatherCityUserEmotionContract> CreateCityUserEmotionAsync(int userId, int cityId, int emotionId, DateTimeZone timezone);
        List<WeatherCityEmotionCountContract> GetCityEmotionCounts(int cityId, DateTimeZone timezone);
        Task<List<WeatherCityForecastContract>> GetCityForecastsAsync(int cityId);
        Task<WeatherCityObservationContract> GetCityObservationAsync(int cityId);
        WeatherCityUserConfigurationContract GetCityUserConfiguration(int userId, int cityId, DateTimeZone timezone);
        WeatherConfigurationContract GetConfiguration();
        WeatherEmotionContract GetEmotion(int emotionId);
    }

    class WeatherService : BaseService<WeatherConfiguration, WeatherRepository>, IWeatherService
    {
        public WeatherService(WeatherConfiguration configuration, WeatherRepository repository, ILocationService locationService, IUserService userService, IMemoryCache cache) : base(configuration, repository, cache)
        {
            _locationService = locationService;
            _userService = userService;
            _openWeatherApi = new RestClient(_configuration.OpenWeatherApi.Server);
        }

        private readonly ILocationService _locationService;
        private readonly IUserService _userService;
        private readonly IRestClient _openWeatherApi;

        #region Get

        public WeatherEmotionContract GetEmotion(int emotionId) =>
            WeatherMemoryCache.WeatherEmotions.TryGetValue(emotionId, out WeatherEmotionContract? emotion)
                ? emotion
                : throw new BadRequestException($"{nameof(emotionId)} is invalid");

        public async Task<WeatherCityObservationContract> GetCityObservationAsync(int cityId)
        {
            var memoryCacheKey = $"{{Weather}}.{{WeatherCityObservationContract}}.{{CityId}}={cityId}";

            if (_memoryCache.TryGetValue(memoryCacheKey, out WeatherCityObservationContract cityObservation))
                return cityObservation;

            var city = _locationService.GetCity(cityId);

            return _memoryCache.Set(
                memoryCacheKey,
                (await GetCityObservationResponseAsync(city.OpenWeatherId))
                    .MapToCityObservationContract(),
                TimeSpan.FromSeconds(_configuration.Duration.CityObservationCacheDurationInSeconds)
            );
        }

        public async Task<List<WeatherCityForecastContract>> GetCityForecastsAsync(int cityId)
        {
            var memoryCacheKey = $"{{Weather}}.{{List<WeatherCityForecastContract>}}.{{CityId}}={cityId}";

            if (_memoryCache.TryGetValue(memoryCacheKey, out List<WeatherCityForecastContract> cityForecasts))
                return cityForecasts;

            var city = _locationService.GetCity(cityId);

            return _memoryCache.Set(
                memoryCacheKey,
                (await GetCityForecastsResponseAsync(city.OpenWeatherId))
                    .MapToCityForecastContracts(),
                TimeSpan.FromSeconds(_configuration.Duration.CityForecastsCacheDurationInSeconds)
            );
        }

        public List<WeatherCityEmotionCountContract> GetCityEmotionCounts(int cityId, DateTimeZone timezone)
        {
            var city = _locationService.GetCity(cityId);

            return WeatherMemoryCache.WeatherCityUserEmotions.GetCityUserEmotionsAtTimezoneToday(timezone)
                .GroupBy(cityUserEmotion => cityUserEmotion.EmotionId)
                .Select(cityUserEmotionGroup => new WeatherCityEmotionCountContract()
                {
                    EmotionId = cityUserEmotionGroup.Key,
                    CityCount = cityUserEmotionGroup.Where(cityUserEmotion => cityUserEmotion.CityId == city.CityId).Count(),
                    GlobalCount = cityUserEmotionGroup.Count()
                })
                .ToList();
        }

        public WeatherCityUserConfigurationContract GetCityUserConfiguration(int userId, int cityId, DateTimeZone timezone)
        {
            var user = _userService.GetUser(userId);
            var city = _locationService.GetCity(cityId);

            var cityUserEmotions = WeatherMemoryCache.WeatherCityUserEmotions.GetCityUserEmotionsAtTimezoneToday(timezone, userId);

            return new WeatherCityUserConfigurationContract()
            {
                EmotionId = cityUserEmotions.SingleOrDefault(cityUserEmotion => cityUserEmotion.CityId == city.CityId)?.EmotionId,
                SelectionsToday = cityUserEmotions.Count,
                LimitToday = _configuration.Limit.CreateCityUserEmotionLimit
            };
        }

        public WeatherConfigurationContract GetConfiguration() => new()
        {
            Emotions = WeatherMemoryCache.WeatherEmotions.Select(emotion => emotion.Value).ToList()
        };

        #endregion

        #region Create

        public async Task<WeatherCityUserEmotionContract> CreateCityUserEmotionAsync(int userId, int cityId, int emotionId, DateTimeZone timezone)
        {
            var user = _userService.GetUser(userId);
            var city = _locationService.GetCity(cityId);
            var emotion = GetEmotion(emotionId);

            var cityUserEmotions = WeatherMemoryCache.WeatherCityUserEmotions.GetCityUserEmotionsAtTimezoneToday(timezone, userId);

            if (cityUserEmotions.Any(cityUserEmotion => cityUserEmotion.CityId == city.CityId))
                throw new BadRequestException("You already selected an emotion");

            if (cityUserEmotions.Count >= _configuration.Limit.CreateCityUserEmotionLimit)
                throw new BadRequestException("You have reached your daily limit. Come back tomorrow!");

            var cityUserEmotion = (await _repository.AddAndSaveChangesAsync(new WeatherCityUserEmotionEntity()
            {
                CityId = city.CityId,
                UserId = user.UserId,
                EmotionId = emotion.EmotionId,
                CreatedOn = DateTimeOffset.Now.AtTimezone(timezone)
            })).MapToCityUserEmotionContract();

            WeatherMemoryCache.WeatherCityUserEmotions.TryAdd(cityUserEmotion.CityUserEmotionId, cityUserEmotion);

            return cityUserEmotion;
        }

        #endregion

        #region Open Weather Api

        private async Task<WeatherCityObservationResponse> GetCityObservationResponseAsync(int openWeatherId) =>
            (await _openWeatherApi.ExecuteGetAsync<WeatherCityObservationResponse>(
                new RestRequest("data/2.5/weather")
                    .AddQueryParameter("APPID", _configuration.OpenWeatherApi.AuthenticationKey)
                    .AddQueryParameter("units", "imperial")
                    .AddParameter("id", openWeatherId)
            )).GetData(isSuccess: (response) =>
            {
                if (response is null || response.Conditions is null || response.Temperature is null || response.City is null)
                    return false;

                if (response.Wind is null)
                    response.Wind = new WeatherWindResponse()
                    {
                        Degrees = 0,
                        Speed = 0
                    };

                if (response.Cloud is null)
                    response.Cloud = new WeatherCloudResponse()
                    {
                        Cloudiness = 0
                    };

                if (response.Rain is null)
                    response.Rain = new WeatherRainResponse()
                    {
                        OneHourVolume = 0,
                        ThreeHourVolume = 0
                    };

                if (response.Snow is null)
                    response.Snow = new WeatherSnowResponse()
                    {
                        OneHourVolume = 0,
                        ThreeHourVolume = 0
                    };

                return true;
            });

        private async Task<WeatherForecastResponse> GetCityForecastsResponseAsync(int openWeatherId) =>
            (await _openWeatherApi.ExecuteGetAsync<WeatherForecastResponse>(
                new RestRequest("data/2.5/forecast")
                    .AddQueryParameter("APPID", _configuration.OpenWeatherApi.AuthenticationKey)
                    .AddQueryParameter("units", "imperial")
                    .AddParameter("id", openWeatherId)
            )).GetData(isSuccess: (response) =>
            {
                if (response is null || response.City is null || response.Forecasts is null || response.Forecasts.Any(forecast => forecast.Temperature is null || forecast.Conditions is null))
                    return false;

                response.Forecasts.ForEach(forecast =>
                {
                    if (forecast.Cloud is null)
                        forecast.Cloud = new WeatherCloudResponse()
                        {
                            Cloudiness = 0
                        };

                    if (forecast.Wind is null)
                        forecast.Wind = new WeatherWindResponse()
                        {
                            Degrees = 0,
                            Speed = 0
                        };

                    if (forecast.Rain is null)
                        forecast.Rain = new WeatherRainResponse()
                        {
                            OneHourVolume = 0,
                            ThreeHourVolume = 0
                        };

                    if (forecast.Snow is null)
                        forecast.Snow = new WeatherSnowResponse()
                        {
                            OneHourVolume = 0,
                            ThreeHourVolume = 0
                        };
                });

                return true;
            });

        #endregion
    }
}