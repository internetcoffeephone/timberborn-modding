namespace SequencedKeys
{
    /// <summary>
    /// Shared constants for keybinding IDs and defaults.
    /// </summary>
    public static class SequencedKeysConstants
    {
        public const string ActivateKeyId = "SequencedKeysActivate";

        public const string SelectKeyIdPrefix = "SequencedKeysSelect";

        /// <summary>
        /// Default number of selection keys (q, w, e, r).
        /// The actual count is determined by how many KeyBindingSpec files exist.
        /// </summary>
        public const int DefaultSelectionKeyCount = 4;

        /// <summary>
        /// Minimum number of selection keys allowed.
        /// </summary>
        public const int MinSelectionKeys = 2;
    }
}
