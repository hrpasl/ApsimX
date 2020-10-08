﻿namespace UserInterface.Presenters
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using Models;
    using Models.Core;
    using Views;

    /// <summary>
    /// This presenter connects an instance of a Model.Map with a 
    /// UserInterface.Views.MapView
    /// </summary>
    public class MapPresenter : IPresenter, IExportable
    {
        /// <summary>
        /// The axis model
        /// </summary>
        private Map map;

        /// <summary>
        /// The axis view
        /// </summary>
        private IMapView view;

        /// <summary>
        /// The parent explorer presenter.
        /// </summary>
        private ExplorerPresenter explorerPresenter;

        private PropertyPresenter propertyPresenter;

        /// <summary>
        /// Attach the specified Model and View.
        /// </summary>
        /// <param name="model">The model</param>
        /// <param name="view">The view</param>
        /// <param name="explorerPresenter">The parent explorer presenter</param>
        public void Attach(object model, object view, ExplorerPresenter explorerPresenter)
        {
            this.map = model as Map;
            this.view = view as MapView;
            this.explorerPresenter = explorerPresenter;

            propertyPresenter = new PropertyPresenter();
            propertyPresenter.Attach(model, this.view.Grid, this.explorerPresenter);

            // Tell the view to populate the axis.
            this.PopulateView();
            this.view.Zoom = this.map.Zoom;
            this.view.Center = this.map.Center;
            this.view.ViewChanged += this.OnViewChanged;
            this.view.PreviewDocs += OnPreviewDocs;
            explorerPresenter.CommandHistory.ModelChanged += OnModelChanged;
        }

        /// <summary>
        /// Invoked when the user wants to preview the map as it will appear
        /// in the autodocs.
        /// </summary>
        /// <param name="sender">Sender object.</param>
        /// <param name="e">Event arguments.</param>
        private void OnPreviewDocs(object sender, EventArgs e)
        {
            // Save any changes to the main map so that the preview
            // map uses the current zoom/coordinates as the main map.
            this.view.StoreSettings();

            var newView = new MapView(null);
            var newPresenter = new MapPresenter();

            var window = new WindowView((ViewBase.MasterView as MainView), newView, "Map Documentation Preview");
            window.Width = 800;
            window.Height = 800;
            window.Resizable = false;

            newPresenter.Attach(map, newView, explorerPresenter);
            newView.HideZoomControls();

            // If the user moves/zooms the map in the popup window,
            // changes will be saved to the map object when the presenter
            // is detached, and will be propagated automatically to the
            // master map via the OnModelChanged method below.
            window.Closed += (_, __) => newPresenter.Detach();
            window.Visible = true;
        }

        /// <summary>
        /// Detach the model from the view.
        /// </summary>
        public void Detach()
        {
            view.PreviewDocs -= OnPreviewDocs;
            explorerPresenter.CommandHistory.ModelChanged -= OnModelChanged;
            this.view.StoreSettings();
            this.view.ViewChanged -= this.OnViewChanged;
        }

        /// <summary>Export the map to PDF</summary>
        /// <param name="folder">The working directory name</param>
        /// <returns>The filename string</returns>
        public string ExportToPNG(string folder)
        {
            string path = this.map.FullPath.Replace(".Simulations.", string.Empty);
            string fileName = Path.Combine(folder, path + ".png");

            Image rawImage = this.view.Export();
            rawImage.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);

            return fileName;
        }

        /// <summary>
        /// Populate the view.
        /// </summary>
        private void PopulateView()
        {
            List<string> names = new List<string>();
            this.view.ShowMap(this.map.GetCoordinates(names), names, this.map.Zoom, this.map.Center);
        }

        /// <summary>
        /// Respond to changes in the map zoom level
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private void OnViewChanged(object sender, System.EventArgs e)
        {
            // Maintain a list of all property changes that we need to make.
            List<Commands.ChangeProperty.Property> properties = new List<Commands.ChangeProperty.Property>();

            // Store the property values.
            properties.Add(new Commands.ChangeProperty.Property(this.map, "Zoom", this.view.Zoom));
            properties.Add(new Commands.ChangeProperty.Property(this.map, "Center", this.view.Center));
            this.explorerPresenter.CommandHistory.Add(new Commands.ChangeProperty(properties));

            // properties.Add()
            // this.explorerPresenter.CommandHistory.Add(new Commands.ChangeProperty(map, "Zoom", this.view.Zoom));
            // this.map.Zoom = this.view.Zoom;
        }

        /// <summary>
        /// Respond to changes in the map position by saving the new position
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private void OnPositionChanged(object sender, System.EventArgs e)
        {
            try
            {
                this.explorerPresenter.CommandHistory.Add(new Commands.ChangeProperty(map, "Center", this.view.Center));
            }
            catch (Exception err)
            {
                explorerPresenter.MainPresenter.ShowError(err);
            }
        }

        /// <summary>
        /// The model has changed. Update the view.
        /// </summary>
        /// <param name="changedModel">The model that has changed.</param>
        private void OnModelChanged(object changedModel)
        {
            if (changedModel == this.map)
            {
                this.view.Zoom = this.map.Zoom;
                this.view.Center = this.map.Center;
            }
        }
    }
}
