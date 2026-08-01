using System;
using System.Collections.Generic;

namespace Narthex.Tools
{
    public static class PrometheusToolMenuPaths
    {
        public const string Root = "sragon000/";
        public const string Toolkit = Root + "Prometheus Scene Toolkit";
        public const string Ai = Root + "AI Toolkit/";
        public const string Validation = Root + "Validation/";
        public const string Tests = Root + "Tests/Tutorial/";
        public const string Analysis = Root + "Analysis/Tutorial/";
        public const string Legacy = Root + "Legacy/Tutorial Migration/";
    }

    public enum PrometheusToolCategory
    {
        Validation,
        Test,
        Analysis,
        Migration,
        Art
    }

    public sealed class PrometheusToolDescriptor
    {
        public PrometheusToolDescriptor(
            string label,
            string menuPath,
            PrometheusToolCategory category,
            bool mutatesScene)
        {
            Label = label;
            MenuPath = menuPath;
            Category = category;
            MutatesScene = mutatesScene;
        }

        public string Label { get; }
        public string MenuPath { get; }
        public PrometheusToolCategory Category { get; }
        public bool MutatesScene { get; }
    }

    public static class PrometheusToolRegistry
    {
        public static IReadOnlyList<PrometheusToolDescriptor> Tools { get; } =
            new[]
            {
                Tool("활성 튜토리얼 씬 검증",
                    PrometheusToolMenuPaths.Validation + "Validate Active Tutorial Scene",
                    PrometheusToolCategory.Validation),
                Tool("훈련장 마커 검증",
                    PrometheusToolMenuPaths.Validation + "Validate Training Marker Layout",
                    PrometheusToolCategory.Validation),
                Tool("훈련장 런타임 스모크",
                    PrometheusToolMenuPaths.Tests + "Training Runtime Smoke",
                    PrometheusToolCategory.Test),
                Tool("G 환경 런타임 스모크",
                    PrometheusToolMenuPaths.Tests + "G Environment Runtime Smoke",
                    PrometheusToolCategory.Test),
                Tool("훈련장 플레이 테스트",
                    PrometheusToolMenuPaths.Tests + "Imported Training",
                    PrometheusToolCategory.Test),
                Tool("전체 튜토리얼 플레이 테스트",
                    PrometheusToolMenuPaths.Tests + "Full Tutorial",
                    PrometheusToolCategory.Test),
                Tool("숨겨진 방 플레이 테스트",
                    PrometheusToolMenuPaths.Tests + "Hidden Room",
                    PrometheusToolCategory.Test),
                Tool("G→H 플레이 테스트",
                    PrometheusToolMenuPaths.Tests + "G Wind To H",
                    PrometheusToolCategory.Test),
                Tool("개발자 스킵 플레이 테스트",
                    PrometheusToolMenuPaths.Tests + "Developer Section Skip",
                    PrometheusToolCategory.Test),
                Tool("훈련장 구조 출력",
                    PrometheusToolMenuPaths.Analysis + "Print Training Structure",
                    PrometheusToolCategory.Analysis),
                Tool("헬테 구역 분석",
                    PrometheusToolMenuPaths.Analysis + "Analyze Helte Area",
                    PrometheusToolCategory.Analysis),

                Migration("Chapter 0 A-B Notion 개정 적용",
                    "Apply Notion Chapter0 A-B Revision"),
                Migration("요청 기능 일괄 적용",
                    "Apply Requested Gameplay Features"),
                Migration("복도 1차 연동 적용", "Apply Corridor Integration"),
                Migration("숨겨진 방 상승기류 보정", "Repair Hidden Room Updraft"),
                Migration("테우스 빛·긴급 전환 적용", "Apply Emergency Transition"),
                Migration("회의장 복귀 적용", "Apply Meeting Return"),
                Migration("사다리·외부 연동 적용", "Apply Exterior Integration"),
                Migration("F 전투 연동 적용", "Apply Encounter F Integration"),
                Migration("F 적 배치 추천값 초기화", "Reset Encounter F Enemy Layout"),
                Migration("G 전투 연동 적용", "Apply Encounter G Integration"),
                Migration("G 적 배치 추천값 초기화", "Reset Encounter G Enemy Layout"),
                Migration("G 환경 위험 연동 적용", "Apply G Environment Hazards"),
                Migration("헬테 조우 연동 적용", "Apply Helte Encounter"),
                Migration("훈련장 1차 연동 적용", "Apply Training Integration"),
                Migration("훈련장 마커 생성", "Create Training Markers"),
                Migration("훈련장 추천 배치 초기화", "Reset Training Marker Layout"),
                Migration("개발자 구간 스킵 적용", "Apply Developer Section Skip"),

                Tool("PNG 시퀀스 적용",
                    PrometheusToolMenuPaths.Root + "Art/Character PNG Sequence Setup",
                    PrometheusToolCategory.Art),
                Tool("스프라이트 시트 생성",
                    PrometheusToolMenuPaths.Root + "Art/Sprite Sheet Animation Builder",
                    PrometheusToolCategory.Art)
            };

        private static PrometheusToolDescriptor Tool(
            string label,
            string menuPath,
            PrometheusToolCategory category) =>
            new(label, menuPath, category, false);

        private static PrometheusToolDescriptor Migration(string label, string command) =>
            new(label, PrometheusToolMenuPaths.Legacy + command, PrometheusToolCategory.Migration, true);
    }
}
