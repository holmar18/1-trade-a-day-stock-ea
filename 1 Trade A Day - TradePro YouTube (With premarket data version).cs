using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


/*

    1 Trade a day bot.
    
    Video: https://www.youtube.com/watch?v=4SEZTUaBzAk
    
    Þegar ég gerði þennan bot fyrir cTrader þá gleymdi ég að skoða hlutabréf og hvort það væri í boði
    premarket gögn. Svo vildi það til að ég finn ekki hvernig er hægt að sjá þau þannig að bottinn setur
    ekki niður tímarammana og tekur enginn trade.

*/

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.FullAccess)]
    public class OneTradeADay : Robot
    {
        #region Prameters
        
        [Parameter("FastK", DefaultValue = 14, MinValue = 0, MaxValue = 150, Step = 1, Group = "Stochastic Settings")]
        public int FastK { get; set; }
        
        [Parameter("SlowK", DefaultValue = 3, MinValue = 0, MaxValue = 150, Step = 1, Group = "Stochastic Settings")]
        public int SlowK { get; set; }
        
        [Parameter("SlowD", DefaultValue = 3, MinValue = 0, MaxValue = 150, Step = 1, Group = "Stochastic Settings")]
        public int SlowD { get; set; }
        
        [Parameter("Stoch value for short ABOVE: ", DefaultValue = 75, MinValue = 0, MaxValue = 100, Step = 1, Group = "Strategy Settings")]
        public int UpperBound { get; set; }
        
        [Parameter("Stoch value for long UNDER: ", DefaultValue = 25, MinValue = 0, MaxValue = 100, Step = 1, Group = "Strategy Settings")]
        public int LowerBound { get; set; }
        
        [Parameter("Time Start", DefaultValue = 9.00, MinValue = 0, MaxValue = 24, Step = 0.05, Group = "Strategy Settings")]
        public double TimeOne { get; set; }
        
        [Parameter("Time End", DefaultValue = 9.30, MinValue = 0, MaxValue = 24, Step = 0.05, Group = "Strategy Settings")]
        public double TimeTwo { get; set; }
        
        [Parameter("Stopp loss (0 = No SL)", DefaultValue = 20.00, MinValue = 0, MaxValue = 5000, Step = 1.0, Group = "Trade Settings")]
        public double StopLoss { get; set; }
        
        [Parameter("Take profit (0 = No TP)", DefaultValue = 40.0, MinValue = 0, MaxValue = 5000, Step = 1.0, Group = "Trade Settings")]
        public double TakeProfit { get; set; }
        
        [Parameter("Lot size", DefaultValue = 1.0, MinValue = 0.01, MaxValue = 5000, Step = 0.1, Group = "Trade Settings")]
        public double LotSize { get; set; }
        
        [Parameter("Close position on (Time Start)", DefaultValue = true, Group = "Trade Settings")]
        public bool ClosePosOnMarketClose { get; set; }
        
        [Parameter("Allow long trades", DefaultValue = true, Group = "Trade Settings")]
        public bool AllowLongTrades { get; set; }
        
        [Parameter("Allow short trades", DefaultValue = true, Group = "Trade Settings")]
        public bool AllowShortTrades { get; set; }
        
        [Parameter("Use Breakeven", DefaultValue = true, Group = "Trade Settings")]
        public bool UseBreakEven { get; set; }

        [Parameter("Breakeven at Risk unit", DefaultValue = 1, MinValue = 0.5, MaxValue = 1300, Step = 0.1, Group = "Trade Settings")]
        public double BreakEvenMultiplyer { get; set; }
        #endregion
        
        #region Globals
        private List<double> HighsNLows = new List<double>();
        private double maxVal = 0.00;
        private double minVal = 0.00;
        private bool Long = false;
        private bool Short = false;
        
        private double StochBlue = 0;
        private double StochRed = 0;

        
        // These values block more than 1 trade per day.
        private int TradeCount = 0;
        private int TradeCountExecuted = 0;
        #endregion
        
        
        #region cBot Events
        protected override void OnStart()
        {
           Print("Bot is starting");
        }
        
        
        protected override void OnStop()
        {
            Print("Bot is Stopping");
        }
        

        protected override void OnTick()
        {
            int index = Bars.Count - 1;
            // Strategy
            CheckStrategy(index);
            CheckBreakOut(index);
            
            // Stochastic
            StochasticRsi(index);
            StochCrossing(index);
            if(UseBreakEven)
            {
                MoveToBreakEven();
            }
            //Print("Account Equity: ", Account.Equity); // Upphæð reiknings
        }
        #endregion
        
        
        #region Helpers
        private int RandomNum()
        {
            Random r = new Random();
            return r.Next(0, 1000000);
        }
        #endregion


        #region Indicators
        public void StochasticRsi(int index)
        {
            if(index < FastK)
            {
                StochBlue = 0;
                StochRed = 0;
                return;
            }

            double min = Bars.LowPrices.Minimum(FastK);
            double max = Bars.HighPrices.Maximum(FastK);
            double fast = 0.0;
            
            if (Math.Abs(max - min) > double.Epsilon)
                fast = (Bars.ClosePrices[index] - min)/(max - min)*100;
                
            double tmpOne = StochBlue + (fast - StochBlue)/SlowK;
            StochBlue = tmpOne;
            
            
            // red line
            double tmpTwo = StochRed + (StochBlue - StochRed)/SlowD;
            StochRed = tmpTwo;
        }
        #endregion


        #region cBot Action
        
        /*
            Stores the highs & lows inside the timeframe period given in the settings.
        */
        public void StoreHighsNLows(int index)
        {
            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            HighsNLows.Add(high);
            HighsNLows.Add(low);
        }
        
        
        /*
            Checks for crossing of the stochastic indicator
            First it sets the StockCrossing for when the indicator starts so it knows where the line start.
        
        */
        private void StochCrossing(int index)
        {
            
            if(Math.Round(StochRed, 1) <= Math.Round(StochBlue, 1) && StochRed <= LowerBound && StochBlue <= LowerBound && Long)
            {
                ExicutePosition(TradeType.Buy, index);
                Chart.SetBarColor(index, Color.Green);
                Long = false;
            }
            else if(Math.Round(StochRed) >= Math.Round(StochBlue) && StochRed >= UpperBound && StochBlue >= UpperBound && Short)
            {
                ExicutePosition(TradeType.Sell, index);
                Chart.SetBarColor(index, Color.Red);
                Short = false;
            }
        }

        
        
        public void CheckStrategy(int index)
        {
            double hour = (double) Bars.OpenTimes[index].Hour;
            double min = (double) Bars.OpenTimes[index].Minute / 100;
            
            // if there is an open trade and new day timeframe is starting close the open trade if any.
            if(TimeOne == hour && Positions.Count > 0 && ClosePosOnMarketClose)
            {
                Position pos = Positions.First();
                ClosePosition(pos);
            }
            /*
                Check if the time frame is inside TimeOne and TimeTwo timeframe 
                if so store the highs and the lows to find the min and max later.
            */

            if(TimeOne <= hour + min && TimeTwo >= hour + min)
            {
                StoreHighsNLows(index);
            }
            
            /*
                Draw the vertial line on timeone and timeTwo
            */
            if(TimeOne == hour + min || TimeTwo == hour + min)
            {
                Chart.DrawVerticalLine(string.Format("{0}", RandomNum()), Bars.Count - 1, Color.Purple, 2, LineStyle.Solid);
            }
            
            if(TimeTwo == hour + min && HighsNLows.Count > 0)
            {
                // Get min and max value of the highs and lows
                maxVal = HighsNLows.Max(val => val);
                minVal = HighsNLows.Min(val => val);

                // Draw the lines on highest and lowest point inside the timeframe.
                Chart.DrawHorizontalLine(string.Format("{0}", "TOP"), maxVal, Color.Blue, 1, LineStyle.Solid);
                Chart.DrawHorizontalLine(string.Format("{0}", "BOT"), minVal, Color.Blue, 1, LineStyle.Solid);
                
                // Clear the list.
                HighsNLows.Clear();
            }
           
        }
        
        /*
            Checks if a bar closes above or under the timeframes set
        */
        private void CheckBreakOut(int index)
        {
            if(maxVal != 0)
            {
                double close = Bars.ClosePrices[index];
                double open = Bars.OpenPrices[index];
                if(open > maxVal && close > maxVal)
                {
                    Chart.SetBarColor(index, Color.Orange);
                    maxVal = 0;
                    minVal = 0;
                    Long = true;
                    TradeCount += 1;
                }
                else if(open < minVal && close < minVal)
                {
                    Chart.SetBarColor(index, Color.Pink);
                    maxVal = 0;
                    minVal = 0;
                    Short = true;
                    TradeCount += 1;
                }
            }
        }
        
        
        private void ExicutePosition(TradeType TrType, int index)
        {
            if(Positions.Count > 0 || TradeCount == TradeCountExecuted)
            {
                return;
            }
            TradeCountExecuted = TradeCount;
            
            double Sl = StopLoss / 1000.0;
            double Tp = (TakeProfit / 1000.0);
            
            
            
            // Draw SL/TP on the chart
            if(TrType == TradeType.Buy && AllowLongTrades)
            {
                ExecuteMarketOrder(TrType, Symbol.Name, LotSize * 100000, "PDI", StopLoss, TakeProfit, "LONG");
            }
            else if(TrType == TradeType.Sell && AllowShortTrades)
            {
                ExecuteMarketOrder(TrType, Symbol.Name, LotSize * 100000, "PDI", StopLoss, TakeProfit, "SHORT");
            }
        }
        
        
        private void MoveToBreakEven()
        {

            if (Positions.Count == 0)
            {
                return;
            }
            var position = Positions.First();
            if(position.Pips >= StopLoss)
            {
                double add = position.TradeType == TradeType.Buy ? 0.0001 : -0.0001;
                ModifyPosition(position, position.EntryPrice + add, position.TakeProfit);
            }  
        }
        
        #endregion

    }
}