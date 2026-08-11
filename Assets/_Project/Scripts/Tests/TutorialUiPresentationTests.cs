using Narthex.Presentation;
using NUnit.Framework;

namespace Narthex.Tests
{
    public sealed class TutorialUiPresentationTests
    {
        [TestCase("아다마스 본부 · TUTO_A_01", "아다마스 본부")]
        [TestCase("외부 전투 스테이지 1 · TUTO_F_01", "외부 전투 스테이지 1")]
        [TestCase("선착장 TUTO-H-01", "선착장")]
        [TestCase("훈련장", "훈련장")]
        public void StageDisplayName_HidesDeveloperProgressId(string input, string expected)
        {
            Assert.That(DialogueViewModule.SanitizeStageDisplayName(input), Is.EqualTo(expected));
        }

        [TestCase("SPACE", false, "SPACE · 대화 진행")]
        [TestCase("SPACE", true, "SPACE · 대화 닫기")]
        [TestCase("", false, "SPACE · 대화 진행")]
        public void DialoguePrompt_ExplainsSpaceAction(string binding, bool closing, string expected)
        {
            Assert.That(TutorialDialoguePresenter.FormatContinuePrompt(binding, closing), Is.EqualTo(expected));
        }

        [TestCase("괜찮아. 이제 이동하자.", PromeDialogueExpressionKind.Default)]
        [TestCase("적의 습격이야. 당장 막아야 해.", PromeDialogueExpressionKind.Stern)]
        [TestCase("하… 또 시작이네.", PromeDialogueExpressionKind.Sigh)]
        public void PromeDialogue_ChoosesReadableExpression(string line, PromeDialogueExpressionKind expected)
        {
            Assert.That(DialogueViewModule.ResolvePromeExpression(line), Is.EqualTo(expected));
        }
    }
}
