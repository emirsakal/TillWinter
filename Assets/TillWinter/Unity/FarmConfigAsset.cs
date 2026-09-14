using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Optional editor wrapper around <see cref="FarmConfig"/>. Core never depends on this.</summary>
    [CreateAssetMenu(menuName = "Till Winter/Farm Config", fileName = "FarmConfig")]
    public sealed class FarmConfigAsset : ScriptableObject
    {
        public FarmConfig Config = new FarmConfig();
    }
}
