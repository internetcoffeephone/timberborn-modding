using Bindito.Core;

namespace SequencedKeys
{
    [Context("Game")]
    public class SequencedKeysConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<ToolbarScanner>().AsSingleton();
            Bind<SequencedKeysService>().AsSingleton();
            Bind<SequencedKeysInitializer>().AsSingleton();
        }
    }
}
