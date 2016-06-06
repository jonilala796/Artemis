﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Artemis.Managers;
using Artemis.Models;
using Artemis.Models.Profiles;
using Artemis.Utilities.GameState;
using Newtonsoft.Json;
using Ninject.Extensions.Logging;
using Brush = System.Windows.Media.Brush;

namespace Artemis.Modules.Games.CounterStrike
{
    public class CounterStrikeModel : GameModel
    {
        public CounterStrikeModel(MainManager mainManager, CounterStrikeSettings settings) : base(mainManager, settings, new CounterStrikeDataModel())
        {
            Name = "CounterStrike";
            ProcessName = "csgo";
            Scale = 4;
            Enabled = Settings.Enabled;
            Initialized = false;
        }

        public ILogger Logger { get; set; }
        public int Scale { get; set; }

        public override void Dispose()
        {
            Initialized = false;
            MainManager.GameStateWebServer.GameDataReceived -= HandleGameData;
        }

        public override void Enable()
        {
            Initialized = false;

            MainManager.GameStateWebServer.GameDataReceived += HandleGameData;

            Initialized = true;
        }

        public override void Update()
        {
            // TODO: Set up active weapon in the datamodel
        }

        public void HandleGameData(object sender, GameDataReceivedEventArgs e)
        {
            var jsonString = e.Json.ToString();

            // Ensure it's CS:GO JSON
            if (!jsonString.Contains("Counter-Strike: Global Offensive"))
                return;

            // Parse the JSON
            try
            {
                DataModel = JsonConvert.DeserializeObject<CounterStrikeDataModel>(jsonString);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to deserialize CS:GO JSON");
                throw;
            }
            
        }

        public override List<LayerModel> GetRenderLayers(bool renderMice, bool renderHeadsets)
        {
            return Profile.GetRenderLayers<CounterStrikeDataModel>(DataModel, renderMice, renderHeadsets);
        }
    }
}