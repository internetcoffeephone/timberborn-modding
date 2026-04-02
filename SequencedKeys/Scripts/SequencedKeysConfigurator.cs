using Bindito.Core;
using UnityEngine;

namespace SequencedKeys
{
    [Context("Game")]
    public class SequencedKeysConfigurator : Configurator
    {
        protected override void Configure()
        {
            Debug.Log("[SequencedKeys] Configurator.Configure() called — binding singletons.");
            Bind<ToolbarScanner>().AsSingleton();
            Bind<SequencedKeysService>().AsSingleton();
            Bind<SequencedKeysInitializer>().AsSingleton();
            Debug.Log("[SequencedKeys] Configurator.Configure() complete.");
        }
    }
}
