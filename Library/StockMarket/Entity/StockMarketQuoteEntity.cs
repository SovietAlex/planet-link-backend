﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Collections.Generic;

namespace Library.StockMarket.Entity
{
    public class StockMarketQuoteEntity
    {
        public int QuoteId { get; internal set; }
        public int ExchangeId { get; internal set; }
        public string Name { get; internal set; } = default!;
        public string Symbol { get; internal set; } = default!;

        public virtual StockMarketExchangeEntity Exchange { get; internal set; } = default!;
        public virtual ICollection<StockMarketQuoteUserAlertEntity> UserAlerts { get; internal set; } = new HashSet<StockMarketQuoteUserAlertEntity>();
    }
}

namespace Library.StockMarket.Entity.Configuration
{
    class StockMarketQuoteEntityConfiguration : IEntityTypeConfiguration<StockMarketQuoteEntity>
    {
        public void Configure(EntityTypeBuilder<StockMarketQuoteEntity> entity)
        {
            entity.ToTable(nameof(StockMarketContext.StockMarketQuotes));
            entity.HasKey(quote => quote.QuoteId);

            entity.HasOne(quote => quote.Exchange).WithMany(exchange => exchange.Quotes).IsRequired(true);

            entity.HasMany(quote => quote.UserAlerts).WithOne(quoteUserAlert => quoteUserAlert.Quote);
        }
    }
}