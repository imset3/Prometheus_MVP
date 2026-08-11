using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusTutorialFPilotArtBuilder
    {
        const string ScenePath="Assets/Scenes/TutorialScene.unity";
        const string TilePng="Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/F_RustCargo/Generated";
        const string TileOut="Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/F_RustCargo/Tiles";
        const string Enemy="Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v1/TutorialGuard";
        const string EnemyOut=Enemy+"/UnityGenerated";
        const string VisualName="CharacterSprite_ART";
        static readonly string[] Roles={"Platform_Isolated","Platform_Left","Platform_Middle","Platform_Right","Block_TopLeft","Block_Top","Block_TopRight","Block_Fill","Wall_Left","Block_FillAlt","Wall_Right","Support_Pillar"};
        static readonly string[] Enemies={"ExteriorA_Enemy_01_ART_SLOT","ExteriorA_Enemy_02_ART_SLOT","ExteriorA_Enemy_03_ART_SLOT"};

        [MenuItem("sragon000/AI Toolkit/F Pilot Art/Dry Run")]
        public static void DryRun()=>Run(true);
        [MenuItem("sragon000/AI Toolkit/F Pilot Art/Apply")]
        public static void Apply()=>Run(false);

        static void Run(bool dry)
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException($"F pilot is restricted to {ScenePath}; active={scene.path}");
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var stage=One(scene,"F스테이지");
            var actors=Enemies.Select(n=>One(scene,n)).ToArray();
            var renderers=stage.GetComponentsInChildren<SpriteRenderer>(true);
            ValidateFiles();
            Debug.Log($"[Prometheus F Pilot] {(dry?"DRY RUN":"APPLY")} scene={scene.path}; platformRenderers={renderers.Length}; enemySlots={actors.Length}; tileAssets={Roles.Length}; clips=Work,Attack,Death");
            if(dry)return;

            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply F Pilot Art");
            try
            {
                var sprites=BuildTiles();
                ApplyPlatforms(renderers,sprites);
                var clips=BuildClips();
                var controller=BuildController(clips);
                foreach(var actor in actors) ApplyActor(actor,controller,clips);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                Debug.Log($"[Prometheus F Pilot] Applied platformRenderers={renderers.Length}; enemySlots={actors.Length}; scene intentionally left unsaved.");
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }

        static void ValidateFiles()
        {
            foreach(var r in Roles) Need($"{TilePng}/TUTO_F_{r}_v1.png");
            foreach(var m in new[]{"Walk","Attack","Die"})
                for(int i=0;i<8;i++) Need($"{Enemy}/Animations/{m}/Frames/TutorialGuard_{m}_{i:00}.png");
        }

        static Dictionary<string,Sprite> BuildTiles()
        {
            Folder(TileOut);
            var map=new Dictionary<string,Sprite>();
            foreach(var r in Roles)
            {
                string png=$"{TilePng}/TUTO_F_{r}_v1.png";
                Import(png,256,new Vector2(.5f,.5f));
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if(sprite==null)throw new InvalidOperationException($"Sprite import failed: {png}");
                map[r]=sprite;
                string path=$"{TileOut}/TUTO_F_{r}_v1.asset";
                var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
                if(tile==null){tile=ScriptableObject.CreateInstance<Tile>();AssetDatabase.CreateAsset(tile,path);}
                tile.name=$"TUTO_F_{r}_v1";tile.sprite=sprite;tile.color=Color.white;
                tile.transform=Matrix4x4.identity;tile.colliderType=Tile.ColliderType.Sprite;
                tile.flags=TileFlags.LockColor|TileFlags.LockTransform;EditorUtility.SetDirty(tile);
            }
            return map;
        }

        static void ApplyPlatforms(IReadOnlyList<SpriteRenderer> rs,IReadOnlyDictionary<string,Sprite> sprites)
        {
            float center=rs.Count==0?0:rs.Average(r=>r.transform.localPosition.x);
            foreach(var r in rs)
            {
                Undo.RecordObject(r,"Apply F platform art");Undo.RecordObject(r.transform,"Normalize F platform scale");
                var s=r.transform.localScale;float w=Mathf.Max(.01f,Mathf.Abs(s.x)),h=Mathf.Max(.01f,Mathf.Abs(s.y));
                string role=w>=h*1.75f?(h<=1.5f?"Platform_Middle":"Block_FillAlt"):
                    h>=w*1.75f?(w<=1.5f?"Support_Pillar":(r.transform.localPosition.x<=center?"Wall_Left":"Wall_Right")):
                    h<=1.5f?"Block_Top":"Block_Fill";
                r.sprite=sprites[role];r.color=Color.white;r.drawMode=SpriteDrawMode.Tiled;
                r.tileMode=SpriteTileMode.Continuous;r.size=new Vector2(w,h);
                r.transform.localScale=new Vector3(s.x<0?-1:1,s.y<0?-1:1,s.z);
                EditorUtility.SetDirty(r);EditorUtility.SetDirty(r.transform);
            }
        }

        static Dictionary<string,AnimationClip> BuildClips()
        {
            Folder(EnemyOut);
            var walk=Frames("Walk");var attack=Frames("Attack");var die=Frames("Die");
            var cadence=new[]{0,1,2,2,3,4,5,6,6,7}.Select(i=>walk[i]).ToArray();
            return new Dictionary<string,AnimationClip>{{"Work",Clip("Work",cadence,7,true)},{"Attack",Clip("Attack",attack,10,false)},{"Death",Clip("Death",die,8,false)}};
        }

        static Sprite[] Frames(string motion)
        {
            var result=new Sprite[8];
            for(int i=0;i<8;i++)
            {
                string p=$"{Enemy}/Animations/{motion}/Frames/TutorialGuard_{motion}_{i:00}.png";
                Import(p,256,new Vector2(.5f,0));
                result[i]=AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if(result[i]==null)throw new InvalidOperationException($"Sprite import failed: {p}");
            }
            return result;
        }

        static AnimationClip Clip(string name,IReadOnlyList<Sprite> frames,float fps,bool loop)
        {
            string p=$"{EnemyOut}/TutorialGuard_{name}.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if(clip==null){clip=new AnimationClip{name=name};AssetDatabase.CreateAsset(clip,p);}
            clip.frameRate=fps;
            var keys=new ObjectReferenceKeyframe[frames.Count+1];
            for(int i=0;i<frames.Count;i++)keys[i]=new ObjectReferenceKeyframe{time=i/fps,value=frames[i]};
            keys[frames.Count]=new ObjectReferenceKeyframe{time=frames.Count/fps,value=frames[frames.Count-1]};
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }

        static AnimatorController BuildController(IReadOnlyDictionary<string,AnimationClip> clips)
        {
            string p=$"{EnemyOut}/TutorialGuard.controller";
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(p)??AnimatorController.CreateAnimatorControllerAtPath(p);
            var sm=c.layers[0].stateMachine;
            foreach(var pair in clips)
            {
                var state=sm.states.Select(x=>x.state).FirstOrDefault(x=>x.name==pair.Key)??sm.AddState(pair.Key);
                state.motion=pair.Value;if(pair.Key=="Work")sm.defaultState=state;
            }
            EditorUtility.SetDirty(c);return c;
        }

        static void ApplyActor(GameObject actor,RuntimeAnimatorController controller,IReadOnlyDictionary<string,AnimationClip> clips)
        {
            Undo.RegisterFullObjectHierarchyUndo(actor,"Apply Tutorial Guard");
            var bind=Desc(actor.transform,"Visual_ART_BIND")??actor.transform;
            var old=bind.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.name!=VisualName).ToArray();
            var visual=Direct(bind,VisualName);
            if(visual==null){var go=new GameObject(VisualName);Undo.RegisterCreatedObjectUndo(go,"Create Tutorial Guard sprite");go.transform.SetParent(bind,false);visual=go.transform;}
            var sr=visual.GetComponent<SpriteRenderer>();
            if(sr==null)sr=Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            var animator=visual.GetComponent<Animator>();
            if(animator==null)animator=visual.gameObject.AddComponent<Animator>();
            if(animator==null)throw new InvalidOperationException($"Animator creation failed: {visual.name}");
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            sr.sprite=First(clips["Work"]);sr.color=Color.white;sr.sortingOrder=10;
            Fit(actor,visual,sr,old.FirstOrDefault());
            foreach(var r in old){Undo.RecordObject(r,"Disable enemy placeholder");r.enabled=false;EditorUtility.SetDirty(r);}
            var motion=actor.GetComponent<CombatVisualMotionHost>();
            var bridge=actor.GetComponent<CharacterPngAnimationBridge>()??Undo.AddComponent<CharacterPngAnimationBridge>(actor);
            if(!bridge.HasSetupBackup)bridge.CaptureSetupBackup(old,old.Select(_=>true).ToArray(),motion,motion==null||motion.enabled,actor.GetComponent<Collider2D>(),bind,old);
            var player=Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x=>x.scene==actor.scene&&x.CompareTag("Player"));
            bridge.Configure(CharacterPngAnimationPreset.Generic,animator,sr,actor.GetComponent<Rigidbody2D>(),null,null,null,
                actor.GetComponent<EnemyAttackHost>(),actor.GetComponent<CombatActorHost>(),null,motion,true,player!=null?player.transform:null,
                clips["Attack"].length,clips["Attack"].length,clips["Attack"].length);
            EditorUtility.SetDirty(bridge);
            if(motion!=null){Undo.RecordObject(motion,"Disable procedural enemy motion");motion.enabled=false;EditorUtility.SetDirty(motion);}
            var contract=actor.GetComponent<ArtReplacementContractHost>();
            if(contract!=null)
            {
                Undo.RecordObject(contract,"Bind Tutorial Guard art");
                var so=new SerializedObject(contract);so.FindProperty("visualRoot").objectReferenceValue=bind;
                var a=so.FindProperty("renderers");a.arraySize=1;a.GetArrayElementAtIndex(0).objectReferenceValue=sr;so.ApplyModifiedProperties();
            }
        }

        static void Fit(GameObject actor,Transform visual,SpriteRenderer sr,Renderer reference)
        {
            var box=actor.GetComponent<BoxCollider2D>();
            float height=box!=null?Mathf.Max(.5f,box.size.y):reference!=null?Mathf.Max(.5f,reference.bounds.size.y):2;
            float scale=height/Mathf.Max(.01f,sr.sprite.bounds.size.y);visual.localScale=new Vector3(scale,scale,1);
            visual.localPosition=box!=null?new Vector3(box.offset.x,box.offset.y-box.size.y*.5f-sr.sprite.bounds.min.y*scale,0):Vector3.zero;
        }

        static Sprite First(AnimationClip clip)
        {
            var b=AnimationUtility.GetObjectReferenceCurveBindings(clip).First();
            return AnimationUtility.GetObjectReferenceCurve(clip,b)[0].value as Sprite;
        }

        static void Import(string p,float ppu,Vector2 pivot)
        {
            AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
            if(!(AssetImporter.GetAtPath(p) is TextureImporter i))throw new InvalidOperationException($"No TextureImporter: {p}");
            var s=new TextureImporterSettings();i.ReadTextureSettings(s);s.spriteAlignment=(int)SpriteAlignment.Custom;s.spritePivot=pivot;s.spriteMeshType=SpriteMeshType.FullRect;
            i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=ppu;
            i.alphaIsTransparency=true;i.mipmapEnabled=false;i.sRGBTexture=true;i.filterMode=FilterMode.Bilinear;
            i.wrapMode=TextureWrapMode.Clamp;i.textureCompression=TextureImporterCompression.Uncompressed;i.SetTextureSettings(s);i.SaveAndReimport();
        }

        static GameObject One(Scene scene,string name)
        {
            var a=Resources.FindObjectsOfTypeAll<GameObject>().Where(x=>x.scene==scene&&x.name==name).ToArray();
            if(a.Length!=1)throw new InvalidOperationException($"Expected one {name}, found {a.Length}");return a[0];
        }
        static Transform Desc(Transform r,string n){if(r.name==n)return r;for(int i=0;i<r.childCount;i++){var f=Desc(r.GetChild(i),n);if(f!=null)return f;}return null;}
        static Transform Direct(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static void Folder(string p){var parts=p.Split('/');var cur=parts[0];foreach(var x in parts.Skip(1)){var next=$"{cur}/{x}";if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(cur,x);cur=next;}}
        static void Need(string p){var root=Directory.GetParent(Application.dataPath)?.FullName??throw new InvalidOperationException();if(!File.Exists(Path.Combine(root,p)))throw new FileNotFoundException("Missing F pilot asset",p);}
    }
}
