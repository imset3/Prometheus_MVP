using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Narthex.Tools
{
    public static class PrometheusReleaseTestMenu
    {
        private static TestRunnerApi runner;
        private static string currentLabel;

        [MenuItem(PrometheusToolMenuPaths.Validation + "Run All EditMode Tests")]
        public static void RunAllEditModeTests() => Run(TestMode.EditMode, "EditMode 전체");

        [MenuItem(PrometheusToolMenuPaths.Validation + "Run All PlayMode Tests")]
        public static void RunAllPlayModeTests() => Run(TestMode.PlayMode, "PlayMode 전체");

        private static void Run(TestMode mode, string label)
        {
            if (runner != null)
            {
                Debug.LogWarning($"[sragon000][릴리스 검증] {currentLabel} 테스트가 이미 실행 중입니다.");
                return;
            }

            currentLabel = label;
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Callbacks());
            runner.Execute(new ExecutionSettings(new Filter { testMode = mode }));
            Debug.Log($"[sragon000][릴리스 검증] {label} 테스트를 시작합니다.");
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log(
                    $"[sragon000][릴리스 검증][{currentLabel}] status={result.TestStatus}, " +
                    $"pass={result.PassCount}, fail={result.FailCount}, skip={result.SkipCount}, " +
                    $"duration={result.Duration:F2}s");
                runner = null;
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Passed) return;
                Debug.LogError($"[sragon000][릴리스 검증][실패] {result.FullName}: {result.Message}\n{result.StackTrace}");
            }
        }
    }
}
