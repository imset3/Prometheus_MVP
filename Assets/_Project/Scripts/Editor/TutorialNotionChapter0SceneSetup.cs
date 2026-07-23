using System;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class TutorialNotionChapter0SceneSetup
    {
        private const string MenuPath = "Narthex/Tutorial/Apply Notion Chapter0 A-B Revision";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item != null && item.scene.IsValid())
                .ToArray();
            GameObject Find(string name) => sceneObjects.FirstOrDefault(item => item.name == name);

            var stageSystems = Require(Find("StageSystems"), "StageSystems");
            var player = Require(Find("PlayerRoot"), "PlayerRoot");
            var hq = Require(Find("Z01_HQ_Prologue"), "Z01_HQ_Prologue");
            var tutorialLevelRoot = Require(Find("TutorialLevelRoot"), "TutorialLevelRoot");
            var hud = Require(Find("TutorialHUD"), "TutorialHUD");
            var guide = Require(Find("TutorialGuideCompanion"), "TutorialGuideCompanion");
            var beacon = Require(Find("TutorialObjectiveBeacon"), "TutorialObjectiveBeacon");
            var cameraObject = Require(Find("Main Camera"), "Main Camera");
            var routeObject = Require(Find("HQGuideRouteController"), "HQGuideRouteController");

            var sourceMaterial = Require(Find("HQ_MainHallFloor"), "HQ_MainHallFloor")
                .GetComponent<Renderer>().sharedMaterial;
            var roomMaterial = GetOrCreateMaterial(
                "Assets/_Project/Art/Materials/TutorialHiddenRoom.mat",
                sourceMaterial,
                Color.white);
            var edgeMaterial = GetOrCreateMaterial(
                "Assets/_Project/Art/Materials/TutorialHiddenRoomEdge.mat",
                sourceMaterial,
                Color.white);
            var passkeyMaterial = GetOrCreateMaterial(
                "Assets/_Project/Art/Materials/TutorialPasskey.mat",
                sourceMaterial,
                Color.white);
            var alarmMaterial = GetOrCreateMaterial(
                "Assets/_Project/Art/Materials/TutorialTheusAlarm.mat",
                sourceMaterial,
                Color.white);

            var hiddenRoot = GetOrCreate(tutorialLevelRoot.transform, "Z01B_HiddenGlideRoom");
            hiddenRoot.transform.position = Vector3.zero;
            var geometry = GetOrCreate(hiddenRoot.transform, "GeometryRoot");
            var narrative = GetOrCreate(hiddenRoot.transform, "NarrativeRoot");
            var gameplay = GetOrCreate(hiddenRoot.transform, "GameplayRoot");
            var anchors = GetOrCreate(hiddenRoot.transform, "Anchors");

            CreateVisualCube(geometry.transform, "HiddenRoom_Backdrop_ART_SLOT", new Vector3(-199f, 1f, 3f),
                new Vector3(32f, 11f, 0.5f), roomMaterial);
            CreateSolidCube(geometry.transform, "HiddenRoom_LeftPlatform", new Vector3(-210f, -0.4f, 0f),
                new Vector3(10f, 1.2f, 1f), roomMaterial);
            CreateSolidCube(geometry.transform, "HiddenRoom_RightPlatform", new Vector3(-187.5f, -0.4f, 0f),
                new Vector3(7f, 1.2f, 1f), roomMaterial);
            CreateSolidCube(geometry.transform, "HiddenRoom_LeftWall", new Vector3(-215f, 2.5f, 0f),
                new Vector3(1f, 7f, 1f), roomMaterial);
            CreateSolidCube(geometry.transform, "HiddenRoom_RightWall", new Vector3(-184f, 2.5f, 0f),
                new Vector3(1f, 7f, 1f), roomMaterial);
            CreateVisualCube(geometry.transform, "HiddenRoom_LedgeMarker_ART_SLOT", new Vector3(-204.5f, 0.32f, -0.1f),
                new Vector3(0.35f, 1.2f, 0.4f), edgeMaterial);

            var updraft = GetOrCreate(gameplay.transform, "Updraft_ART_SLOT");
            for (var i = 0; i < 5; i++)
            {
                var x = -202f + i * 2.5f;
                var strip = CreateVisualCube(updraft.transform, $"WindStrip_{i + 1:00}", new Vector3(x, 0.25f, -0.15f),
                    new Vector3(0.18f, 7.2f + i % 2, 0.2f), edgeMaterial);
                strip.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -8f : 8f);
            }

            var passkey = CreateVisualCube(gameplay.transform, "AirshipPasskey_ART_SLOT", new Vector3(-187.5f, 1.25f, -0.2f),
                new Vector3(0.85f, 0.4f, 0.25f), passkeyMaterial);
            CreateVisualCube(passkey.transform, "PasskeyTooth_ART_SLOT", new Vector3(-186.9f, 1.25f, -0.2f),
                new Vector3(0.45f, 0.18f, 0.25f), passkeyMaterial);

            var hiddenSpawn = GetOrCreateAnchor(anchors.transform, "HiddenRoomSpawn", new Vector3(-212f, 0.8f, 0f));
            var ledgeTarget = GetOrCreateAnchor(anchors.transform, "LedgeStop", new Vector3(-206f, 0.8f, 0f));
            var passkeyTarget = GetOrCreateAnchor(anchors.transform, "PasskeyTarget", new Vector3(-187.5f, 1.25f, 0f));
            var hiddenReturnTarget = GetOrCreateAnchor(anchors.transform, "HiddenReturnTarget", new Vector3(-212.5f, 0.8f, 0f));
            var passkeyTrigger = CreateTrigger(gameplay.transform, "PasskeyPickupTrigger", passkeyTarget.position, new Vector2(1.8f, 2.5f));
            var ledgeTrigger = CreateTrigger(gameplay.transform, "LedgeBriefingTrigger", ledgeTarget.position, new Vector2(1.4f, 4f));
            var returnTrigger = CreateTrigger(gameplay.transform, "HiddenRoomReturnTrigger", hiddenReturnTarget.position, new Vector2(1.6f, 4f));

            var hqGameplay = RequireChild(hq.transform, "GameplayRoot");
            var hqAnchors = RequireChild(hq.transform, "Anchors");
            var hiddenEntryTarget = GetOrCreateAnchor(hqAnchors, "HiddenRoomEntryTarget", new Vector3(-38f, 1f, 0f));
            var hiddenEntryTrigger = CreateTrigger(hqGameplay, "HiddenRoomEntryTrigger", hiddenEntryTarget.position, new Vector2(1.8f, 5f));
            var meetingReturn = GetOrCreateAnchor(hqAnchors, "MeetingReturnSpawn", new Vector3(-30f, 1f, 0f));
            var exitTransform = RequireChild(hqAnchors, "ExitTrigger");
            var transitionTrigger = Require(exitTransform.GetComponent<Collider2D>(), "HQ ExitTrigger Collider2D");

            var flashlight = CreateVisualCube(guide.transform, "TheusFlashlight_ART_SLOT", guide.transform.position + new Vector3(1.8f, 0f, 0.2f),
                new Vector3(3.4f, 0.45f, 0.12f), passkeyMaterial);
            flashlight.transform.SetParent(guide.transform, true);
            var alarm = CreateVisualCube(guide.transform, "TheusWrongWayAlarm_ART_SLOT", guide.transform.position + new Vector3(0f, 0f, -0.2f),
                new Vector3(1.45f, 1.45f, 0.14f), alarmMaterial);
            alarm.transform.SetParent(guide.transform, true);
            flashlight.SetActive(false);
            alarm.SetActive(false);

            var glideUi = CreateGlideInstruction(hud.transform);
            var fade = Require(Find("TutorialZoneFadeOverlay"), "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>();
            var introCard = stageSystems.GetComponent<TutorialDialoguePresenter>();
            ConfigureIntroductionCard(introCard);
            ConfigureNarrative(stageSystems.GetComponent<TutorialNarrativeSequenceHost>());
            ConfigureRewardDialogue(stageSystems.GetComponent<TutorialNarrativeSequenceHost>());

            var flow = stageSystems.GetComponent<TutorialChapter0IntroFlowHost>();
            if (flow == null) flow = stageSystems.AddComponent<TutorialChapter0IntroFlowHost>();
            SetReference(flow, "serviceRoot", stageSystems.GetComponent<Narthex.Core.ServiceRoot>());
            SetReference(flow, "saveSystemHost", stageSystems.GetComponent<SaveSystemHost>());
            SetReference(flow, "questSequenceHost", stageSystems.GetComponent<TutorialQuestSequenceHost>());
            SetReference(flow, "dialoguePresenter", introCard);
            SetReference(flow, "playerInputHost", player.GetComponent<PlayerInputHost>());
            SetReference(flow, "playerMotorHost", player.GetComponent<PlayerMotorHost>());
            SetReference(flow, "cameraFollowHost", cameraObject.GetComponent<CameraFollowHost>());
            SetReference(flow, "guideCompanion", guide.GetComponent<TutorialGuideCompanionHost>());
            SetReference(flow, "legacyGuideRoute", routeObject.GetComponent<TutorialGuideRouteHost>());
            SetReference(flow, "objectiveBeacon", beacon.GetComponent<TutorialObjectiveBeaconHost>());
            SetReference(flow, "player", player.transform);
            SetReference(flow, "playerBody", player.GetComponent<Rigidbody2D>());
            SetReference(flow, "playerCollider", player.GetComponent<Collider2D>());
            SetReference(flow, "fadeCanvasGroup", fade);
            SetReference(flow, "trainingExitTransitionTrigger", transitionTrigger);
            SetReference(flow, "hiddenRoomRoot", hiddenRoot);
            SetReference(flow, "hiddenRoomSpawn", hiddenSpawn);
            SetReference(flow, "meetingReturnSpawn", meetingReturn);
            SetReference(flow, "hiddenRoomEntryTarget", hiddenEntryTarget);
            SetReference(flow, "ledgeTarget", ledgeTarget);
            SetReference(flow, "passkeyTarget", passkeyTarget);
            SetReference(flow, "hiddenRoomReturnTarget", hiddenReturnTarget);
            SetReference(flow, "trainingExitTarget", exitTransform);
            SetReference(flow, "hiddenRoomEntryTrigger", hiddenEntryTrigger);
            SetReference(flow, "ledgeTrigger", ledgeTrigger);
            SetReference(flow, "passkeyTrigger", passkeyTrigger);
            SetReference(flow, "hiddenRoomReturnTrigger", returnTrigger);
            SetReference(flow, "passkeyVisual", passkey);
            SetReference(flow, "theusFlashlightVisual", flashlight);
            SetReference(flow, "wrongWayAlarmVisual", alarm);
            SetReference(flow, "glideInstructionRoot", glideUi.root);
            SetReference(flow, "glideKeyVisual", glideUi.keyRect);
            SetReference(flow, "updraftVisual", updraft);
            var flowObject = new SerializedObject(flow);
            flowObject.FindProperty("wrongWayThresholdX").floatValue = -18f;
            flowObject.FindProperty("updraftMin").vector2Value = new Vector2(-204f, -5f);
            flowObject.FindProperty("updraftMax").vector2Value = new Vector2(-190f, 3.8f);
            flowObject.ApplyModifiedPropertiesWithoutUndo();

            routeObject.GetComponent<TutorialGuideRouteHost>().enabled = false;
            hiddenRoot.SetActive(false);
            glideUi.root.SetActive(false);
            transitionTrigger.enabled = true;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Applied Notion Chapter0 A/B revision: meeting rewrite, hidden glide room, passkey, return route, and save-safe progression.");
        }

        private static void ConfigureIntroductionCard(TutorialDialoguePresenter presenter)
        {
            var presenterObject = new SerializedObject(presenter);
            var definitions = presenterObject.FindProperty("introductionDefinitions");
            for (var i = 0; i < definitions.arraySize; i++)
            {
                var definition = definitions.GetArrayElementAtIndex(i);
                if (definition.FindPropertyRelative("questId").stringValue != "QST-TUTO-001") continue;
                definition.FindPropertyRelative("showAfterDialogue").boolValue = true;
                definition.FindPropertyRelative("description").stringValue =
                    "아다마스의 멤버이자 프로메의 여행을 돕는 AI.\n주변 환경과 물건을 분석하고 목적지까지 동행한다.";
            }
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            var card = FindSceneComponent<DialogueIntroductionCardModule>();
            var cardObject = new SerializedObject(card);
            var root = cardObject.FindProperty("cardRoot").objectReferenceValue as GameObject;
            if (root == null) throw new InvalidOperationException("Introduction card root is missing.");
            var canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
            cardObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            cardObject.FindProperty("cardRect").objectReferenceValue = root.GetComponent<RectTransform>();
            cardObject.FindProperty("promptDelay").floatValue = 3f;
            cardObject.FindProperty("collapseDuration").floatValue = 0.24f;
            cardObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNarrative(TutorialNarrativeSequenceHost narrative)
        {
            var serialized = new SerializedObject(narrative);
            var beats = serialized.FindProperty("beats");
            for (var i = 0; i < beats.arraySize; i++)
            {
                var beat = beats.GetArrayElementAtIndex(i);
                if (beat.FindPropertyRelative("questId").stringValue != "QST-TUTO-001") continue;
                beat.FindPropertyRelative("stageId").stringValue = "아다마스 본부 · TUTO_A_01";
                SetLines(beat.FindPropertyRelative("lines"), new[]
                {
                    "시스템: 제니스의 판도라 공장이 평소와 다른 낌새를 보인 지 일주일째가 지났다.",
                    "에온: 아르온의 정보대로 제니스가 분주한 모습을 보이기 시작했어.",
                    "에온: 프로메, 작전을 실행할 때가 됐어.",
                    "에온: 프로메, 너에게 큰 부담을 준 것 같아 미안하구나.",
                    "프로메: 나도 오랫동안 준비해 온 일이야. 미안한 마음 갖지 마.",
                    "아르온: 프로메, 나가기 전에 훈련장에서 마지막 훈련을 해 보는 건 어때?",
                    "아르온: 길은 테우스가 알려 줄 거야.",
                    "아르온: 맞다. 제니스행 비행선 패스키를 챙기는 것도 잊지 말고.",
                    "테우스: 안녕, 프로메!",
                    "테우스: 일단 비행선 패스키부터 챙기러 가 보자!"
                });
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRewardDialogue(TutorialNarrativeSequenceHost narrative)
        {
            var serialized = new SerializedObject(narrative);
            var beats = serialized.FindProperty("beats");
            for (var i = 0; i < beats.arraySize; i++)
            {
                var beat = beats.GetArrayElementAtIndex(i);
                var questId = beat.FindPropertyRelative("questId").stringValue;
                if (questId == "QST-TUTO-007")
                {
                    SetLines(beat.FindPropertyRelative("lines"), new[]
                    {
                        "테우스: 이야, 바깥 공기는 정말 오랜만이야!",
                        "프로메: 너는 로봇인데 공기가 느껴져?",
                        "테우스: 사소한 건 넘어가고 저 비행선을 봐. 숨겨진 방에서 챙긴 패스키로 제니스까지 갈 수 있어.",
                        "테우스: 헬테가 판도라 공장 항로 도면과 접근 정보를 가지고 있어. 외곽 경비들이 먼저 접근 중이야.",
                        "프로메: 길을 막는 적부터 처리하고 헬테를 만나러 가자.",
                        "테우스: 좋아. 이 통로는 일자야. 앞의 릴레이를 열고 그대로 전진하면 돼."
                    });
                }
                else if (questId == "QST-TUTO-008")
                {
                    SetLines(beat.FindPropertyRelative("lines"), new[]
                    {
                        "테우스: 광물 저장고에 헬테가 있어. 항로 도면과 판도라 공장 접근 정보를 확인해야 해.",
                        "프로메: 여기까지 와서 돌아갈 수는 없어.",
                        "헬테: 아다마스의 아이가 여기까지 들어왔군.",
                        "프로메: 길을 비켜 줘. 우리는 판도라 공장으로 가야 해."
                    });
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (GameObject root, RectTransform keyRect) CreateGlideInstruction(Transform hud)
        {
            var root = GetOrCreateUi(hud, "HiddenRoomGlideInstruction");
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(600f, 92f);
            var image = GetOrAdd<Image>(root);
            image.color = Color.clear;
            image.raycastTarget = false;

            var title = GetOrCreateUi(root.transform, "InstructionText");
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(86f, 8f);
            titleRect.offsetMax = new Vector2(-16f, -8f);
            var titleText = GetOrAdd<Text>(title);
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 20;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;
            titleText.text = "SPACE 점프 → 키 떼기 → 공중에서 SPACE 길게 눌러 활공";

            var key = GetOrCreateUi(root.transform, "SpaceKey_ART_SLOT");
            var keyRect = (RectTransform)key.transform;
            keyRect.anchorMin = new Vector2(0f, 0.5f);
            keyRect.anchorMax = new Vector2(0f, 0.5f);
            keyRect.pivot = new Vector2(0.5f, 0.5f);
            keyRect.anchoredPosition = new Vector2(48f, 0f);
            keyRect.sizeDelta = new Vector2(66f, 48f);
            var keyImage = GetOrAdd<Image>(key);
            keyImage.color = new Color(0.18f, 0.76f, 0.86f, 1f);
            var keyLabel = GetOrCreateUi(key.transform, "Label");
            var keyLabelRect = (RectTransform)keyLabel.transform;
            keyLabelRect.anchorMin = Vector2.zero;
            keyLabelRect.anchorMax = Vector2.one;
            keyLabelRect.offsetMin = Vector2.zero;
            keyLabelRect.offsetMax = Vector2.zero;
            var keyText = GetOrAdd<Text>(keyLabel);
            keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            keyText.fontStyle = FontStyle.Bold;
            keyText.fontSize = 16;
            keyText.alignment = TextAnchor.MiddleCenter;
            keyText.color = Color.black;
            keyText.text = "SPACE";
            return (root, keyRect);
        }

        private static Material GetOrCreateMaterial(string path, Material source, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateVisualCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var child = parent.Find(name);
            var gameObject = child != null ? child.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            var collider3D = gameObject.GetComponent<Collider>();
            if (collider3D != null) UnityEngine.Object.DestroyImmediate(collider3D);
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return gameObject;
        }

        private static GameObject CreateSolidCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var gameObject = CreateVisualCube(parent, name, position, scale, material);
            var collider = gameObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = Vector2.one;
            return gameObject;
        }

        private static Collider2D CreateTrigger(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var gameObject = GetOrCreate(parent, name);
            gameObject.transform.position = position;
            var trigger = gameObject.GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = size;
            return trigger;
        }

        private static Transform GetOrCreateAnchor(Transform parent, string name, Vector3 position)
        {
            var gameObject = GetOrCreate(parent, name);
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private static GameObject GetOrCreate(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static GameObject GetOrCreateUi(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child == null) throw new InvalidOperationException($"Missing scene child '{parent.name}/{path}'.");
            return child;
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            var component = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(item => item != null && item.gameObject.scene.IsValid());
            if (component == null) throw new InvalidOperationException($"Missing scene component {typeof(T).Name}.");
            return component;
        }

        private static void SetLines(SerializedProperty lines, string[] values)
        {
            lines.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) lines.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            if (target == null || value == null) throw new InvalidOperationException($"Cannot assign {fieldName}: target or value is null.");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null) throw new InvalidOperationException($"{target.GetType().Name} is missing serialized field '{fieldName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Require<T>(T value, string label) where T : UnityEngine.Object
        {
            if (value == null) throw new InvalidOperationException($"Required object '{label}' is missing.");
            return value;
        }
    }
}
