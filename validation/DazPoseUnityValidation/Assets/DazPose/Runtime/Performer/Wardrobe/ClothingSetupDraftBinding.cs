using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Saved setup draft, independent of the last successfully compiled performer binding.</summary>
    [DisallowMultipleComponent]
    public sealed class ClothingSetupDraftBinding : MonoBehaviour
    {
        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private WardrobeConfiguration configuration;
        public WardrobeCatalog Catalog => catalog;
        public WardrobeConfiguration Configuration => configuration;
#if UNITY_EDITOR
        public void Configure(WardrobeCatalog draftCatalog, WardrobeConfiguration draftConfiguration)
        { catalog = draftCatalog; configuration = draftConfiguration; }
#endif
    }
}
