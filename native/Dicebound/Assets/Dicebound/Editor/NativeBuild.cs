using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Dicebound.Presentation;

namespace Dicebound.Editor
{
    public static class NativeBuild
    {
        [MenuItem("Dicebound/Configure project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Settings");Directory.CreateDirectory("Assets/Scenes");AssetDatabase.Refresh();
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/DiceboundPipeline.asset");
            if(!pipeline){var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Settings/DiceboundRenderer.asset");pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/Settings/DiceboundPipeline.asset");}
            pipeline.msaaSampleCount=4;pipeline.renderScale=1;pipeline.supportsHDR=true;pipeline.shadowDistance=46;pipeline.mainLightShadowmapResolution=4096;pipeline.supportsCameraDepthTexture=true;
            pipeline.maxAdditionalLightsCount=8;pipeline.shadowCascadeCount=4;
            var lightingSettings=new SerializedObject(pipeline);lightingSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
            lightingSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
            lightingSettings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=2048;
            lightingSettings.FindProperty("m_SoftShadowsSupported").boolValue=true;lightingSettings.FindProperty("m_ReflectionProbeBoxProjection").boolValue=true;lightingSettings.ApplyModifiedProperties();
            var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/DiceboundRenderer.asset");
            rendererData.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if(!rendererData.postProcessData)throw new Exception("Missing URP post-processing resources.");
            var ambientOcclusion=rendererData.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if(!ambientOcclusion){ambientOcclusion=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ambientOcclusion.name="Soft contact occlusion";AssetDatabase.AddObjectToAsset(ambientOcclusion,rendererData);rendererData.rendererFeatures.Add(ambientOcclusion);}
            var occlusionSettings=new SerializedObject(ambientOcclusion);occlusionSettings.FindProperty("m_Settings.Intensity").floatValue=.65f;occlusionSettings.FindProperty("m_Settings.Radius").floatValue=.24f;occlusionSettings.FindProperty("m_Settings.Downsample").boolValue=true;occlusionSettings.ApplyModifiedProperties();rendererData.SetDirty();EditorUtility.SetDirty(rendererData);
            ambientOcclusion.SetActive(true);
            var glassCapture=rendererData.rendererFeatures.OfType<TacticalGlassRendererFeature>().FirstOrDefault();
            if(!glassCapture){glassCapture=ScriptableObject.CreateInstance<TacticalGlassRendererFeature>();glassCapture.name="Tactical glass backdrop";AssetDatabase.AddObjectToAsset(glassCapture,rendererData);rendererData.rendererFeatures.Add(glassCapture);}
            glassCapture.blurShader=Shader.Find("Hidden/Dicebound/TacticalGlassBlur");
            if(!glassCapture.blurShader)throw new Exception("Missing tactical backdrop blur shader.");
            glassCapture.downsample=2;glassCapture.blurRadius=1.75f;glassCapture.SetActive(true);
            var rangeOverlay=rendererData.rendererFeatures.OfType<TacticalRangeRendererFeature>().FirstOrDefault();
            if(!rangeOverlay){rangeOverlay=ScriptableObject.CreateInstance<TacticalRangeRendererFeature>();rangeOverlay.name="Readable tactical ranges after grading";AssetDatabase.AddObjectToAsset(rangeOverlay,rendererData);rendererData.rendererFeatures.Add(rangeOverlay);}
            rangeOverlay.depthCopyShader=Shader.Find("Hidden/Universal Render Pipeline/CopyDepth");
            if(!rangeOverlay.depthCopyShader)throw new Exception("Missing URP live-depth copy shader.");
            rangeOverlay.SetActive(true);
            rendererData.SetDirty();EditorUtility.SetDirty(rendererData);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=1;
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            for(int i=shaders.arraySize-1;i>=0;i--){var shader=shaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;if(shader&&shader.name=="GUI/Text Shader"){shaders.GetArrayElementAtIndex(i).objectReferenceValue=null;shaders.DeleteArrayElementAtIndex(i);}}
            foreach(string name in new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Complex Lit","Universal Render Pipeline/Unlit","Hidden/Universal Render Pipeline/CopyDepth","UI/Default","Dicebound/TacticalSprite","Dicebound/TacticalWater","Dicebound/PainterlyStone","Dicebound/PaintedSkill","Dicebound/PaintedGuideDepth","Dicebound/PaintedEnvironment","Dicebound/PaintedEnvironmentWater","Dicebound/TacticalRange","Dicebound/UI/CinematicInk","Dicebound/UI/TacticalGlass","Hidden/Dicebound/TacticalGlassBlur"}){
                var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader "+name);
                bool found=false;for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;
                if(!found){int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedProperties();
            PlayerSettings.companyName="Dicebound Studio";PlayerSettings.productName="Dicebound";PlayerSettings.bundleVersion="0.22.4";
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedProperties();}
            if(!File.Exists("Assets/Scenes/Duel.unity")){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,"Assets/Scenes/Duel.unity");}
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Duel.unity",true)};
            foreach(string path in Directory.GetFiles("Assets/Resources/Art","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/'))){
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);string key=Path.GetFileNameWithoutExtension(path);bool actor=path.Contains("/Heroes/")||path.Contains("/Enemies/");
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.sRGBTexture=true;importer.alphaIsTransparency=actor;importer.mipmapEnabled=true;importer.filterMode=actor?FilterMode.Trilinear:FilterMode.Bilinear;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.anisoLevel=4;importer.SaveAndReimport();
                if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new Exception("Not a loadable 2D texture: "+path);
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Sprites","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Textures","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.wrapMode=TextureWrapMode.Repeat;
                importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=2048;
                importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Vfx","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Cinematics","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/UI/Art","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            foreach(string jadeRoot in new[]{"Assets/Resources/Tactics/Jade","Assets/Resources/Tactics/JadeCraft"})
            if(Directory.Exists(jadeRoot))foreach(string path in Directory.GetFiles(jadeRoot,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=path.EndsWith("/route-plate-soft.png")||path.EndsWith("/route-city-reference.png")||path.EndsWith("/title-companions-back-v2.png")?FilterMode.Bilinear:FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            const string paintedRoot="Assets/Resources/Tactics/Painted";
            if(Directory.Exists(paintedRoot))foreach(string path in Directory.GetFiles(paintedRoot,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                bool depth=Path.GetFileNameWithoutExtension(path).Contains("depth"),data=depth||Path.GetFileNameWithoutExtension(path).Contains("mask");
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!data;
                importer.isReadable=depth;importer.npotScale=TextureImporterNPOTScale.None;
                importer.filterMode=data?FilterMode.Point:FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.mipmapEnabled=!data;importer.maxTextureSize=8192;importer.sRGBTexture=!data;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Environment/Polish","*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
                importer.sRGBTexture=false;importer.alphaIsTransparency=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;
                importer.anisoLevel=8;importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles("Assets/Resources/Tactics/Environment","*.hdr",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.TextureCube;
                importer.generateCubemap=TextureImporterGenerateCubemap.Spheremap;
                var reflectionImport=new TextureImporterSettings();importer.ReadTextureSettings(reflectionImport);
                reflectionImport.cubemapConvolution=TextureImporterCubemapConvolution.Specular;importer.SetTextureSettings(reflectionImport);
                importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.isReadable=false;
                importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=256,format=TextureImporterFormat.BC6H,compressionQuality=100});
                importer.SaveAndReimport();
                if(!AssetDatabase.LoadAssetAtPath<Cubemap>(path))throw new Exception("Not a reflection cubemap: "+path);
            }
            AssetDatabase.SaveAssets();Debug.Log("DICEBOUND_CONFIGURED");
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/Brand/icon-v1.png");if(icon)PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{icon});
        }
        [MenuItem("Dicebound/Build Windows")]
        public static void Windows()
        {
            Configure();Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Duel.unity"},locationPathName="Builds/Windows/Dicebound.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Debug.Log("DICEBOUND_BUILD "+report.summary.result+" "+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
        }

        [MenuItem("Dicebound/Build Jade visual preview")]
        public static void JadePreview()
        {
            Configure();Directory.CreateDirectory("Builds/JadePreview");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Duel.unity"},locationPathName="Builds/JadePreview/Dicebound.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Debug.Log("DICEBOUND_BUILD "+report.summary.result+" "+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Jade preview build failed: "+report.summary.result);
        }
    }
}
