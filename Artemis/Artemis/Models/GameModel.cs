﻿using System.Collections.Generic;
using Artemis.Managers;
using Artemis.Models.Interfaces;
using Artemis.Models.Profiles;

namespace Artemis.Models
{
    public abstract class GameModel : EffectModel
    {
        protected GameModel(MainManager mainManager, GameSettings settings, IDataModel dataModel) : base(mainManager, dataModel)
        {
            Settings = settings;
        }

        public GameSettings Settings { get; set; }
        public bool Enabled { get; set; }
        public string ProcessName { get; set; }
    }
}