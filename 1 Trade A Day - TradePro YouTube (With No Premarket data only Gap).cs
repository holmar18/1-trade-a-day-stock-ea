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
        
        [Parameter("Stoch val for short above: ", DefaultValue = 75, MinValue = 0, MaxValue = 100, Step = 1, Group = "Strategy Settings")]
        public int UpperBound { get; set; }
        
        [Parameter("Stoch val for long under: ", DefaultValue = 25, MinValue = 0, MaxValue = 100, Step = 1, Group = "Strategy Settings")]
        public int LowerBound { get; set; }

        [Parameter("Stopp loss", DefaultValue = 200.00, MinValue = 0, MaxValue = 5000, Step = 1.0, Group = "Strategy Settings")]
        public double StopLoss { get; set; }
        
        [Parameter("Take profit", DefaultValue = 400.0, MinValue = 0, MaxValue = 5000, Step = 1.0, Group = "Strategy Settings")]
        public double TakeProfit { get; set; }
        
        [Parameter("Close position on market close", DefaultValue = true, Group = "Strategy Settings")]
        public bool CloseWhenMarketClose { get; set; }

        #endregion
        
        #region Globals
        private bool Long = false;
        private bool Short = false;
        
        private double StochBlue = 0;
        private double StochRed = 0;
        
        private int LastDayTraded = 0;
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
            
            // Stochastic
            StochasticRsi(index);
            StochCrossing(index);
            
            // Strategy
            GetLasBardayBefore(index);
            
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
        
        public void GetLasBardayBefore(int index)
        {
            double hour = (double) Bars.OpenTimes[index].Hour;
            double min = (double) Bars.OpenTimes[index].Minute / 100;

            if(hour + min == 13.3)
            {
                double currentHigh = Bars.HighPrices[index];
                double currentLow = Bars.LowPrices[index];
                
                double prevHigh = Bars.HighPrices[index - 1];
                double prevLow = Bars.LowPrices[index - 1];
                Chart.SetBarColor(index, Color.Orange);
                Chart.SetBarColor(index - 1, Color.Orange);
                
                if (currentHigh > prevHigh || currentLow > prevLow)
                {
                    Long = true;
                    ClosePositions();
                }
                else
                {
                    Short = true;
                    ClosePositions();
                }
            }
            
        }
        
        /*
            Checks for crossing of the stochastic indicator
            First it sets the StockCrossing for when the indicator starts so it knows where the line start.
        
        */
        private void StochCrossing(int index)
        {
            
            
            if(Math.Round(StochRed, 1) <= Math.Round(StochBlue, 1) && StochRed <= LowerBound && StochBlue <= LowerBound && Long)
            {  
                Print("LOWERBOND: ", LowerBound);
                Print("L - Red: ", StochRed);
                Print("L - StochBlue: ", StochBlue);
                Print("L - StochRed: ", StochRed);
                Print("L - UpperBound: ", LowerBound);
                ExicutePosition(TradeType.Buy, index);
                Long = false;
            }
            else if(Math.Round(StochRed, 1) >= Math.Round(StochBlue, 1) && StochRed >= UpperBound && StochBlue >= UpperBound && Short)
            {
                //Print("S - Red: ", StochRed);
                //Print("S - StochBlue: ", StochBlue);
                //Print("S - StockCrossing: ", StockCrossing);
                //Print("S - StochRed: ", StochRed);
                //Print("S - UpperBound: ", UpperBound);
                ExicutePosition(TradeType.Sell, index);
                Short = false;
            }
        }
        
        
        private void ClosePositions()
        {
            if(CloseWhenMarketClose && Positions.Count > 0)
            {
                Position pos = Positions.First();
                ClosePosition(pos);
            }
        }
        
        
        
        
        private void ExicutePosition(TradeType TrType, int index)
        {
            if(Positions.Count > 0 || LastDayTraded == Bars.OpenTimes[index].Day)
            {
                return;
            }
            
            
            if(TrType == TradeType.Buy)
            {
                double Sl = (StopLoss / 100);
                double Tp = (TakeProfit / 100);

                var res = ExecuteMarketOrder(TrType, Symbol.Name, 100, "PDI", Sl, Tp);
                if(res.IsSuccessful)
                {
                    Print(string.Format("SL:{0}   -  TP:{1}   -  Entry Price:{2}   -   Time:{3}", res.Position.StopLoss, res.Position.TakeProfit, res.Position.EntryPrice, res.Position.EntryTime));
                }
                Chart.SetBarColor(index, Color.Green);
            }
            else if(TrType == TradeType.Sell)
            {
                double Sl = (StopLoss / 100);
                double Tp = (TakeProfit / 100);

                var res = ExecuteMarketOrder(TrType, Symbol.Name, 100, "PDI", Sl, Tp);
                if(res.IsSuccessful)
                {
                    Print(string.Format("SL:{0}   -  TP:{1}   -  Entry Price:{2}   -   Time:{3}", res.Position.StopLoss, res.Position.TakeProfit, res.Position.EntryPrice, res.Position.EntryTime));
                }
                Chart.SetBarColor(index, Color.Red);

            }
            Print("Red: ", StochRed);
            LastDayTraded = Bars.OpenTimes[index].Day;
            
        }
        #endregion

    }
}