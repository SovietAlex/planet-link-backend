﻿using System.Text.Json.Serialization;

namespace Library.StockMarket.Contract
{
    public class StockMarketUserAlertTypeCountContract
    {
        [JsonIgnore]
        public int TypeId { get; internal set; }

        public int Count { get; internal set; }
        public decimal Points { get; internal set; }
        public StockMarketAlertTypeContract Type => IStockMarketMemoryCache.StockMarketAlertTypes[TypeId];
    }
}