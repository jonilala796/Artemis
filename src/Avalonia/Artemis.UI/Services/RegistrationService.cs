﻿using Artemis.Core.Services;
using Artemis.UI.Providers;
using Artemis.UI.Services.Interfaces;

namespace Artemis.UI.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IInputService _inputService;

        public RegistrationService(IInputService inputService)
        {
            _inputService = inputService;
        }
        public void RegisterBuiltInDataModelDisplays()
        {
        }

        public void RegisterBuiltInDataModelInputs()
        {
        }

        public void RegisterBuiltInPropertyEditors()
        {
        }

        public void RegisterProviders()
        {
            _inputService.AddInputProvider(new AvaloniaInputProvider());
        }

        public void RegisterControllers()
        {
        }

        public void ApplyPreferredGraphicsContext()
        {
        }
    }
}