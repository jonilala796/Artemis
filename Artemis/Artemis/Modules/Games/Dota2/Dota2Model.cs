﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;
using Artemis.KeyboardProviders;
using Artemis.Managers;
using Artemis.Models;
using Artemis.Utilities;
using Artemis.Utilities.GameState;
using Artemis.Utilities.Keyboard;
using Newtonsoft.Json;
using Color = System.Drawing.Color;

namespace Artemis.Modules.Games.Dota2
{
    internal class Dota2Model : GameModel
    {
        private KeyboardRegion _keyPad;
        private KeyboardRegion _abilityKeys;
        private KeyboardRegion _topRow;

        public Dota2Model(MainManager mainManager, Dota2Settings settings) : base(mainManager)
        {
            Settings = settings;
            Name = "Dota2";
            ProcessName = "dota2";
            Enabled = Settings.Enabled;
            Initialized = false;
            Scale = 4;
        }


        public override void Dispose()
        {
            Initialized = false;
            MainManager.GameStateWebServer.GameDataReceived -= HandleGameData;
        }

        public override void Enable()
        {
            Initialized = false;
            _topRow = MainManager.KeyboardManager.ActiveKeyboard.KeyboardRegions.First(r => r.RegionName == "TopRow");
            _keyPad = MainManager.KeyboardManager.ActiveKeyboard.KeyboardRegions.First(r => r.RegionName == "NumPad");
            _abilityKeys = MainManager.KeyboardManager.ActiveKeyboard.KeyboardRegions.First(r => r.RegionName == "QWER");
            HealthRectangle = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard
                , 0
                , _topRow.BottomRight.Y*Scale
                , new List<Color>()
                , LinearGradientMode.Horizontal)
            {Height = Scale, ContainedBrush = false};

            ManaRectangle = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard
                , 0
                , (_topRow.BottomRight.Y + 1)*Scale
                , new List<Color>()
                , LinearGradientMode.Horizontal)
            {Height = Scale, ContainedBrush = false};

            EventRectangle = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard
                , 0
                , _topRow.TopLeft.X + 3
                , new List<Color>()
                , LinearGradientMode.Horizontal)
            {
                Height = MainManager.KeyboardManager.ActiveKeyboard.Height*Scale - Scale
                ,
                Width = MainManager.KeyboardManager.ActiveKeyboard.Width*Scale - Scale - 12
            };

            DayCycleRectangle = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard
                , _keyPad.TopLeft.X*Scale
                , _keyPad.TopLeft.Y* Scale
                , new List<Color>()
                , LinearGradientMode.Horizontal)
            {
                Height = _keyPad.GetRectangle().Height*Scale,
                Width = _keyPad.GetRectangle().Width*Scale
            };

            SetAbilityKeys();

            MainManager.GameStateWebServer.GameDataReceived += HandleGameData;
            Initialized = true;
        }

        private void SetAbilityKeys()
        {
            #region Long Switch Statement for Keys
            switch (Settings.KeyboardLayout)
            {
                case "0":
                case "Default": //default
                case "4": //Heroes of newearth
                case "3": //League of Legends
                    for (int i = 0; i < AbilityKeysRectangles.Length; i++)
                    {
                        AbilityKeysRectangles[i] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                        (_abilityKeys.TopLeft.X + i) * Scale - 2,
                                        _abilityKeys.TopLeft.Y * Scale,
                                        new List<Color>(),
                                        LinearGradientMode.Horizontal)
                        {
                            Height = Scale,
                            Width = Scale
                        };
                    }
                    break;
                case "2":
                    AbilityKeysRectangles[0] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                    (_abilityKeys.TopLeft.X * Scale) - 2,
                                    _abilityKeys.TopLeft.Y * Scale,
                                    new List<Color>(),
                                    LinearGradientMode.Horizontal)
                    {
                        Height = Scale,
                        Width = Scale
                    };
                    AbilityKeysRectangles[1] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                    ((_abilityKeys.TopLeft.X + 2) * Scale) - 2,
                                    _abilityKeys.TopLeft.Y * Scale,
                                    new List<Color>(),
                                    LinearGradientMode.Horizontal)
                    {
                        Height = Scale,
                        Width = Scale
                    };
                    AbilityKeysRectangles[2] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                    ((_abilityKeys.TopLeft.X +3) * Scale) - 2,
                                    _abilityKeys.TopLeft.Y * Scale,
                                    new List<Color>(),
                                    LinearGradientMode.Horizontal)
                    {
                        Height = Scale,
                        Width = Scale
                    };
                    AbilityKeysRectangles[3] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                    ((_abilityKeys.TopLeft.X +3)* Scale) - 2,
                                    (_abilityKeys.TopLeft.Y +1)* Scale,
                                    new List<Color>(),
                                    LinearGradientMode.Horizontal)
                    {
                        Height = Scale,
                        Width = Scale
                    };
                    break;
                case "1": //MMO
                case "5": //Smite
                    for (int i = 0; i < AbilityKeysRectangles.Length; i++)
                    {
                        AbilityKeysRectangles[i] = new KeyboardRectangle(MainManager.KeyboardManager.ActiveKeyboard,
                                        (_abilityKeys.TopLeft.X + i) * Scale - 3,
                                        (_abilityKeys.TopLeft.Y-1) * Scale,
                                        new List<Color>(),
                                        LinearGradientMode.Horizontal)
                        {
                            Height = Scale,
                            Width = Scale
                        };
                    }
                    break;

            }
        #endregion
        }

        public override void Update()
        {
            if (D2Json?.map == null)
                return;

            UpdateMainColor();
            if (Settings.ShowEvents)
                UpdateEvents();
            if (Settings.ShowDayCycle)
                UpdateDay();
            if (!D2Json.hero.alive)
                return;
            if (Settings.CanCastAbility)
                UpdateAbilities();
            if (Settings.ShowHealth)
                UpdateHealth();
            if (Settings.ShowMana)
                UpdateMana();

        }

        private void UpdateMainColor()
        {
            var list = new List<Color> {ColorHelpers.ToDrawingColor(Settings.MainColor)};
            EventRectangle.Colors = list;
            DayCycleRectangle.Colors = list;
            HealthRectangle.Colors = list;
            ManaRectangle.Colors = list;
            foreach (var key in AbilityKeysRectangles)
                key.Colors = list;
        }

        private void UpdateEvents()
        {
            List<Color> list = null;
            if (!D2Json.hero.alive)
                list = new List<Color> { Color.LightGray };
            else if (D2Json.hero.disarmed)
                list = new List<Color> { Color.Yellow };
            else if (D2Json.hero.hexed)
                list = new List<Color> { Color.Yellow };
            else if (D2Json.hero.silenced)
                list = new List<Color> { Color.Yellow };
            else if (D2Json.hero.stunned)
                list = new List<Color> { Color.Yellow };
            else if (D2Json.hero.magicimmune)
                list = new List<Color> { Color.Lime };
            if(list == null)
                return;

            EventRectangle.Colors = list;
            DayCycleRectangle.Colors = list;
            HealthRectangle.Colors = list;
            ManaRectangle.Colors = list;
            foreach (var item in AbilityKeysRectangles)
                item.Colors = list;
        }

        private void UpdateDay()
        {
            if (D2Json?.map?.daytime == null)
                return;

            if (D2Json.map.nightstalker_night)
            {
                DayCycleRectangle.Colors = new List<Color> {Color.Blue};
                return;
            }

            var timeLeft = 240 - D2Json.map.clock_time%240;
            var timePercentage = 100.00/240*timeLeft;
            DayCycleRectangle.Width = (int) (_keyPad.GetRectangle().Width*Scale/100.00*timePercentage);
            DayCycleRectangle.Colors = D2Json.map.daytime
                ? new List<Color> {Color.Yellow}
                : new List<Color> {Color.Blue};
        }

        private void UpdateMana()
        {
            if (D2Json?.hero == null || D2Json.hero.mana_percent == -1)
                return;

            var manaPercent = D2Json.hero.mana_percent;
            ManaRectangle.Colors = new List<Color> {ColorHelpers.ToDrawingColor(Settings.ManaColor)};
            ManaRectangle.Width = (int) Math.Floor(_topRow.GetRectangle().Width*Scale/100.00*manaPercent);
        }

        private void UpdateAbilities()
        {
            if (AbilityKeysRectangles == null)
                return;
            
            AbilityKeysRectangles[0].Colors = D2Json?.abilities?.ability0?.can_cast == true ? new List<Color>
            { ColorHelpers.ToDrawingColor(Settings.AbilityReadyColor) } : new List<Color> { ColorHelpers.ToDrawingColor(Settings.AbilityCooldownColor) };
            AbilityKeysRectangles[1].Colors = D2Json?.abilities?.ability1?.can_cast == true ? new List<Color>
            { ColorHelpers.ToDrawingColor(Settings.AbilityReadyColor) } : new List<Color> { ColorHelpers.ToDrawingColor(Settings.AbilityCooldownColor) };
            AbilityKeysRectangles[2].Colors = D2Json?.abilities?.ability2?.can_cast == true ? new List<Color>
            { ColorHelpers.ToDrawingColor(Settings.AbilityReadyColor) } : new List<Color> { ColorHelpers.ToDrawingColor(Settings.AbilityCooldownColor) };
            AbilityKeysRectangles[3].Colors = D2Json?.abilities?.ability3?.can_cast == true ? new List<Color>
            { ColorHelpers.ToDrawingColor(Settings.AbilityReadyColor) } : new List<Color> { ColorHelpers.ToDrawingColor(Settings.AbilityCooldownColor) };
        }


        private void UpdateHealth()
        {
            if (D2Json?.hero == null || D2Json.hero.health_percent == -1)
                return;

            var healthPercent = D2Json.hero.health_percent;
            if (healthPercent > 66)
                HealthRectangle.Colors = new List<Color> {Color.Lime};
            else if (healthPercent > 33)
                HealthRectangle.Colors = new List<Color> {Color.Yellow};
            else
                HealthRectangle.Colors = new List<Color> {Color.Red};

            HealthRectangle.Width = (int) Math.Floor(_topRow.GetRectangle().Width*Scale/100.00*healthPercent);
        }


        public override Bitmap GenerateBitmap()
        {
            var bitmap = MainManager.KeyboardManager.ActiveKeyboard.KeyboardBitmap(Scale);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                EventRectangle.Draw(g);
                HealthRectangle.Draw(g);
                ManaRectangle.Draw(g);
                foreach (var item in AbilityKeysRectangles)
                {
                    item.Draw(g);
                }
                DayCycleRectangle.Draw(g);

            }
            return bitmap;
        }


        public void HandleGameData(object sender, GameDataReceivedEventArgs e)
        {
            var jsonString = e.Json.ToString();

            // Ensure it's Dota 2 JSON
            if (!jsonString.Contains("Dota 2"))
                return;

            // Parse the JSON
            D2Json = JsonConvert.DeserializeObject<Dota2DataModel.Rootobject>(jsonString);
        }

        #region Variables

        public Dota2Settings Settings { get; set; }
        public Dota2DataModel.Rootobject D2Json { get; set; }
        public int Scale { get; set; }
        public KeyboardRectangle HealthRectangle { get; set; }
        public KeyboardRectangle EventRectangle { get; set; }
        public KeyboardRectangle DayCycleRectangle { get; set; }
        public KeyboardRectangle ManaRectangle { get; set; }
        public KeyboardRectangle[] AbilityKeysRectangles = new KeyboardRectangle[4];
        #endregion

    }
}