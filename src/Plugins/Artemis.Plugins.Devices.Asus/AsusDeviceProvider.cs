﻿using Artemis.Core.Plugins.Abstract;
using Artemis.Core.Plugins.Models;
using Artemis.Core.Services.Interfaces;
using RGB.NET.Core;
using RGB.NET.Devices.Asus;

namespace Artemis.Plugins.Devices.Asus
{
    // ReSharper disable once UnusedMember.Global
    public class AsusDeviceProvider : DeviceProvider
    {
        private readonly IRgbService _rgbService;

        public AsusDeviceProvider(PluginInfo pluginInfo, IRgbService rgbService) : base(pluginInfo, RGB.NET.Devices.Asus.AsusDeviceProvider.Instance)
        {
            _rgbService = rgbService;
        }

        public override void EnablePlugin()
        {
            PathHelper.ResolvingAbsolutePath += (sender, args) => ResolveAbsolutePath(typeof(AsusRGBDevice<>), sender, args);
            _rgbService.AddDeviceProvider(RgbDeviceProvider);
        }

        public override void DisablePlugin()
        {
            // TODO: Remove the device provider from the surface
        }

        public override void Dispose()
        {
            // TODO: This will probably not go well without first removing the device provider
            // AsusDeviceProvider.Instance.ResetDevices();
            // AsusDeviceProvider.Instance.Dispose();
        }
    }
}