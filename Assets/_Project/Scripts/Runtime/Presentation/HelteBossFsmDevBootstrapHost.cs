using System.Collections;
using System.Linq;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Development-scene-only bootstrap that enters the Helte section without completing or saving
    /// the skipped tutorial quests. Do not place this component in TutorialScene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelteBossFsmDevBootstrapHost : MonoBehaviour
    {
        [SerializeField, Min(0)] private int settleFrames = 2;

        private IEnumerator Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (var frame = 0; frame < settleFrames; frame++)
                yield return null;

            var sectionSkip = Resources.FindObjectsOfTypeAll<TutorialDebugSectionSkipHost>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene == gameObject.scene);
            if (sectionSkip == null || !sectionSkip.HasValidSetup)
            {
                Debug.LogError(
                    "HelteBossFsmDevBootstrapHost requires the cloned tutorial section-skip setup.",
                    this);
                yield break;
            }

            if (!sectionSkip.JumpToSection(sectionSkip.SectionCount - 1))
            {
                Debug.LogError("Failed to enter the Helte FSM development section.", this);
                yield break;
            }

            Debug.Log("[sragon000][Helte FSM Dev] 헬테 보스 개발 구간으로 바로 이동했습니다.", this);
#else
            enabled = false;
            yield break;
#endif
        }
    }
}
