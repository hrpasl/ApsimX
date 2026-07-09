using System;
using APSIM.Core;
using APSIM.Numerics;
using APSIM.Shared.APSoil;
using APSIM.Shared.Utilities;
using Models.Core;
using Models.Interfaces;
using Models.Soils;
using Models.Soils.Nutrients;

namespace Models.Functions
{
    /// <summary>Fraction of N denitrified which is N2O</summary>
    /// <remarks>
    /// Denitrification N2O fraction model from DayCent
    /// </remarks>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class DayCentN2OFractionModel : Model, IFunction
    {

        /// <summary>The water balance model</summary>
        [Link]
        ISoilWater waterBalance = null;

        [Link(ByName = true)]
        Solute NO3 = null;

        [Link]
        Nutrient Nutrient = null;

        [Link]        
        CERESDenitrificationWaterFactor Gen_WF = null; 

        //public double N2ODiffusionCoefficient { get; set; } = 25.1;
 
        /// <summary>Gas diffusivity in soil at field capacity.</summary>
        [Description("Gas diffusivity in soil at field capacity")]
        public double[] N2ODiffusionCoefficientbylayer { get; set; }

        [EventSubscribe("WaterChanged")]
        private void OnWaterChanged(object sender, EventArgs e)
        {
            double[] SW = waterBalance.SW;
            
        if (N2ODiffusionCoefficientbylayer == null||
            N2ODiffusionCoefficientbylayer.Length != SW.Length)
            {
                N2ODiffusionCoefficientbylayer = new double[SW.Length];
            }

            for (int i = 0; i < SW.Length; i++)
            {
                if (Gen_WF.Value(i)<0.007)
                {
                     N2ODiffusionCoefficientbylayer[i] = (-3410 * Gen_WF.diffusivity[i]) + 23.8;
                }
                else
                {
                    N2ODiffusionCoefficientbylayer[i] = 0.0;
                }
            }
        }
        /// <summary>Gets the value.</summary>
        /// <value>The value.</value>
        public double Value(int arrayIndex = -1)
        {
            if (arrayIndex == -1)
                throw new Exception("Layer number must be provided to CERES Nitrification Model");

            double CO2Factor = Math.Max(0.16, Math.Exp(-0.8 * MathUtilities.Divide(NO3.kgha[arrayIndex], Nutrient.Catm[arrayIndex], 0)));
            double N2N2ORatio = Math.Max(0, N2ODiffusionCoefficientbylayer[arrayIndex] * CO2Factor);
            double N2OFraction = 1 / (N2N2ORatio + 1);

            return N2OFraction;
        }
    }
}