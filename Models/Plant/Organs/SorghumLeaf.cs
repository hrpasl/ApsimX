﻿using APSIM.Shared.Utilities;
using Models.Core;
using Models.Interfaces;
using Models.Functions;
using Models.PMF.Interfaces;
using Models.PMF.Library;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Models.PMF.Phen;
using Models.PMF.Struct;
using System.Linq;
using Models.Functions.DemandFunctions;

namespace Models.PMF.Organs
{

    /// <summary>
    /// This organ is simulated using a SimpleLeaf organ type.  It provides the core functions of intercepting radiation, producing biomass
    ///  through photosynthesis, and determining the plant's transpiration demand.  The model also calculates the growth, senescence, and
    ///  detachment of leaves.  SimpleLeaf does not distinguish leaf cohorts by age or position in the canopy.
    /// 
    /// Radiation interception and transpiration demand are computed by the MicroClimate model.  This model takes into account
    ///  competition between different plants when more than one is present in the simulation.  The values of canopy Cover, LAI, and plant
    ///  Height (as defined below) are passed daily by SimpleLeaf to the MicroClimate model.  MicroClimate uses an implementation of the
    ///  Beer-Lambert equation to compute light interception and the Penman-Monteith equation to calculate potential evapotranspiration.  
    ///  These values are then given back to SimpleLeaf which uses them to calculate photosynthesis and soil water demand.
    /// </summary>
    /// <remarks>
    /// NOTE: the summary above is used in the Apsim's autodoc.
    /// 
    /// SimpleLeaf has two options to define the canopy: the user can either supply a function describing LAI or a function describing canopy cover directly.  From either of these functions SimpleLeaf can obtain the other property using the Beer-Lambert equation with the specified value of extinction coefficient.
    /// The effect of growth rate on transpiration is captured by the Fractional Growth Rate (FRGR) function, which is passed to the MicroClimate model.
    /// </remarks>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class SorghumLeaf : Model, IHasWaterDemand, IOrgan, IArbitration, ICustomDocumentation, IRemovableBiomass
    {
        //IHasWaterDemand, removing to see if it's necessary

        /// <summary>
        /// The SMM2SM
        /// </summary>
        public const double smm2sm = 1.0 / 1000000.0;      //! conversion factor of mm^2 to m^2

        /// <summary>The plant</summary>
        [Link]
        private Plant Plant = null;

        [Link]
        private SorghumArbitrator Arbitrator = null;

        /// <summary>The met data</summary>
        [Link]
        public IWeather MetData = null;

        #region Canopy interface

        /// <summary>Gets the LAI</summary>
        [Units("m^2/m^2")]
        public double DltLAI { get; set; }
        
        /// <summary>Gets the Potential DltLAI</summary>
        [Units("m^2/m^2")]
        public double dltPotentialLAI { get; set; }

        /// <summary>Gets the LAI</summary>
        [Units("m^2/m^2")]
        public double dltStressedLAI { get; set; }

        /// <summary>Gets the LAI</summary>
        [Units("m^2/m^2")]
        public double LAI { get; set; }

        /// <summary>Gets the LAI live + dead (m^2/m^2)</summary>
        public double LAITotal { get { return LAI + LAIDead; } }

        /// <summary>Gets the LAI</summary>
        public double SLN { get; set; }

        /// <summary>Gets the cover green.</summary>
        [Units("0-1")]
        public double CoverGreen { get; set; }
        //{
        //    get
        //    {
        //        if (Plant.IsAlive)
        //        {
        //            double greenCover = 0.0;
        //            if (CoverFunction == null)
        //                greenCover = 1.0 - Math.Exp(-ExtinctionCoefficientFunction.Value() * LAI);
        //            else
        //                greenCover = CoverFunction.Value();
        //            return Math.Min(Math.Max(greenCover, 0.0), 0.999999999); // limiting to within 10^-9, so MicroClimate doesn't complain
        //        }
        //        else
        //            return 0.0;

        //    }
        //}

        /// <summary>Gets the cover total.</summary>
        [Units("0-1")]
        public double CoverTotal
        {
            get { return 1.0 - (1 - CoverGreen) * (1 - CoverDead); }
        }

        /// <summary>Gets or sets the height.</summary>
        [Units("mm")]
        public double Height { get; set; }

        /// <summary>Sets the actual water demand.</summary>
        [Units("mm")]
        public double WaterDemand { get; set; }

        /// <summary>
        /// Flag to test if Microclimate is present
        /// </summary>
        public bool MicroClimatePresent { get; set; } = false;

        #endregion

        #region Parameters
        ///// <summary>The FRGR function</summary>
        //[Link]
        //IFunction FRGRFunction = null;   // VPD effect on Growth Interpolation Set

        ///// <summary>The cover function</summary>
        //[Link(IsOptional = true)]
        //IFunction CoverFunction = null;

        ///// <summary>The lai function</summary>
        //[Link(IsOptional = true)]
        //IFunction LAIFunction = null;

        /// <summary>The extinction coefficient function</summary>
        [Link(IsOptional = true)]
        IFunction ExtinctionCoefficientFunction = null;
        
        /// <summary>The photosynthesis</summary>
        [Link]
        public IFunction Photosynthesis = null;
        
        /// <summary>The height function</summary>
        [Link]
        IFunction HeightFunction = null;

        /// <summary>The lai dead function</summary>
        [Link]
        IFunction dltLAIFunction = null;

        /// <summary>The lai dead function</summary>
        [Link]
        IFunction LaiDeadFunction = null;

        ///// <summary>The lai dead function</summary>
        //[Link]
        //IFunction sdRatio = null;

        /// <summary>The lai dead function</summary>
        [Link]
        IFunction ExpansionStress = null;
        
        ///// <summary>The structure</summary>
        //[Link(IsOptional = true)]
        //public Structure Structure = null;

        /// <summary>Water Demand Function</summary>
        [Link(IsOptional = true)]
        IFunction WaterDemandFunction = null;

        /// <summary>DM Fixation Demand Function</summary>
        [Link]
        IFunction DMSupplyFixation = null;

        /// <summary>DM Fixation Demand Function</summary>
        [Link]
        IFunction PotentialBiomassTEFunction = null;
        /// <summary>DM Fixation Demand Function</summary>

        /// <summary>Potential Biomass via Radiation Use Efficientcy.</summary>
        public double BiomassRUE { get; set; }
        /// <summary>Potential Biomass via Radiation Use Efficientcy.</summary>
        public double BiomassTE { get; set; }

        /// <summary>The Stage that leaves are initialised on</summary>
        [Description("The Stage that leaves are initialised on")]
        public string LeafInitialisationStage { get; set; } = "Emergence";

        #endregion

        #region States and variables

        /// <summary>The leaves</summary>
        public List<Culm> Culms = new List<Culm>();

        /// <summary>Gets or sets the k dead.</summary>
        public double KDead { get; set; }                  // Extinction Coefficient (Dead)
        /// <summary>Calculates the water demand.</summary>
        public double CalculateWaterDemand()
        {
            if (WaterDemandFunction != null)
                return WaterDemandFunction.Value();
            else
            {
                return WaterDemand;
            }
        }
        /// <summary>Gets the transpiration.</summary>
        public double Transpiration { get { return WaterAllocation; } }

        ///// <summary>Potential Biomass limited by Transpiration Efficiency</summary>
        //[Link(IsOptional = true)]
        //IFunction PotentialBiomTEFunction = null;   

        ///// <summary>Gets the fw.</summary>
        //public double Fw { get { return MathUtilities.Divide(WaterAllocation, PotentialEP, 1); } }

        ///// <summary>Gets the function.</summary>
        //public double Fn
        //{
        //    get
        //    {
        //        if (Live != null)
        //            return MathUtilities.Divide(Live.N, Live.Wt * MaxNconc, 1);
        //        return 0;
        //    }
        //}

        ///// <summary>Gets the metabolic N concentration factor.</summary>
        //public double FNmetabolic
        //{
        //    get
        //    {
        //        double factor = 0.0;
        //        if (Live != null)
        //            factor = MathUtilities.Divide(Live.N - Live.StructuralN, Live.Wt * (CritNconc - MinNconc), 1.0);
        //        return Math.Min(1.0, factor);
        //    }
        //}

        /// <summary>Gets or sets the lai dead.</summary>
        public double LAIDead { get; set; }


        /// <summary>Gets the cover dead.</summary>
        public double CoverDead { get { return 1.0 - Math.Exp(-KDead * LAIDead); } }

        /// <summary>Gets the RAD int tot.</summary>
        [Units("MJ/m^2/day")]
        [Description("This is the intercepted radiation value that is passed to the RUE class to calculate DM supply")]
        public double RadIntTot
        {
            get
            {
                return CoverGreen * MetData.Radn;
            }
        }

        /// <summary>Potential biomass production due to water (transpiration).</summary>
        [Description("This is the daily Potential Biomass Production due to Transpiration")]
        public double dltDMPotentialTE
        {
            // referred to as both dlt_dm_transp and dltDMPotTE in old sorghum code
            get
            {
                return 0.0;
            }
        }

        /// <summary>Potential biomass production due to light (limited by water and N).</summary>
        [Description("This is the daily Potential Biomass Production due to light")]
        public double dltDMPotentialRUE
        {
            // referred to as both dlt_dm_transp and dltDMPotTE in old sorghum code
            get
            {
                return 0.0;
            }
        }
        /// <summary>Potential biomass production due to light (limited by water and N).</summary>
        [Description("This is the daily Potential Biomass Production due to light")]
        public double RUE
        {
            // referred to as both dlt_dm_transp and dltDMPotTE in old sorghum code
            get
            {
                return 0.0;
            }
        }

        /// <summary>Temperature Stress Function</summary>
        [Link]
        IFunction TemperatureStressFunction = null;

        /// <summary>Stress.</summary>
        [Description("Temp Stress")]
        public double TempStress
        {
            get
            {
                if(TemperatureStressFunction != null)
                {
                    return TemperatureStressFunction.Value();
                }
                return 1; //no stress
            }
        }

        /// <summary>Stress.</summary>
        [Description("Nitrogen Stress")]
        public double NitrogenStress
        {
            get
            {
                var photoStress = (2.0 / (1.0 + Math.Exp(-6.05 * (SLN - 0.41))) - 1.0);
                return Math.Max(photoStress, 0.0);
            }
        }

        /// <summary>Stress.</summary>
        [Description("Phosphorus Stress")]
        public double PhosphorusStress { get; set; }


        private double SowingDensity { get; set; }

        private bool LeafInitialised = false;
        #endregion

        #region Arbitrator Methods
        /// <summary>Gets or sets the water allocation.</summary>
        [XmlIgnore]
        public double WaterAllocation { get; set; }

        /// <summary>Calculate and return the dry matter supply (g/m2)</summary>
        [EventSubscribe("SetDMSupply")]
        private void SetDMSupply(object sender, EventArgs e)
        {
            DMSupply.Reallocation = AvailableDMReallocation();
            DMSupply.Retranslocation = AvailableDMRetranslocation();
            DMSupply.Uptake = 0;
            DMSupply.Fixation = DMSupplyFixation.Value();
        }

        /// <summary>The amount of mass lost each day from maintenance respiration</summary>
        public double MaintenanceRespiration { get; }

        /// <summary>Remove maintenance respiration from live component of organs.</summary>
        public void RemoveMaintenanceRespiration(double respiration)
        { }

        #endregion

        #region Events
        /// <summary>Called when [phase changed].</summary>
        [EventSubscribe("PhaseChanged")]
        private void OnPhaseChanged(object sender, PhaseChangedType phaseChange)
        {
            if (phaseChange.StageName == LeafInitialisationStage)
            {
                LeafInitialised = true;
                if(Culms.Count == 0)
                {
                    //first culm is the main culm
                    AddCulm(new CulmParameters() { Density = SowingDensity });
                }
            }
        }

        #endregion

        #region Component Process Functions

        /// <summary>Add a culm to the plant.</summary>
        public void AddCulm(CulmParameters parameters)
        {
            var culm = new Culm();
            culm.CulmNumber = parameters.CulmNumber;
            culm.Proportion = parameters.Proportion;
            culm.LeafAtAppearance = parameters.LeafAtAppearance;
            culm.VerticalAdjustment = parameters.VerticalAdjustment;
            culm.Density = SowingDensity;

            Culms.Add(culm);
        }

        /// <summary>Clears this instance.</summary>
        private void Clear()
        {
            Live = new Biomass();
            Dead = new Biomass();
            DMSupply.Clear();
            NSupply.Clear();
            DMDemand.Clear();
            NDemand.Clear();
            potentialDMAllocation.Clear();
            Allocated.Clear();
            Senesced.Clear();
            Detached.Clear();
            Removed.Clear();
            Height = 0;
            LAI = 0;

            Culms = new List<Culm>();
        
            LeafInitialised = false;
            laiEqlbLightTodayQ = new Queue<double>(10);
            laiEqlbLightTodayQ.Clear();

            laiEquilibWaterQ = new Queue<double>(10);
            sdRatioQ = new Queue<double>(5);
        }
        #endregion


        ///// <summary>Temperature Stress Function</summary>
        //[Link]
        //SorghumArbitrator Arbitrator = null;

        #region Top Level time step functions
        /// <summary>Event from sequencer telling us to do our potential growth.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantGrowth")]
        private void OnDoPotentialPlantGrowth(object sender, EventArgs e)
        {
            // save current state
            if (parentPlant.IsEmerged)
                startLive = Live;
            if (LeafInitialised)
            {

                //FRGR = FRGRFunction.Value();
                //if (CoverFunction == null && ExtinctionCoefficientFunction == null)
                //    throw new Exception("\"CoverFunction\" or \"ExtinctionCoefficientFunction\" should be defined in " + this.Name);
                //if (CoverFunction != null)
                //    LAI = (Math.Log(1 - CoverGreen) / (ExtinctionCoefficientFunction.Value() * -1));
                //if (LAIFunction != null)
                //    LAI = LAIFunction.Value(); //doesn't need to be calculated here as it is dne at the ed of the day

                // var dltPotentialLAI = 0.0;
                dltPotentialLAI = Culms.Sum(culm => culm.calcPotentialArea());
                //var tmp = Arbitrator.WatSupply;
                //var waterFunction = WaterDemandFunction as TEWaterDemandFunction;
                //var tmp3 = waterFunction.Value();
                //var tmp2 = sdRatio.Value();
                dltStressedLAI = dltPotentialLAI * ExpansionStress.Value();
                //old model calculated BiomRUE at the end of the day
                //this is done at ??, so make sure it refers to yesterdays results
                //BiomRUE = Photosynthesis.Value() * TemperatureStressFunction.Value() * NitrogenStress * PhosphorusStress;
                BiomassRUE = Photosynthesis.Value();


                //var bimT = 0.009 / waterFunction.VPD / 0.001 * Arbitrator.WSupply;
                BiomassTE = PotentialBiomassTEFunction.Value();

                //i think wsupply is being calculated as enough for demand
                if(BiomassTE - BiomassRUE > 0.5)
                {
                    Console.WriteLine("water supply is higher than demand");
                }

                Height = HeightFunction.Value();

                LAIDead = LaiDeadFunction.Value();
            }
        }

        /// <summary>Does the water limited dm allocations.  Water constaints to growth are accounted for in the calculation of DM supply
        /// and does initial N calculations to work out how much N uptake is required to pass to SoilArbitrator</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantPartioning")]
        virtual protected void OnDoPotentialPlantPartioning(object sender, EventArgs e)
        {
            if (Plant.IsEmerged)
            {
                //this will recalculate LAI given avaiable DM
                //areaActual in old model
                DltLAI = dltLAIFunction.Value();
                senesceArea();
            }
        }

        /// <summary>Does the nutrient allocations.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoActualPlantGrowth")]
        private void OnDoActualPlantGrowth(object sender, EventArgs e)
        {
            // if (!parentPlant.IsAlive) return; wtf
            if (!Plant.IsAlive) return;
            if (!LeafInitialised) return;

            // Derives seneseced plant dry matter (g/m^2) for the day
            // calculate scenesced N
            double laiToday = LAI + DltLAI;
            double dmGreenLeafToday = Live.Wt;   // should be already calculated
            double slaToday = dmGreenLeafToday > 0 ? laiToday / dmGreenLeafToday : 0.0;

            if (Live.Wt > 0.0)
            {
                var DltSenescedBiomass = slaToday > 0.0 ? DltSenescedLai / slaToday : 0.0;
                var SenescingProportion = DltSenescedBiomass / Live.Wt;

                if (DltSenescedBiomass > 0)
                {
                    var structuralWtSenescing = Live.StructuralWt * SenescingProportion;
                    Live.StructuralWt -= structuralWtSenescing;
                    Dead.StructuralWt += structuralWtSenescing;
                    Senesced.StructuralWt += structuralWtSenescing;

                    var metabolicWtSenescing = Live.MetabolicWt * SenescingProportion;
                    Live.MetabolicWt -= metabolicWtSenescing;
                    Dead.MetabolicWt += metabolicWtSenescing;
                    Senesced.StructuralWt += structuralWtSenescing;

                    var storageWtSenescing = Live.StorageWt * SenescingProportion;
                    Live.StorageWt -= storageWtSenescing;
                    Dead.StorageWt += storageWtSenescing;
                    Senesced.StructuralWt += structuralWtSenescing;
                }
            }

            double slnToday = laiToday > 0.0 ? Live.N / laiToday : 0.0;
            //var DltSenescedN = DltSenescedLai * Math.Max((slnToday - senescedLeafSLN), 0.0);

            //UpdateVars
            LAI += DltLAI;
            SLN = Live.N / LAI;
            CoverGreen = MathUtilities.Bound(1.0 - Math.Exp(-ExtinctionCoefficientFunction.Value() * LAI), 0.0, 0.999999999);// limiting to within 10^-9, so MicroClimate doesn't complain

        }
        /// <summary>sen_radn_crit.</summary>
        public double senRadnCrit { get; set; } = 2;
        /// <summary>sen_light_time_const.</summary>
        public double senLightTimeConst { get; set; } = 10;
        /// <summary>temperature threshold for leaf death.</summary>
        public double frostKill { get; set; } = 10;

        /// <summary>supply:demand ratio for onset of water senescence.</summary>
        public double senThreshold { get; set; } = 0.25;
        /// <summary>delay factor for water senescence.</summary>
        public double senWaterTimeConst { get; set; } = 10;


        /// <summary>Only water stress at this stage.</summary>
        /// Diff between potentialLAI and stressedLAI
        public double LossFromExpansionStress { get; set; }

        /// <summary>Total LAII as a result of senescence.</summary>
        public double SenescedLai { get; set; }

        /// <summary>Delta of LAI removed due to Senescence.</summary>
        public double DltSenescedLai { get; set; }
        /// <summary>Delta of LAI removed due to Light Senescence.</summary>
        public double DltSenescedLaiLight { get; set; }
        /// <summary>Delta of LAI removed due to Water Senescence.</summary>
        public double DltSenescedLaiWater { get; set; }
        /// <summary>Delta of LAI removed due to Frost Senescence.</summary>
        public double DltSenescedLaiFrost { get; set; }

        private double totalLaiEqlbLight;
        private double avgLaiEquilibLight;
        private Queue<double> laiEqlbLightTodayQ;
        private double updateAvLaiEquilibLight(double laiEqlbLightToday, int days)
        {
            totalLaiEqlbLight += laiEqlbLightToday;
            laiEqlbLightTodayQ.Enqueue(laiEqlbLightToday);
            if (laiEqlbLightTodayQ.Count > days)
            {
                totalLaiEqlbLight -= laiEqlbLightTodayQ.Dequeue();
            }
            return MathUtilities.Divide(totalLaiEqlbLight, laiEqlbLightTodayQ.Count, 0);
        }

        private double totalLaiEquilibWater;
        private double avLaiEquilibWater;
        private Queue<double> laiEquilibWaterQ;
        private double updateAvLaiEquilibWater(double valToday, int days)
        {
            totalLaiEquilibWater += valToday;
            laiEquilibWaterQ.Enqueue(valToday);
            if (laiEquilibWaterQ.Count > days)
            {
                totalLaiEquilibWater -= laiEquilibWaterQ.Dequeue();
            }
            return MathUtilities.Divide(totalLaiEquilibWater, laiEquilibWaterQ.Count, 0);
        }

        private double totalSDRatio;
        private double avSDRatio;
        private Queue<double> sdRatioQ;
        private double updateAvSDRatio(double valToday, int days)
        {
            totalSDRatio += valToday;
            sdRatioQ.Enqueue(valToday);
            if (sdRatioQ.Count > days)
            {
                totalSDRatio -= sdRatioQ.Dequeue();
            }
            return MathUtilities.Divide(totalSDRatio, sdRatioQ.Count, 0);
        }

        /// <summary>Senesce the LEaf Area.</summary>
        private void senesceArea()
        {
            DltSenescedLai = 0.0;
            //sLai - is the running total of dltSLai
            //could be a stage issue here. should only be between fi and flag
            LossFromExpansionStress += (dltPotentialLAI - dltStressedLAI);
            var maxLaiPossible = LAI + SenescedLai - LossFromExpansionStress;

            DltSenescedLaiLight = calcLaiSenescenceLight();
            DltSenescedLai = Math.Max(DltSenescedLai, DltSenescedLaiLight);

            DltSenescedLaiWater = calcLaiSenescenceWater();
            DltSenescedLai = Math.Max(DltSenescedLai, DltSenescedLaiWater);

            DltSenescedLaiFrost = calcLaiSenescenceFrost();
            DltSenescedLai = Math.Max(DltSenescedLai, DltSenescedLaiFrost);
            if(DltSenescedLai > 0.0)
            {
                Console.WriteLine("DltSenescedLAI: {0}", DltSenescedLai);
            }
        }

        private double calcLaiSenescenceFrost()
        {
            //  calculate senecence due to frost
            double dltSlaiFrost = 0.0;
            if (MetData.MinT < frostKill)
                dltSlaiFrost = LAI;

            return dltSlaiFrost;
        }

        private double calcLaiSenescenceWater()
        {
            /* TODO : Direct translation sort of. needs work */
            Arbitrator.WatSupply = Plant.Root.TotalExtractableWater();
            double dlt_dm_transp = PotentialBiomassTEFunction.Value();

            //double radnCanopy = divide(plant->getRadnInt(), coverGreen, plant->today.radn);
            double effectiveRue = MathUtilities.Divide(Photosynthesis.Value(), RadIntTot, 0);

            double radnCanopy = MathUtilities.Divide(RadIntTot, CoverGreen, MetData.Radn);

            double sen_radn_crit = MathUtilities.Divide(dlt_dm_transp, effectiveRue, radnCanopy);
            double intc_crit = MathUtilities.Divide(sen_radn_crit, radnCanopy, 1.0);

            //            ! needs rework for row spacing
            double laiEquilibWaterToday;
            if (intc_crit < 1.0)
                laiEquilibWaterToday = -Math.Log(1.0 - intc_crit) / ExtinctionCoefficientFunction.Value();
            else
                laiEquilibWaterToday = LAI;

            avLaiEquilibWater = updateAvLaiEquilibWater(laiEquilibWaterToday, 10);
            var sdRatio = 0.0;
            avSDRatio = updateAvSDRatio(sdRatio, 5);
            //// average of the last 10 days of laiEquilibWater`
            //laiEquilibWater.push_back(laiEquilibWaterToday);
            //double avLaiEquilibWater = movingAvgVector(laiEquilibWater, 10);

            //// calculate a 5 day moving average of the supply demand ratio
            //avSD.push_back(plant->water->getSdRatio());

            double dltSlaiWater = 0.0;
            if (avSDRatio < senThreshold)
            {
                dltSlaiWater = Math.Max(0.0, MathUtilities.Divide((LAI - avLaiEquilibWater), senWaterTimeConst, 0.0));
            }
            dltSlaiWater = Math.Min(LAI, dltSlaiWater);

            return dltSlaiWater;
            //return 0.0;
        }

        private double calcLaiSenescenceLight()
        {
            double critTransmission = MathUtilities.Divide(senRadnCrit, MetData.Radn, 1);
            /* TODO : Direct translation - needs cleanup */
            //            ! needs rework for row spacing
            double laiEqlbLightToday;
            if (critTransmission > 0.0)
            {
                laiEqlbLightToday = -Math.Log(critTransmission) / ExtinctionCoefficientFunction.Value();
            }
            else
            {
                laiEqlbLightToday = LAI;
            }
            // average of the last 10 days of laiEquilibLight
            avgLaiEquilibLight = updateAvLaiEquilibLight(laiEqlbLightToday, 10);//senLightTimeConst?

            double radnTransmitted = MetData.Radn;// - Plant->getRadnInt();
            double dltSlaiLight = 0.0;
            if (radnTransmitted < senRadnCrit)
                dltSlaiLight = Math.Max(0.0, MathUtilities.Divide(LAI - avgLaiEquilibLight, senLightTimeConst, 0.0));
            dltSlaiLight = Math.Min(dltSlaiLight, LAI);
            return dltSlaiLight;
        }

        #endregion

        /// <summary>Tolerance for biomass comparisons</summary>
        protected double BiomassToleranceValue = 0.0000000001;

        /// <summary>The parent plant</summary>
        [Link]
        private Plant parentPlant = null;

        /// <summary>The surface organic matter model</summary>
        [Link]
        private ISurfaceOrganicMatter surfaceOrganicMatter = null;

        /// <summary>Link to biomass removal model</summary>
        [ChildLink]
        private BiomassRemoval biomassRemovalModel = null;

        /// <summary>The senescence rate function</summary>
        [ChildLinkByName]
        [Units("/d")]
        protected IFunction senescenceRate = null;

        /// <summary>The N retranslocation factor</summary>
        [ChildLinkByName]
        [Units("/d")]
        protected IFunction nRetranslocationFactor = null;

        /// <summary>The N reallocation factor</summary>
        [ChildLinkByName]
        [Units("/d")]
        protected IFunction nReallocationFactor = null;

        // NOT CURRENTLY USED /// <summary>The nitrogen demand switch</summary>
        //[ChildLinkByName]
        //private IFunction nitrogenDemandSwitch = null;

        /// <summary>The DM retranslocation factor</summary>
        [ChildLinkByName]
        [Units("/d")]
        private IFunction dmRetranslocationFactor = null;

        /// <summary>The DM reallocation factor</summary>
        [ChildLinkByName]
        [Units("/d")]
        private IFunction dmReallocationFactor = null;

        /// <summary>The DM demand function</summary>
        [ChildLinkByName]
        [Units("g/m2/d")]
        private BiomassDemand dmDemands = null;

        /// <summary>The N demand function</summary>
        [ChildLinkByName]
        [Units("g/m2/d")]
        private BiomassDemand nDemands = null;

        /// <summary>The initial biomass dry matter weight</summary>
        [ChildLinkByName]
        [Units("g/m2")]
        private IFunction initialWtFunction = null;

        /// <summary>The initial biomass dry matter weight</summary>
        [ChildLinkByName]
        [Units("g/m2")]
        private IFunction initialLAIFunction = null;

        /// <summary>The initial SLN value</summary>
        [ChildLinkByName]
        [Units("g/m2")]
        private IFunction initialSLNFunction = null;

        /// <summary>The maximum N concentration</summary>
        [ChildLinkByName]
        [Units("g/g")]
        private IFunction maximumNConc = null;

        /// <summary>The minimum N concentration</summary>
        [ChildLinkByName]
        [Units("g/g")]
        private IFunction minimumNConc = null;

        /// <summary>The critical N concentration</summary>
        [ChildLinkByName]
        [Units("g/g")]
        private IFunction criticalNConc = null;

//#pragma warning disable 414
        /// <summary>Carbon concentration</summary>
        /// [Units("-")]
        [Link]
        public IFunction CarbonConcentration = null;
//#pragma warning restore 414

        /// <summary>The live biomass state at start of the computation round</summary>
        protected Biomass startLive = null;

        /// <summary>The dry matter supply</summary>
        public BiomassSupplyType DMSupply { get; set; }

        /// <summary>The nitrogen supply</summary>
        public BiomassSupplyType NSupply { get; set; }

        /// <summary>The dry matter demand</summary>
        public BiomassPoolType DMDemand { get; set; }

        /// <summary>Structural nitrogen demand</summary>
        public BiomassPoolType NDemand { get; set; }

        /// <summary>The dry matter potentially being allocated</summary>
        public BiomassPoolType potentialDMAllocation { get; set; }

        /// <summary>Constructor</summary>
        public SorghumLeaf()
        {
            Live = new Biomass();
            Dead = new Biomass();
        }

        /// <summary>Gets a value indicating whether the biomass is above ground or not</summary>
        public bool IsAboveGround { get { return true; } }

        /// <summary>The live biomass</summary>
        [XmlIgnore]
        public Biomass Live { get; private set; }

        /// <summary>The dead biomass</summary>
        [XmlIgnore]
        public Biomass Dead { get; private set; }

        /// <summary>Gets the total biomass</summary>
        [XmlIgnore]
        public Biomass Total { get { return Live + Dead; } }


        /// <summary>Gets the biomass allocated (represented actual growth)</summary>
        [XmlIgnore]
        public Biomass Allocated { get; private set; }

        /// <summary>Gets the biomass senesced (transferred from live to dead material)</summary>
        [XmlIgnore]
        public Biomass Senesced { get; private set; }

        /// <summary>Gets the biomass detached (sent to soil/surface organic matter)</summary>
        [XmlIgnore]
        public Biomass Detached { get; private set; }

        /// <summary>Gets the biomass removed from the system (harvested, grazed, etc.)</summary>
        [XmlIgnore]
        public Biomass Removed { get; private set; }

        /// <summary>Gets the potential DM allocation for this computation round.</summary>
        public BiomassPoolType DMPotentialAllocation { get { return potentialDMAllocation; } }

        /// <summary>Gets or sets the n fixation cost.</summary>
        [XmlIgnore]
        public virtual double NFixationCost { get { return 0; } }

        /// <summary>Gets the maximum N concentration.</summary>
        [XmlIgnore]
        public double MaxNconc { get { return maximumNConc.Value(); } }

        /// <summary>Gets the minimum N concentration.</summary>
        [XmlIgnore]
        public double MinNconc { get { return minimumNConc.Value(); } }

        /// <summary>Gets the minimum N concentration.</summary>
        [XmlIgnore]
        public double CritNconc { get { return criticalNConc.Value(); } }

        /// <summary>Gets the total (live + dead) dry matter weight (g/m2)</summary>
        [XmlIgnore]
        public double Wt { get { return Live.Wt + Dead.Wt; } }

        /// <summary>Gets the total (live + dead) N amount (g/m2)</summary>
        [XmlIgnore]
        public double N { get { return Live.N + Dead.N; } }

        /// <summary>Gets the total (live + dead) N concentration (g/g)</summary>
        [XmlIgnore]
        public double Nconc
        {
            get
            {
                if (Wt > 0.0)
                    return N / Wt;
                else
                    return 0.0;
            }
        }

        /// <summary>Removes biomass from organs when harvest, graze or cut events are called.</summary>
        /// <param name="biomassRemoveType">Name of event that triggered this biomass remove call.</param>
        /// <param name="amountToRemove">The fractions of biomass to remove</param>
        public virtual void RemoveBiomass(string biomassRemoveType, OrganBiomassRemovalType amountToRemove)
        {
            biomassRemovalModel.RemoveBiomass(biomassRemoveType, amountToRemove, Live, Dead, Removed, Detached);
        }

        /// <summary>Computes the amount of DM available for retranslocation.</summary>
        public double AvailableDMRetranslocation()
        {
            double availableDM = Math.Max(0.0, startLive.StorageWt - DMSupply.Reallocation) * dmRetranslocationFactor.Value();
            if (availableDM < -BiomassToleranceValue)
                throw new Exception("Negative DM retranslocation value computed for " + Name);

            return availableDM;
        }

        /// <summary>Computes the amount of DM available for reallocation.</summary>
        public double AvailableDMReallocation()
        {
            double availableDM = startLive.StorageWt * senescenceRate.Value() * dmReallocationFactor.Value();
            if (availableDM < -BiomassToleranceValue)
                throw new Exception("Negative DM reallocation value computed for " + Name);

            return availableDM;
        }

        /// <summary>Calculate and return the nitrogen supply (g/m2)</summary>
        [EventSubscribe("SetNSupply")]
        protected virtual void SetNSupply(object sender, EventArgs e)
        {
            NSupply.Reallocation = Math.Max(0, (startLive.StorageN + startLive.MetabolicN) * senescenceRate.Value() * nReallocationFactor.Value());
            if (NSupply.Reallocation < -BiomassToleranceValue)
                throw new Exception("Negative N reallocation value computed for " + Name);

            NSupply.Retranslocation = Math.Max(0, (startLive.StorageN + startLive.MetabolicN) * (1 - senescenceRate.Value()) * nRetranslocationFactor.Value());
            if (NSupply.Retranslocation < -BiomassToleranceValue)
                throw new Exception("Negative N retranslocation value computed for " + Name);

            NSupply.Fixation = 0;
            NSupply.Uptake = 0;
        }

        /// <summary>Calculate and return the dry matter demand (g/m2)</summary>
        [EventSubscribe("SetDMDemand")]
        protected virtual void SetDMDemand(object sender, EventArgs e)
        {
            DMDemand.Structural = dmDemands.Structural.Value(); // / dmConversionEfficiency.Value() + remobilisationCost.Value();
            DMDemand.Storage = Math.Max(0, dmDemands.Storage.Value()); // / dmConversionEfficiency.Value());
            DMDemand.Metabolic = 0;
        }

        /// <summary>Calculate and return the nitrogen demand (g/m2)</summary>
        [EventSubscribe("SetNDemand")]
        protected virtual void SetNDemand(object sender, EventArgs e)
        {
            NDemand.Structural = nDemands.Structural.Value();
            NDemand.Metabolic = nDemands.Metabolic.Value();
            NDemand.Storage = nDemands.Storage.Value();
        }

        /// <summary>Sets the dry matter potential allocation.</summary>
        /// <param name="dryMatter">The potential amount of drymatter allocation</param>
        public void SetDryMatterPotentialAllocation(BiomassPoolType dryMatter)
        {
            potentialDMAllocation.Structural = dryMatter.Structural;
            potentialDMAllocation.Metabolic = dryMatter.Metabolic;
            potentialDMAllocation.Storage = dryMatter.Storage;
        }

        /// <summary>Sets the dry matter allocation.</summary>
        /// <param name="dryMatter">The actual amount of drymatter allocation</param>
        public virtual void SetDryMatterAllocation(BiomassAllocationType dryMatter)
        {
            // Check retranslocation
            if (dryMatter.Retranslocation - startLive.StorageWt > BiomassToleranceValue)
                throw new Exception("Retranslocation exceeds non structural biomass in organ: " + Name);


            // allocate structural DM
            Allocated.StructuralWt = Math.Min(dryMatter.Structural, DMDemand.Structural);
            Live.StructuralWt += Allocated.StructuralWt;

            // allocate non structural DM
            if ((dryMatter.Storage - DMDemand.Storage) > BiomassToleranceValue)
                throw new Exception("Non structural DM allocation to " + Name + " is in excess of its capacity");

        }

        /// <summary>Sets the n allocation.</summary>
        /// <param name="nitrogen">The nitrogen allocation</param>
        public virtual void SetNitrogenAllocation(BiomassAllocationType nitrogen)
        {
            Live.StructuralN += nitrogen.Structural;
            Live.StorageN += nitrogen.Storage;
            Live.MetabolicN += nitrogen.Metabolic;

            Allocated.StructuralN += nitrogen.Structural;
            Allocated.StorageN += nitrogen.Storage;
            Allocated.MetabolicN += nitrogen.Metabolic;

            // Retranslocation
            if (MathUtilities.IsGreaterThan(nitrogen.Retranslocation, startLive.StorageN + startLive.MetabolicN - NSupply.Retranslocation))
                throw new Exception("N retranslocation exceeds storage + metabolic nitrogen in organ: " + Name);
            double StorageNRetranslocation = Math.Min(nitrogen.Retranslocation, startLive.StorageN * (1 - senescenceRate.Value()) * nRetranslocationFactor.Value());
            Live.StorageN -= StorageNRetranslocation;
            Live.MetabolicN -= (nitrogen.Retranslocation - StorageNRetranslocation);
            Allocated.StorageN -= nitrogen.Retranslocation;

            // Reallocation
            if (MathUtilities.IsGreaterThan(nitrogen.Reallocation, startLive.StorageN + startLive.MetabolicN))
                throw new Exception("N reallocation exceeds storage + metabolic nitrogen in organ: " + Name);
            double StorageNReallocation = Math.Min(nitrogen.Reallocation, startLive.StorageN * senescenceRate.Value() * nReallocationFactor.Value());
            Live.StorageN -= StorageNReallocation;
            Live.MetabolicN -= (nitrogen.Reallocation - StorageNReallocation);
            Allocated.StorageN -= nitrogen.Reallocation;
        }

        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        protected void OnSimulationCommencing(object sender, EventArgs e)
        {
            NDemand = new BiomassPoolType();
            DMDemand = new BiomassPoolType();
            NSupply = new BiomassSupplyType();
            DMSupply = new BiomassSupplyType();
            potentialDMAllocation = new BiomassPoolType();
            startLive = new Biomass();
            Allocated = new Biomass();
            Senesced = new Biomass();
            Detached = new Biomass();
            Removed = new Biomass();
            Live = new Biomass();
            Dead = new Biomass();
            Clear();
        }

        /// <summary>Called when [do daily initialisation].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoDailyInitialisation")]
        protected void OnDoDailyInitialisation(object sender, EventArgs e)
        {
            if (parentPlant.IsAlive)
            {
                Allocated.Clear();
                Senesced.Clear();
                Detached.Clear();
                Removed.Clear();
            }
        }

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="data">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantSowing")]
        protected void OnPlantSowing(object sender, SowPlant2Type data)
        {
            if (data.Plant == parentPlant)
            {
                Clear();
                SowingDensity = data.Population;

                Live.StructuralWt = initialWtFunction.Value() * SowingDensity;
                Live.StorageWt = 0.0;
                LAI = initialLAIFunction.Value() * smm2sm * SowingDensity;
                SLN = initialSLNFunction.Value();

                Live.StructuralN = LAI * SLN;
                Live.StorageN = 0;
            }
        }

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantEnding")]
        protected void DoPlantEnding(object sender, EventArgs e)
        {
            if (Wt > 0.0)
            {
                Detached.Add(Live);
                Detached.Add(Dead);
                surfaceOrganicMatter.Add(Wt * 10, N * 10, 0, parentPlant.CropType, Name);
            }

            Clear();
        }

        /// <summary>Writes documentation for this function by adding to the list of documentation tags.</summary>
        /// <param name="tags">The list of tags to add to.</param>
        /// <param name="headingLevel">The level (e.g. H2) of the headings.</param>
        /// <param name="indent">The level of indentation 1, 2, 3 etc.</param>
        public void Document(List<AutoDocumentation.ITag> tags, int headingLevel, int indent)
        {
            if (IncludeInDocumentation)
            {
                // add a heading, the name of this organ
                tags.Add(new AutoDocumentation.Heading(Name, headingLevel));

                // write the basic description of this class, given in the <summary>
                AutoDocumentation.DocumentModelSummary(this, tags, headingLevel, indent, false);

                // write the memos
                foreach (IModel memo in Apsim.Children(this, typeof(Memo)))
                    AutoDocumentation.DocumentModel(memo, tags, headingLevel + 1, indent);

                //// List the parameters, properties, and processes from this organ that need to be documented:

                // document initial DM weight
                IModel iniWt = Apsim.Child(this, "initialWtFunction");
                AutoDocumentation.DocumentModel(iniWt, tags, headingLevel + 1, indent);

                // document DM demands
                tags.Add(new AutoDocumentation.Heading("Dry Matter Demand", headingLevel + 1));
                tags.Add(new AutoDocumentation.Paragraph("The dry matter demand for the organ is calculated as defined in DMDemands, based on the DMDemandFunction and partition fractions for each biomass pool.", indent));
                IModel DMDemand = Apsim.Child(this, "dmDemands");
                AutoDocumentation.DocumentModel(DMDemand, tags, headingLevel + 2, indent);

                // document N demands
                tags.Add(new AutoDocumentation.Heading("Nitrogen Demand", headingLevel + 1));
                tags.Add(new AutoDocumentation.Paragraph("The N demand is calculated as defined in NDemands, based on DM demand the N concentration of each biomass pool.", indent));
                IModel NDemand = Apsim.Child(this, "nDemands");
                AutoDocumentation.DocumentModel(NDemand, tags, headingLevel + 2, indent);

                // document N concentration thresholds
                IModel MinN = Apsim.Child(this, "MinimumNConc");
                AutoDocumentation.DocumentModel(MinN, tags, headingLevel + 2, indent);
                IModel CritN = Apsim.Child(this, "CriticalNConc");
                AutoDocumentation.DocumentModel(CritN, tags, headingLevel + 2, indent);
                IModel MaxN = Apsim.Child(this, "MaximumNConc");
                AutoDocumentation.DocumentModel(MaxN, tags, headingLevel + 2, indent);
                IModel NDemSwitch = Apsim.Child(this, "NitrogenDemandSwitch");
                if (NDemSwitch is Constant)
                {
                    if ((NDemSwitch as Constant).Value() == 1.0)
                    {
                        //Don't bother documenting as is does nothing
                    }
                    else
                    {
                        tags.Add(new AutoDocumentation.Paragraph("The demand for N is reduced by a factor of " + (NDemSwitch as Constant).Value() + " as specified by the NitrogenDemandSwitch", indent));
                    }
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The demand for N is reduced by a factor specified by the NitrogenDemandSwitch.", indent));
                    AutoDocumentation.DocumentModel(NDemSwitch, tags, headingLevel + 2, indent);
                }

                // document DM supplies
                tags.Add(new AutoDocumentation.Heading("Dry Matter Supply", headingLevel + 1));
                IModel DMReallocFac = Apsim.Child(this, "DMReallocationFactor");
                if (DMReallocFac is Constant)
                {
                    if ((DMReallocFac as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " does not reallocate DM when senescence of the organ occurs.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " will reallocate " + (DMReallocFac as Constant).Value() * 100 + "% of DM that senesces each day.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of senescing DM that is allocated each day is quantified by the DMReallocationFactor.", indent));
                    AutoDocumentation.DocumentModel(DMReallocFac, tags, headingLevel + 2, indent);
                }
                IModel DMRetransFac = Apsim.Child(this, "DMRetranslocationFactor");
                if (DMRetransFac is Constant)
                {
                    if ((DMRetransFac as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " does not retranslocate non-structural DM.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " will retranslocate " + (DMRetransFac as Constant).Value() * 100 + "% of non-structural DM each day.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of non-structural DM that is allocated each day is quantified by the DMReallocationFactor.", indent));
                    AutoDocumentation.DocumentModel(DMRetransFac, tags, headingLevel + 2, indent);
                }

                // document photosynthesis
                IModel PhotosynthesisModel = Apsim.Child(this, "Photosynthesis");
                AutoDocumentation.DocumentModel(PhotosynthesisModel, tags, headingLevel + 2, indent);

                // document N supplies
                tags.Add(new AutoDocumentation.Heading("Nitrogen Supply", headingLevel + 1));
                IModel NReallocFac = Apsim.Child(this, "NReallocationFactor");
                if (NReallocFac is Constant)
                {
                    if ((NReallocFac as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " does not reallocate N when senescence of the organ occurs.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " will reallocate " + (NReallocFac as Constant).Value() * 100 + "% of N that senesces each day.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of senescing N that is allocated each day is quantified by the NReallocationFactor.", indent));
                    AutoDocumentation.DocumentModel(NReallocFac, tags, headingLevel + 2, indent);
                }
                IModel NRetransFac = Apsim.Child(this, "NRetranslocationFactor");
                if (NRetransFac is Constant)
                {
                    if ((NRetransFac as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " does not retranslocate non-structural N.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " will retranslocate " + (NRetransFac as Constant).Value() * 100 + "% of non-structural N each day.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of non-structural N that is allocated each day is quantified by the NReallocationFactor.", indent));
                    AutoDocumentation.DocumentModel(NRetransFac, tags, headingLevel + 2, indent);
                }

                // document canopy
                tags.Add(new AutoDocumentation.Heading("Canopy Properties", headingLevel + 1));
                IModel laiF = Apsim.Child(this, "LAIFunction");
                IModel coverF = Apsim.Child(this, "CoverFunction");
                if (laiF != null)
                {
                    tags.Add(new AutoDocumentation.Paragraph(Name + " has been defined with a LAIFunction, cover is calculated using the Beer-Lambert equation.", indent));
                    AutoDocumentation.DocumentModel(laiF, tags, headingLevel + 2, indent);
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph(Name + " has been defined with a CoverFunction. LAI is calculated using an inverted Beer-Lambert equation", indent));
                    AutoDocumentation.DocumentModel(coverF, tags, headingLevel + 2, indent);
                }
                IModel exctF = Apsim.Child(this, "ExtinctionCoefficientFunction");
                AutoDocumentation.DocumentModel(exctF, tags, headingLevel + 2, indent);
                IModel heightF = Apsim.Child(this, "HeightFunction");
                AutoDocumentation.DocumentModel(heightF, tags, headingLevel + 2, indent);

                // document senescence and detachment
                tags.Add(new AutoDocumentation.Heading("Senescence and Detachment", headingLevel + 1));
                IModel SenRate = Apsim.Child(this, "SenescenceRate");
                if (SenRate is Constant)
                {
                    if ((SenRate as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " has senescence parameterised to zero so all biomass in this organ will remain alive.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " senesces " + (SenRate as Constant).Value() * 100 + "% of its live biomass each day, moving the corresponding amount of biomass from the live to the dead biomass pool.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of live biomass that senesces and moves into the dead pool each day is quantified by the SenescenceRate.", indent));
                    AutoDocumentation.DocumentModel(SenRate, tags, headingLevel + 2, indent);
                }

                IModel DetRate = Apsim.Child(this, "DetachmentRateFunction");
                if (DetRate is Constant)
                {
                    if ((DetRate as Constant).Value() == 0)
                        tags.Add(new AutoDocumentation.Paragraph(Name + " has detachment parameterised to zero so all biomass in this organ will remain with the plant until a defoliation or harvest event occurs.", indent));
                    else
                        tags.Add(new AutoDocumentation.Paragraph(Name + " detaches " + (DetRate as Constant).Value() * 100 + "% of its live biomass each day, passing it to the surface organic matter model for decomposition.", indent));
                }
                else
                {
                    tags.Add(new AutoDocumentation.Paragraph("The proportion of Biomass that detaches and is passed to the surface organic matter model for decomposition is quantified by the DetachmentRateFunction.", indent));
                    AutoDocumentation.DocumentModel(DetRate, tags, headingLevel + 2, indent);
                }

                if (biomassRemovalModel != null)
                    biomassRemovalModel.Document(tags, headingLevel + 1, indent);
            }
        }
    }
}