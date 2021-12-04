﻿using Artemis.UI.Shared;
using Material.Icons;

namespace Artemis.UI.Screens.Root.Sidebar.Dialogs
{
    public class ProfileIconViewModel : ViewModelBase
    {
        public ProfileIconViewModel(MaterialIconKind icon)
        {
            Icon = icon;
            DisplayName = icon.ToString();
        }

        public MaterialIconKind Icon { get; }
    }
}