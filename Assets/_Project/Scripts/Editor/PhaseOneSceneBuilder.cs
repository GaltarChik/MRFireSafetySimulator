using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Analytics.Services;
using MRFireSafety.Core;
using MRFireSafety.Core.Validation;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using MRFireSafety.Suppression.Handlers;
using MRFireSafety.UI.Controllers;
using TMPro;

namespace MRFireSafety.Editor
{
    /// <summary>
    /// Creates the lightweight Phase 1 visual prototype scene for the MR Fire Safety Training Simulator.
    /// The generated server rack and fire use low-poly primitives so that the scene remains suitable
    /// for subsequent optimization toward a 72 FPS mobile XR target.
    /// </summary>
    public static class PhaseOneSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/FireTraining.unity";
        private const string MaterialDirectory = "Assets/_Project/Art/Materials";

        /// <summary>
        /// Builds and opens a new Phase 1 training scene.
        /// </summary>
        [MenuItem("MR Fire Safety/Build Phase 1 Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(MaterialDirectory);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f);

            Material floorMaterial = GetOrCreateMaterial("TrainingFloor", new Color(0.18f, 0.2f, 0.23f), 0.85f, 0.05f);
            Material rackMaterial = GetOrCreateMaterial("ServerRack", new Color(0.07f, 0.09f, 0.12f), 0.65f, 0.15f);
            Material metalMaterial = GetOrCreateMaterial("RackMetal", new Color(0.28f, 0.32f, 0.36f), 0.8f, 0.65f);
            Material accentMaterial = GetOrCreateMaterial("WarningAccent", new Color(0.95f, 0.38f, 0.04f), 0.4f, 0.15f);
            Material extinguisherMaterial = GetOrCreateMaterial("ExtinguisherRed", new Color(0.72f, 0.025f, 0.02f), 0.55f, 0.35f);
            Material hoseMaterial = GetOrCreateMaterial("ExtinguisherHose", new Color(0.035f, 0.04f, 0.045f), 0.15f, 0.18f);

            GameObject sceneRoot = new GameObject("MRFireSafety_TrainingScene");
            sceneRoot.AddComponent<SessionDataManager>();
            sceneRoot.AddComponent<PerformanceProfiler>();
            sceneRoot.AddComponent<PerformanceConfigurationService>();
            sceneRoot.AddComponent<DeviceProfiler>();
            sceneRoot.AddComponent<TrainingSessionController>();
            sceneRoot.AddComponent<MrReadinessValidator>();
            CreateGround(sceneRoot.transform, floorMaterial);
            CreateServerRack(sceneRoot.transform, rackMaterial, metalMaterial, accentMaterial);
            CreateFireVisual(sceneRoot.transform);
            CreateSmokeVisual(sceneRoot.transform);
            CreateExtinguisherPreview(sceneRoot.transform, extinguisherMaterial, metalMaterial, hoseMaterial);
            CreateLighting(sceneRoot.transform);
            CreatePreviewCamera(sceneRoot.transform);
            CreateTrainingHud(sceneRoot.transform);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            Selection.activeGameObject = sceneRoot;
            Debug.Log("MR Fire Safety: Phase 1 scene created at " + ScenePath);
        }

        private static void CreateGround(Transform parentTransform, Material material)
        {
            GameObject groundObject = CreatePrimitive(PrimitiveType.Plane, "TrainingFloor", parentTransform, Vector3.zero, new Vector3(1.5f, 1f, 1.5f), material);
            groundObject.tag = "Untagged";
        }

        private static void CreateServerRack(Transform parentTransform, Material rackMaterial, Material metalMaterial, Material accentMaterial)
        {
            Transform rackTransform = new GameObject("VirtualServerRack").transform;
            rackTransform.SetParent(parentTransform);
            rackTransform.position = new Vector3(0f, 0f, 1.1f);

            CreatePrimitive(PrimitiveType.Cube, "RackBody", rackTransform, new Vector3(0f, 0.9f, 0f), new Vector3(0.72f, 1.8f, 0.54f), rackMaterial);
            CreatePrimitive(PrimitiveType.Cube, "RackTop", rackTransform, new Vector3(0f, 1.84f, 0f), new Vector3(0.82f, 0.08f, 0.64f), metalMaterial);

            CreateRackPost(rackTransform, new Vector3(-0.36f, 0.9f, -0.26f), metalMaterial);
            CreateRackPost(rackTransform, new Vector3(0.36f, 0.9f, -0.26f), metalMaterial);
            CreateRackPost(rackTransform, new Vector3(-0.36f, 0.9f, 0.26f), metalMaterial);
            CreateRackPost(rackTransform, new Vector3(0.36f, 0.9f, 0.26f), metalMaterial);

            for (int unitIndex = 0; unitIndex < 5; unitIndex++)
            {
                float unitHeight = 0.34f + (unitIndex * 0.28f);
                CreatePrimitive(PrimitiveType.Cube, "ServerUnit_" + unitIndex, rackTransform, new Vector3(0f, unitHeight, -0.285f), new Vector3(0.58f, 0.2f, 0.035f), metalMaterial);
                CreatePrimitive(PrimitiveType.Cube, "StatusLight_" + unitIndex, rackTransform, new Vector3(0.21f, unitHeight, -0.31f), new Vector3(0.045f, 0.045f, 0.012f), accentMaterial);
            }

            BoxCollider fireTargetCollider = rackTransform.gameObject.AddComponent<BoxCollider>();
            fireTargetCollider.center = new Vector3(0f, 1f, 0f);
            fireTargetCollider.size = new Vector3(0.8f, 1.9f, 0.65f);
            fireTargetCollider.isTrigger = true;
            rackTransform.gameObject.AddComponent<FireObjectIntegrityController>();
        }

        private static void CreateRackPost(Transform parentTransform, Vector3 localPosition, Material material)
        {
            CreatePrimitive(PrimitiveType.Cube, "RackPost", parentTransform, localPosition, new Vector3(0.055f, 1.8f, 0.055f), material);
        }

        private static void CreateFireVisual(Transform parentTransform)
        {
            GameObject fireObject = new GameObject("FireVisual");
            fireObject.transform.SetParent(parentTransform);
            fireObject.transform.position = new Vector3(0f, 0.92f, 1.38f);

            ParticleSystem particleSystem = fireObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.loop = true;
            mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            mainModule.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            mainModule.startColor = new Color(1f, 0.35f, 0.02f, 0.9f);
            mainModule.maxParticles = 96;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
            emissionModule.rateOverTime = 34f;

            ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;
            shapeModule.radius = 0.18f;
            shapeModule.angle = 18f;

            ParticleSystem.ColorOverLifetimeModule colorModule = particleSystem.colorOverLifetime;
            colorModule.enabled = true;
            Gradient colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.01f), 0.55f),
                    new GradientColorKey(new Color(0.16f, 0.03f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorModule.color = colorGradient;

            Light fireLight = fireObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.28f, 0.04f);
            fireLight.intensity = 3.5f;
            fireLight.range = 3.2f;
            fireLight.shadows = LightShadows.None;

            fireObject.AddComponent<FirePropagationSystem>();
            fireObject.AddComponent<FireVisualController>();
        }

        private static void CreateLighting(Transform parentTransform)
        {
            GameObject lightObject = new GameObject("DirectionalLight");
            lightObject.transform.SetParent(parentTransform);
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.1f;
            directionalLight.color = new Color(0.72f, 0.82f, 1f);
            directionalLight.shadows = LightShadows.Soft;
        }

        private static void CreateExtinguisherPreview(Transform parentTransform, Material extinguisherMaterial, Material metalMaterial, Material hoseMaterial)
        {
            Transform extinguisherTransform = new GameObject("VirtualExtinguisher").transform;
            extinguisherTransform.SetParent(parentTransform);
            extinguisherTransform.position = new Vector3(-1.1f, 0.62f, 0.35f);
            extinguisherTransform.rotation = Quaternion.Euler(0f, 28f, 0f);

            CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherBody", extinguisherTransform, Vector3.zero, new Vector3(0.22f, 0.5f, 0.22f), extinguisherMaterial);
            CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherCollar", extinguisherTransform, new Vector3(0f, 0.5f, 0f), new Vector3(0.18f, 0.06f, 0.18f), metalMaterial);
            CreatePrimitive(PrimitiveType.Cube, "ExtinguisherHandle", extinguisherTransform, new Vector3(0f, 0.66f, 0.03f), new Vector3(0.12f, 0.05f, 0.3f), metalMaterial);
            CreatePrimitive(PrimitiveType.Cube, "ExtinguisherGrip", extinguisherTransform, new Vector3(0f, 0.76f, -0.02f), new Vector3(0.14f, 0.05f, 0.24f), metalMaterial);

            Transform nozzleTransform = CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherNozzle", extinguisherTransform, new Vector3(0.08f, 0.58f, 0.45f), new Vector3(0.05f, 0.24f, 0.05f), hoseMaterial).transform;
            nozzleTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherHose", extinguisherTransform, new Vector3(0.11f, 0.52f, 0.22f), new Vector3(0.035f, 0.26f, 0.035f), hoseMaterial).transform.localRotation = Quaternion.Euler(62f, 0f, 0f);

            ParticleSystem agentParticleSystem = CreateAgentParticleStream(nozzleTransform);
            extinguisherTransform.gameObject.AddComponent<SuppressionRaycastController>();
            extinguisherTransform.gameObject.AddComponent<AgentSuppressionManager>();
            agentParticleSystem.gameObject.AddComponent<ParticleCollisionHandler>();
        }

        private static ParticleSystem CreateAgentParticleStream(Transform nozzleTransform)
        {
            GameObject agentObject = new GameObject("ExtinguishingAgentStream");
            agentObject.transform.SetParent(nozzleTransform);
            agentObject.transform.localPosition = Vector3.forward * 0.22f;
            agentObject.transform.localRotation = Quaternion.identity;

            ParticleSystem agentParticleSystem = agentObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule mainModule = agentParticleSystem.main;
            mainModule.loop = true;
            mainModule.startLifetime = 0.6f;
            mainModule.startSpeed = 5f;
            mainModule.startSize = 0.035f;
            mainModule.startColor = new Color(0.85f, 0.94f, 1f, 0.7f);
            mainModule.maxParticles = 128;
            mainModule.playOnAwake = false;

            ParticleSystem.EmissionModule emissionModule = agentParticleSystem.emission;
            emissionModule.rateOverTime = 80f;
            ParticleSystem.ShapeModule shapeModule = agentParticleSystem.shape;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;
            shapeModule.angle = 4f;
            shapeModule.radius = 0.02f;
            ParticleSystem.CollisionModule collisionModule = agentParticleSystem.collision;
            collisionModule.enabled = true;
            collisionModule.type = ParticleSystemCollisionType.World;
            collisionModule.mode = ParticleSystemCollisionMode.Collision3D;
            collisionModule.sendCollisionMessages = true;
            collisionModule.maxCollisionShapes = 32;
            return agentParticleSystem;
        }

        private static void CreateTrainingHud(Transform parentTransform)
        {
            GameObject canvasObject = new GameObject("TrainingHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parentTransform);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1440f, 900f);

            TMP_Text timerText = CreateText("Timer", canvasObject.transform, new Vector2(34f, -34f), new Vector2(360f, 62f), 30f, TextAlignmentOptions.Left, "TIME  00:00");
            TMP_Text statusText = CreateText("Status", canvasObject.transform, new Vector2(34f, -100f), new Vector2(520f, 62f), 22f, TextAlignmentOptions.Left, "STATUS  EXTINGUISH THE SOURCE");
            Slider agentSlider = CreateHudSlider("AgentSlider", canvasObject.transform, new Vector2(34f, -172f), "AGENT");
            Slider fireSlider = CreateHudSlider("FireSlider", canvasObject.transform, new Vector2(34f, -238f), "FIRE");
            Slider integritySlider = CreateHudSlider("IntegritySlider", canvasObject.transform, new Vector2(34f, -304f), "INTEGRITY");
            integritySlider.value = 1f;

            GameObject resultsPanel = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
            resultsPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelTransform = resultsPanel.GetComponent<RectTransform>();
            panelTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelTransform.sizeDelta = new Vector2(520f, 310f);
            Image panelImage = resultsPanel.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.03f, 0.055f, 0.94f);
            TMP_Text resultsText = CreateText("ResultsText", resultsPanel.transform, Vector2.zero, new Vector2(470f, 250f), 28f, TextAlignmentOptions.Center, "");
            TMP_Text historyText = CreateText("HistoryText", canvasObject.transform, new Vector2(-430f, -34f), new Vector2(400f, 190f), 17f, TextAlignmentOptions.TopLeft, "RECENT SESSIONS\nLoading...");
            RectTransform historyTransform = historyText.rectTransform;
            historyTransform.anchorMin = new Vector2(1f, 1f);
            historyTransform.anchorMax = new Vector2(1f, 1f);
            historyTransform.pivot = new Vector2(1f, 1f);

            TrainingHudController hudController = canvasObject.AddComponent<TrainingHudController>();
            hudController.Configure(timerText, statusText, agentSlider, fireSlider, integritySlider);
            SessionResultsPresenter resultsPresenter = canvasObject.AddComponent<SessionResultsPresenter>();
            resultsPresenter.Configure(resultsPanel, resultsText);
            SessionHistoryPresenter historyPresenter = canvasObject.AddComponent<SessionHistoryPresenter>();
            historyPresenter.Configure(historyText);
        }

        private static TMP_Text CreateText(string objectName, Transform parentTransform, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment, string content)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parentTransform, false);
            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0f, 1f);
            textTransform.anchorMax = new Vector2(0f, 1f);
            textTransform.pivot = new Vector2(0f, 1f);
            textTransform.anchoredPosition = anchoredPosition;
            textTransform.sizeDelta = size;
            TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.text = content;
            return textComponent;
        }

        private static Slider CreateHudSlider(string objectName, Transform parentTransform, Vector2 anchoredPosition, string label)
        {
            TMP_Text labelText = CreateText(label + "Label", parentTransform, anchoredPosition, new Vector2(140f, 30f), 18f, TextAlignmentOptions.Left, label);
            labelText.color = new Color(0.72f, 0.86f, 1f);

            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parentTransform, false);
            RectTransform sliderTransform = sliderObject.GetComponent<RectTransform>();
            sliderTransform.anchorMin = new Vector2(0f, 1f);
            sliderTransform.anchorMax = new Vector2(0f, 1f);
            sliderTransform.pivot = new Vector2(0f, 1f);
            sliderTransform.anchoredPosition = anchoredPosition + new Vector2(155f, -8f);
            sliderTransform.sizeDelta = new Vector2(230f, 20f);

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            RectTransform backgroundTransform = backgroundObject.GetComponent<RectTransform>();
            backgroundTransform.anchorMin = Vector2.zero;
            backgroundTransform.anchorMax = Vector2.one;
            backgroundTransform.offsetMin = Vector2.zero;
            backgroundTransform.offsetMax = Vector2.zero;

            GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaTransform = fillAreaObject.GetComponent<RectTransform>();
            fillAreaTransform.anchorMin = Vector2.zero;
            fillAreaTransform.anchorMax = Vector2.one;
            fillAreaTransform.offsetMin = new Vector2(2f, 2f);
            fillAreaTransform.offsetMax = new Vector2(-2f, -2f);
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.14f, 0.72f, 0.98f, 1f);
            RectTransform fillTransform = fillObject.GetComponent<RectTransform>();
            fillTransform.anchorMin = Vector2.zero;
            fillTransform.anchorMax = Vector2.one;
            fillTransform.offsetMin = Vector2.zero;
            fillTransform.offsetMax = Vector2.zero;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillTransform;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static void CreateSmokeVisual(Transform parentTransform)
        {
            GameObject smokeObject = new GameObject("SmokeVisual");
            smokeObject.transform.SetParent(parentTransform);
            smokeObject.transform.position = new Vector3(0f, 1.18f, 1.38f);

            ParticleSystem particleSystem = smokeObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.loop = true;
            mainModule.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
            mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            mainModule.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            mainModule.startColor = new Color(0.18f, 0.2f, 0.24f, 0.28f);
            mainModule.maxParticles = 32;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
            emissionModule.rateOverTime = 8f;

            ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;
            shapeModule.radius = 0.16f;
            shapeModule.angle = 12f;

            ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
            noiseModule.enabled = true;
            noiseModule.strength = 0.22f;
            noiseModule.frequency = 0.35f;
        }

        private static void CreatePreviewCamera(Transform parentTransform)
        {
            GameObject cameraObject = new GameObject("EditorPreviewCamera");
            cameraObject.transform.SetParent(parentTransform);
            cameraObject.transform.position = new Vector3(3.2f, 2.25f, -3.8f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.85f, 1f));

            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            cameraComponent.fieldOfView = 58f;
        }

        private static GameObject CreatePrimitive(PrimitiveType primitiveType, string objectName, Transform parentTransform, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitiveObject = GameObject.CreatePrimitive(primitiveType);
            primitiveObject.name = objectName;
            primitiveObject.transform.SetParent(parentTransform);
            primitiveObject.transform.localPosition = localPosition;
            primitiveObject.transform.localScale = localScale;
            primitiveObject.GetComponent<Renderer>().sharedMaterial = material;
            return primitiveObject;
        }

        private static Material GetOrCreateMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            string materialPath = MaterialDirectory + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader)
            {
                name = materialName
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}
