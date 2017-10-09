﻿using Models.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.WholeFarm.Resources
{
	/// <summary>
	/// This stores the initialisation parameters for a Cohort of a specific Other Animal Type.
	/// </summary>
	[Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
	[ValidParent(ParentType = typeof(OtherAnimalsType))]
    [Description("This specifies an other animal cohort at the start of the simulation.")]
    public class OtherAnimalsTypeCohort: WFModel
	{
		/// <summary>
		/// Gender
		/// </summary>
		[Description("Gender")]
        [Required]
		public Sex Gender { get; set; }

		/// <summary>
		/// Age (Months)
		/// </summary>
		[Description("Age")]
        [Required]
        public int Age { get; set; }

		/// <summary>
		/// Starting Number
		/// </summary>
		[Description("Number")]
        [Required]
        public double Number { get; set; }

		/// <summary>
		/// Starting Weight
		/// </summary>
		[Description("Weight (kg)")]
        [Required]
        public double Weight { get; set; }

		/// <summary>
		/// Standard deviation of starting weight. Use 0 to use starting weight only
		/// </summary>
		[Description("Standard deviation of starting weight")]
        [Required]
        public double StartingWeightSD { get; set; }

		/// <summary>
		/// Flag to identify individual ready for sale
		/// </summary>
		public HerdChangeReason SaleFlag { get; set; }

		/// <summary>
		/// Gender as string for reports
		/// </summary>
		public string GenderAsString { get { return Gender.ToString().Substring(0, 1); } }

		/// <summary>
		/// SaleFlag as string for reports
		/// </summary>
		public string SaleFlagAsString { get { return SaleFlag.ToString(); } }
	}
}
