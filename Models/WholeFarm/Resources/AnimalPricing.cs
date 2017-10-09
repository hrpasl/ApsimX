﻿using Models.Core;
using Models.WholeFarm.Activities;
using Models.WholeFarm.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace Models.WholeFarm.Resources
{
	/// <summary>
	/// User entry of Animal prices
	/// </summary>
	[Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
	[ValidParent(ParentType = typeof(RuminantType))]
    [Description("This is the parent model component holing all Animal Price Entries that define the value of individuals in the breed/herd.")]
    public class AnimalPricing: WFModel
	{
		/// <summary>
		/// Style of pricing animals
		/// </summary>
		[Description("Style of pricing animals")]
        [Required]
        public PricingStyleType PricingStyle { get; set; }

        /// <summary>
        /// Price of individual breeding sire
        /// </summary>
        [Description("Price of individual breeding sire")]
        [Required, Range(0, int.MaxValue, ErrorMessage = "Value must be a greter than or equal to 0")]
        public double BreedingSirePrice { get; set; }
    }

    /// <summary>
    /// Individual price entry
    /// </summary>
    [Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
	[ValidParent(ParentType = typeof(AnimalPricing))]
    [Description("This is the price entry specifying the value of all individuals of a specified sex and greater than or equal to the specified age.")]
    public class AnimalPriceEntry: WFModel
	{
		/// <summary>
		/// Gender
		/// </summary>
		[Description("Gender")]
        [Required]
        public Sex Gender { get; set; }

		/// <summary>
		/// Age in months
		/// </summary>
		[Description("Age in months")]
        [Required, Range(0, double.MaxValue, ErrorMessage = "Value must be a greter than or equal to 0")]
        public double Age { get; set; }

		/// <summary>
		/// Purchase value of individual
		/// </summary>
		[Description("Purchase value of individual")]
        [Required, Range(0, double.MaxValue, ErrorMessage = "Value must be a greter than or equal to 0")]
        public double PurchaseValue { get; set; }

		/// <summary>
		/// Sell value of individual
		/// </summary>
		[Description("Sell value of individual")]
        [Required, Range(0, double.MaxValue, ErrorMessage = "Value must be a greter than or equal to 0")]
        public double SellValue { get; set; }
	}


}
