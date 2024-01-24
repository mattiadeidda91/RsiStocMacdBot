using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class RSI_Stoc_MACD : Robot
    {
        [Parameter(DefaultValue = "Hello world!")]
        public string Message { get; set; }

        [Parameter("Volume (Lots)", DefaultValue = 0.01, Group = "Trade")]
        public double volumeInLots { get; set; }

        [Parameter("Stop Loss (Pips)", DefaultValue = 10, Group = "Trade")]
        public double stopLossInPips { get; set; }

        [Parameter("Take Profit (Pips)", DefaultValue = 10, Group = "Trade")]
        public double takeProfitInPips { get; set; }

        private MovingAverage _ma;
        private MovingAverage _ema;

        private RelativeStrengthIndex rsi;
        private StochasticOscillator stoc;
        private MacdCrossOver macd;

        private double old_rsi_value = 0;
        private double rsi_value = 0;
        private double stoc_main = 0;
        private double stoc_signal = 0;
        private double mac_main = 0;
        private double mac_signal = 0;

        private bool alreadyCrossTrade = false;
        Trend trend = Trend.None;

        private double _volumeInUnits;
        public Position[] BotPositions
        {
            get
            {
                return Positions.FindAll("Trade");
            }
        }

        protected override void OnStart()
        {
            _volumeInUnits = Symbol.QuantityToVolumeInUnits(volumeInLots);

            _ma = Indicators.SimpleMovingAverage(Bars.ClosePrices, 50);
            _ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);

            rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
            stoc = Indicators.StochasticOscillator(14, 1, 3, MovingAverageType.Simple);
            macd = Indicators.MacdCrossOver(Bars.ClosePrices, 12, 26, 9);
        }

        protected override void OnBar()
        {
            rsi_value = rsi.Result.Last(1);

            stoc_main = stoc.PercentD.Last(1);
            stoc_signal = stoc.PercentK.Last(1);

            mac_main = macd.MACD.Last(1);
            mac_signal = macd.Signal.Last(1);

            trend = TrendMA();

            bool isCrossingRSI = GetRSICrossing();
            old_rsi_value= rsi_value;

            if (!Positions.FindAll("Trade").Any())
            {
                //Trade
                if (isCrossingRSI)
                {
                    Trade(0, null);
                }
            }
        }

        protected override void OnStop()
        {
            ClosePositions(TradeType.Buy);
            ClosePositions(TradeType.Sell);
        }

        private bool GetRSICrossing()
        {
            if(rsi_value < 50 && old_rsi_value < 50)
            {
                if(old_rsi_value > 45)
                    return false;

                if (old_rsi_value <= 45 && rsi_value < old_rsi_value && !alreadyCrossTrade)
                    return true;
            }

            if(rsi_value > 50 && old_rsi_value > 50)
            {
                if(old_rsi_value <55)
                    return false;
                if(old_rsi_value >=55 && rsi_value > old_rsi_value && !alreadyCrossTrade)
                    return true;
            }

            if(rsi_value <50 && old_rsi_value > 50)
            {
                alreadyCrossTrade = false;
                return true;
            }
            if(rsi_value > 50 && old_rsi_value < 50)
            {
                alreadyCrossTrade = false;
                return true;
            }

            return false;
        }

        private void Trade(int i, TradeSignal? signal)
        {
            var signalTrade = signal == null ? IsSignal() : signal;

            if (signalTrade != TradeSignal.None)
            {
                if(signalTrade.Value == TradeSignal.Sell && trend == Trend.Bearish)
                {
                    ClosePositions(TradeType.Buy);
                    ExecuteMarketOrder(TradeType.Sell, SymbolName, _volumeInUnits, "Trade", stopLossInPips, takeProfitInPips);
                }

                if(signalTrade.Value == TradeSignal.Buy && trend == Trend.Bullish)
                {
                    ClosePositions(TradeType.Sell);
                    ExecuteMarketOrder(TradeType.Buy, SymbolName, _volumeInUnits, "Trade", stopLossInPips, takeProfitInPips);
                }
            }
        }
        private Trend TrendMA()
        {
            var currentPrice = Bars.ClosePrices.Last(1);
            var maValue = _ma.Result.Last(1);

            if (currentPrice > maValue)
               return Trend.Bullish;
            else if (currentPrice < maValue)
                return Trend.Bearish;
            else
                return Trend.Lateral;
        }


        private TradeSignal IsSignal()
        {
            //SELL
            if (rsi_value <= 45 && rsi_value >= 31)
            {
                if (!(stoc_main >= 80) && !(stoc_main <= 20) && !(stoc_signal >= 80) && !(stoc_signal <= 20))
                {
                    if (stoc_main < stoc_signal)
                    {
                        if (mac_main < mac_signal)
                        {
                            alreadyCrossTrade = true;
                            return TradeSignal.Sell;
                        }
                    }
                }
            }

            //BUY
            if(rsi_value >= 55 && rsi_value <= 69)
            {
                if(!(stoc_main >= 80) && !(stoc_main <= 20) && !(stoc_signal >= 80) && !(stoc_signal <= 20)) 
                { 
                    if(stoc_main > stoc_signal)
                    {
                        if(mac_main > mac_signal)
                        {
                            alreadyCrossTrade= true;
                            return TradeSignal.Buy;
                        }
                    }
                }
            }

            return TradeSignal.None;
        }

        private void ClosePositions(TradeType tradeType)
        {
            foreach (var position in BotPositions)
            {
                if (position.TradeType != tradeType) continue;
                ClosePosition(position);
            }
        }
    }

    public enum TradeSignal
    {
        Buy,
        Sell,
        None
    }

    public enum Trend
    {
        Bullish,
        Bearish,
        Lateral,
        None
    }
}