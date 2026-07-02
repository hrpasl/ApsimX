using System;
using APSIM.Core;
using APSIM.Numerics;
using APSIM.Shared.Utilities;
using Models.Core;
using Models.Interfaces;
using Models.Soils;

namespace Models.Functions
{
    /// <summary>Fraction of urea that hydrolyses per day</summary>
    /// \pre All children have to contain a public function "Value"
    /// \retval fraction of Urea hydrolysed.
    [Serializable]
    [Description("Urea hydrolysis model from CERES-Maize")]
    public class CERESUreaHydrolysisModel : Model, IFunction
    {
        [Link]
        Organic organic = null;

        private double[] wf;

        [Link]
        Soil soil = null;

        [Link]
        Water water = null;

        [Link]
        IPhysical physical = null;

        [Link]
        Chemical chemical = null;

        [Link]
        ISoilTemperature soilTemperature = null;

        [Link(Type = LinkType.Child)]
        CERESMineralisationWaterFactor CERESWF = null;


        /// <summary>Gets the value.</summary>
        /// <value>The value.</value>
        [EventSubscribe("WaterChanged")]
        private void OnWaterChanged(object sender, EventArgs e)
        {
            double[] SW = water.Volumetric;
            double[] AD = physical.AirDry;
            double[] LL15 = physical.LL15;
            double[] DUL = physical.DUL;
            double[] SAT = physical.SAT;
            if (wf == null)
                wf = new double[SW.Length];
            for (int i = 0; i < SW.Length; i++)
            {
                if (SW[i] < LL15[i])
                {
                        wf[i] = Math.Min(1, MathUtilities.Divide(SW[i] - AD[i], LL15[i] - AD[i], 0.0));
                }
                else if (SW[i] < DUL[i])
                {
                    wf[i]=1;
                }
                else
                    wf[i] = 1 - 0.5 * MathUtilities.Divide(SW[i] - DUL[i], SAT[i] - DUL[i], 0.0);
            }
        }
    

        /// <summary>Gets the value.</summary>
        /// <value>The value.</value>
        public double Value(int arrayIndex = -1)
        {
            if (arrayIndex == -1)
                throw new Exception("Layer number must be provided to CERES Urea Hydrolysis Model");

            double potentialRate = -1.12 + 1.31 * organic.Carbon[arrayIndex] + 0.203 * chemical.PH[arrayIndex] - 0.155 * organic.Carbon[arrayIndex] * chemical.PH[arrayIndex];
            potentialRate = MathUtilities.Bound(potentialRate, 0, 1);
            double WF = MathUtilities.Bound(wf[arrayIndex], 0, 1);
            double TF = MathUtilities.Bound(soilTemperature.Value[arrayIndex] / 40 + 0.2, 0, 1);
            double rateModifer = Math.Min(WF, TF);

            return potentialRate * rateModifer;
        }
    }
}
