using Bindito.Core;
using SequencedKeybindings.Services;
using Timberborn.InputSystem;

namespace SequencedKeybindings
{
    [Context("Game")]
    public class SequencedKeybindingsConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<ToolbarNavigator>().AsSingleton();
            containerDefinition.Bind<BadgeOverlayService>().AsSingleton();
            containerDefinition.Bind<SequencedKeyService>().AsSingleton();
            containerDefinition.MultiBind<IInputProcessor>()
                .To<SequencedKeyService>().AsSingleton();
        }
    }
}
