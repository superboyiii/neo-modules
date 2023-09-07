using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class FasterDBStore : Plugin, IStoreProvider
    {
        public override string Description => "Uses Microsoft FASTER to store the blockchain data.";

        public FasterDBStore() =>
            StoreFactory.RegisterProvider(this);

        public IStore GetStore(string path) =>
            new FasterStore(path);
    }
}
