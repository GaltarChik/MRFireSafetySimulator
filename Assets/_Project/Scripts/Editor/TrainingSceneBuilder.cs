using System.IO;
using System.Linq;
using MRFireSafety.Analytics.Services;
using MRFireSafety.Analytics.Systems;
using MRFireSafety.Core;
using MRFireSafety.Core.Input;
using MRFireSafety.Core.Session;
using MRFireSafety.Core.Spatial;
using MRFireSafety.Core.Validation;
using MRFireSafety.Fire.Controllers;
using MRFireSafety.Fire.Systems;
using MRFireSafety.Suppression.Controllers;
using MRFireSafety.Suppression.Handlers;
using MRFireSafety.UI.Controllers;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MRFireSafety.Editor
{
    /// <summary>
    /// Generates the mixed-reality training scene: an AR Foundation rig with passthrough, the
    /// anchored virtual server rack, the controller-driven extinguisher, and the session systems.
    /// Every dependency is wired during generation, so the scene runs on a headset without manual
    /// inspector work. Geometry uses low-poly primitives to stay inside the mobile XR budget.
    /// </summary>
    public static class TrainingSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/FireTraining.unity";
        private const string MaterialDirectory = "Assets/_Project/Art/Materials";
        private const string InputActionsPath = "Assets/_Project/Input/FireExtinguisherControls.inputactions";
        private const string FireTargetLayerName = "FireTarget";

        /// <summary>
        /// Builds, saves, and opens the mixed-reality training scene, then makes it the first entry
        /// in the build settings.
        /// </summary>
        [MenuItem("MR Fire Safety/Build Training Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(MaterialDirectory);
            int fireTargetLayer = EnsureLayer(FireTargetLayerName);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.4f);

            Material rackMaterial = GetOrCreateMaterial("ServerRack", new Color(0.07f, 0.09f, 0.12f), 0.65f, 0.15f);
            Material metalMaterial = GetOrCreateMaterial("RackMetal", new Color(0.28f, 0.32f, 0.36f), 0.8f, 0.65f);
            Material accentMaterial = GetOrCreateMaterial("WarningAccent", new Color(0.95f, 0.38f, 0.04f), 0.4f, 0.15f);
            Material extinguisherMaterial = GetOrCreateMaterial("ExtinguisherRed", new Color(0.72f, 0.025f, 0.02f), 0.55f, 0.35f);
            Material hoseMaterial = GetOrCreateMaterial("ExtinguisherHose", new Color(0.035f, 0.04f, 0.045f), 0.15f, 0.18f);

            CreateSessionOrigin(out ARPlaneManager planeManager, out ARAnchorManager anchorManager, out Camera xrCamera, out Transform cameraOffsetTransform);
            Transform propTransform = CreateTrainingProp(rackMaterial, metalMaterial, accentMaterial, fireTargetLayer);
            Transform extinguisherTransform = CreateExtinguisher(extinguisherMaterial, metalMaterial, hoseMaterial, cameraOffsetTransform, fireTargetLayer);
            CreateTrainingHud(xrCamera.transform);
            CreateLighting();

            SpatialAwarenessSystem spatialAwarenessSystem = planeManager.GetComponent<SpatialAwarenessSystem>();
            SpatialAnchorService spatialAnchorService = anchorManager.GetComponent<SpatialAnchorService>();
            VirtualPropPlacementController placementController = planeManager.GetComponent<VirtualPropPlacementController>();
            placementController.Configure(spatialAwarenessSystem, spatialAnchorService, propTransform, xrCamera.transform);

            ControllerPoseProvider poseProvider = extinguisherTransform.GetComponent<ControllerPoseProvider>();
            poseProvider.Configure(
                CreateActionProperty("NozzlePosition"),
                CreateActionProperty("NozzleRotation"),
                extinguisherTransform,
                cameraOffsetTransform);

            CreateTrainingSystems();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            RegisterSceneInBuildSettings();
            Debug.Log($"MR Fire Safety: training scene created at {ScenePath}. Fire target layer: {FireTargetLayerName} ({fireTargetLayer}).");
        }

        private static void CreateSessionOrigin(out ARPlaneManager planeManager, out ARAnchorManager anchorManager, out Camera xrCamera, out Transform cameraOffsetTransform)
        {
            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            GameObject originObject = new GameObject("XR Origin");
            XROrigin xrOrigin = originObject.AddComponent<XROrigin>();

            GameObject cameraOffsetObject = new GameObject("Camera Offset");
            cameraOffsetObject.transform.SetParent(originObject.transform, false);
            cameraOffsetTransform = cameraOffsetObject.transform;

            GameObject cameraObject = new GameObject("Main Camera")
            {
                tag = "MainCamera"
            };
            cameraObject.transform.SetParent(cameraOffsetObject.transform, false);

            xrCamera = cameraObject.AddComponent<Camera>();
            xrCamera.clearFlags = CameraClearFlags.SolidColor;

            // Fully transparent clear lets the device compositor show the physical room behind the
            // virtual training prop.
            xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            xrCamera.nearClipPlane = 0.1f;
            xrCamera.farClipPlane = 25f;

            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            ConfigureHeadTracking(cameraObject.AddComponent<TrackedPoseDriver>());

            xrOrigin.Camera = xrCamera;
            xrOrigin.CameraFloorOffsetObject = cameraOffsetObject;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            planeManager = originObject.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            anchorManager = originObject.AddComponent<ARAnchorManager>();

            originObject.AddComponent<SpatialAwarenessSystem>();
            originObject.AddComponent<SpatialAnchorService>();
            originObject.AddComponent<VirtualPropPlacementController>();
            originObject.AddComponent<MixedRealityBootstrapper>();
        }

        private static void ConfigureHeadTracking(TrackedPoseDriver trackedPoseDriver)
        {
            trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            trackedPoseDriver.positionInput = new InputActionProperty(
                new InputAction("HMD Position", InputActionType.PassThrough, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            trackedPoseDriver.rotationInput = new InputActionProperty(
                new InputAction("HMD Rotation", InputActionType.PassThrough, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        }

        private static Transform CreateTrainingProp(Material rackMaterial, Material metalMaterial, Material accentMaterial, int fireTargetLayer)
        {
            GameObject propObject = new GameObject("TrainingProp")
            {
                layer = fireTargetLayer
            };

            // Placed ahead of the origin only as an authoring default; the placement controller
            // anchors the prop onto the tracked physical floor at runtime.
            propObject.transform.position = new Vector3(0f, 0f, 1.5f);

            Transform rackTransform = new GameObject("VirtualServerRack").transform;
            rackTransform.SetParent(propObject.transform, false);

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

            BoxCollider fireTargetCollider = propObject.AddComponent<BoxCollider>();
            fireTargetCollider.center = new Vector3(0f, 1f, 0f);
            fireTargetCollider.size = new Vector3(0.8f, 1.9f, 0.65f);

            // Trigger collider keeps the prop out of the physics solver; the suppression ray queries
            // triggers explicitly. Switch this off when evaluating particle-driven suppression,
            // because particle collisions ignore trigger colliders.
            fireTargetCollider.isTrigger = true;
            propObject.AddComponent<FireObjectIntegrityController>();

            CreateFireSource(propObject.transform);
            CreateSmokeVisual(propObject.transform);
            SetLayerRecursively(propObject.transform, fireTargetLayer);
            return propObject.transform;
        }

        private static void CreateRackPost(Transform parentTransform, Vector3 localPosition, Material material)
        {
            CreatePrimitive(PrimitiveType.Cube, "RackPost", parentTransform, localPosition, new Vector3(0.055f, 1.8f, 0.055f), material);
        }

        private static void CreateFireSource(Transform parentTransform)
        {
            GameObject fireObject = new GameObject("FireSource");
            fireObject.transform.SetParent(parentTransform, false);

            // The grid occupies the local X/Y plane on the front face of the rack.
            fireObject.transform.localPosition = new Vector3(0f, 0.92f, -0.34f);

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

        private static void CreateSmokeVisual(Transform parentTransform)
        {
            GameObject smokeObject = new GameObject("SmokeVisual");
            smokeObject.transform.SetParent(parentTransform, false);
            smokeObject.transform.localPosition = new Vector3(0f, 1.2f, -0.34f);

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

        private static Transform CreateExtinguisher(Material extinguisherMaterial, Material metalMaterial, Material hoseMaterial, Transform cameraOffsetTransform, int fireTargetLayer)
        {
            Transform extinguisherTransform = new GameObject("VirtualExtinguisher").transform;
            extinguisherTransform.position = new Vector3(0.25f, 1f, 0.25f);

            CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherBody", extinguisherTransform, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), extinguisherMaterial);
            CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherCollar", extinguisherTransform, new Vector3(0f, 0.17f, 0f), new Vector3(0.1f, 0.03f, 0.1f), metalMaterial);
            CreatePrimitive(PrimitiveType.Cube, "ExtinguisherHandle", extinguisherTransform, new Vector3(0f, 0.23f, 0.02f), new Vector3(0.07f, 0.03f, 0.14f), metalMaterial);

            Transform nozzleTransform = CreatePrimitive(PrimitiveType.Cylinder, "ExtinguisherNozzle", extinguisherTransform, new Vector3(0f, 0.2f, 0.16f), new Vector3(0.03f, 0.09f, 0.03f), hoseMaterial).transform;

            // The nozzle points along the local forward axis, which the suppression ray follows.
            nozzleTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem agentParticleSystem = CreateAgentParticleStream(nozzleTransform);
            agentParticleSystem.gameObject.AddComponent<ParticleCollisionHandler>();

            SuppressionRaycastController raycastController = extinguisherTransform.gameObject.AddComponent<SuppressionRaycastController>();
            raycastController.Configure(CreateActionProperty("Spray"), nozzleTransform, agentParticleSystem, 1 << fireTargetLayer);
            extinguisherTransform.gameObject.AddComponent<AgentSuppressionManager>();
            extinguisherTransform.gameObject.AddComponent<ControllerPoseProvider>();
            return extinguisherTransform;
        }

        private static ParticleSystem CreateAgentParticleStream(Transform nozzleTransform)
        {
            GameObject agentObject = new GameObject("ExtinguishingAgentStream");
            agentObject.transform.SetParent(nozzleTransform, false);

            // The nozzle mesh is rotated, so the stream is re-aligned with the nozzle forward axis.
            agentObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            agentObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);

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

        private static void CreateTrainingSystems()
        {
            GameObject systemsObject = new GameObject("TrainingSystems");
            systemsObject.AddComponent<DeviceProfiler>();
            systemsObject.AddComponent<PerformanceProfiler>();
            systemsObject.AddComponent<PerformanceConfigurationService>();
            systemsObject.AddComponent<SessionDataManager>();
            systemsObject.AddComponent<TrainingSessionController>();
            systemsObject.AddComponent<MrReadinessValidator>();
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("DirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.1f;
            directionalLight.color = new Color(0.94f, 0.95f, 1f);
            directionalLight.shadows = LightShadows.None;
        }

        private static void CreateTrainingHud(Transform cameraTransform)
        {
            GameObject canvasObject = new GameObject("TrainingHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(cameraTransform, false);

            // A head-locked world-space canvas: screen-space overlay is not rendered in XR.
            canvasObject.transform.localPosition = new Vector3(0f, -0.12f, 1.2f);
            canvasObject.transform.localScale = Vector3.one * 0.0011f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();
            canvasTransform.sizeDelta = new Vector2(900f, 600f);

            TMP_Text timerText = CreateText("Timer", canvasObject.transform, new Vector2(24f, -24f), new Vector2(320f, 48f), 30f, TextAlignmentOptions.Left, "TIME  00:00");
            TMP_Text statusText = CreateText("Status", canvasObject.transform, new Vector2(24f, -76f), new Vector2(520f, 42f), 22f, TextAlignmentOptions.Left, "STATUS  PLACE THE TRAINING PROP");
            Slider agentSlider = CreateHudSlider("AgentSlider", canvasObject.transform, new Vector2(24f, -132f), "AGENT");
            Slider fireSlider = CreateHudSlider("FireSlider", canvasObject.transform, new Vector2(24f, -184f), "FIRE");
            Slider integritySlider = CreateHudSlider("IntegritySlider", canvasObject.transform, new Vector2(24f, -236f), "INTEGRITY");
            fireSlider.value = 0f;

            GameObject resultsPanel = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
            resultsPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelTransform = resultsPanel.GetComponent<RectTransform>();
            panelTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelTransform.sizeDelta = new Vector2(460f, 280f);
            resultsPanel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.055f, 0.94f);
            TMP_Text resultsText = CreateText("ResultsText", resultsPanel.transform, Vector2.zero, new Vector2(420f, 240f), 26f, TextAlignmentOptions.Center, string.Empty);

            TMP_Text historyText = CreateText("HistoryText", canvasObject.transform, new Vector2(-24f, -24f), new Vector2(330f, 180f), 17f, TextAlignmentOptions.TopLeft, "RECENT SESSIONS\nLoading...");
            RectTransform historyTransform = historyText.rectTransform;
            historyTransform.anchorMin = new Vector2(1f, 1f);
            historyTransform.anchorMax = new Vector2(1f, 1f);
            historyTransform.pivot = new Vector2(1f, 1f);
            historyTransform.anchoredPosition = new Vector2(-24f, -24f);

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
            TMP_Text labelText = CreateText(label + "Label", parentTransform, anchoredPosition, new Vector2(140f, 28f), 18f, TextAlignmentOptions.Left, label);
            labelText.color = new Color(0.72f, 0.86f, 1f);

            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parentTransform, false);
            RectTransform sliderTransform = sliderObject.GetComponent<RectTransform>();
            sliderTransform.anchorMin = new Vector2(0f, 1f);
            sliderTransform.anchorMax = new Vector2(0f, 1f);
            sliderTransform.pivot = new Vector2(0f, 1f);
            sliderTransform.anchoredPosition = anchoredPosition + new Vector2(150f, -6f);
            sliderTransform.sizeDelta = new Vector2(230f, 18f);

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            backgroundObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            StretchToParent(backgroundObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            StretchToParent(fillAreaObject.GetComponent<RectTransform>(), new Vector2(2f, 2f), new Vector2(-2f, -2f));

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.14f, 0.72f, 0.98f, 1f);
            RectTransform fillTransform = fillObject.GetComponent<RectTransform>();
            StretchToParent(fillTransform, Vector2.zero, Vector2.zero);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillTransform;
            slider.targetGraphic = fillImage;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static void StretchToParent(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static InputActionProperty CreateActionProperty(string actionName)
        {
            InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(candidate => candidate.action != null && candidate.action.name == actionName);

            if (reference == null)
            {
                Debug.LogWarning($"MR Fire Safety: input action '{actionName}' was not found in {InputActionsPath}. Assign it manually in the inspector.");
                return default;
            }

            return new InputActionProperty(reference);
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer >= 0)
            {
                return existingLayer;
            }

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // Layers 0-7 are reserved by Unity.
            for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return layerIndex;
            }

            Debug.LogWarning($"MR Fire Safety: no free user layer available for '{layerName}'. Using the default layer.");
            return 0;
        }

        private static void SetLayerRecursively(Transform rootTransform, int layer)
        {
            rootTransform.gameObject.layer = layer;
            foreach (Transform childTransform in rootTransform)
            {
                SetLayerRecursively(childTransform, layer);
            }
        }

        private static void RegisterSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            updatedScenes[0] = new EditorBuildSettingsScene(ScenePath, true);

            int writeIndex = 1;
            foreach (EditorBuildSettingsScene existingScene in existingScenes)
            {
                if (existingScene.path == ScenePath)
                {
                    continue;
                }

                updatedScenes[writeIndex] = new EditorBuildSettingsScene(existingScene.path, false);
                writeIndex++;
            }

            EditorBuildSettings.scenes = updatedScenes.Take(writeIndex).ToArray();
        }

        private static GameObject CreatePrimitive(PrimitiveType primitiveType, string objectName, Transform parentTransform, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitiveObject = GameObject.CreatePrimitive(primitiveType);
            primitiveObject.name = objectName;
            primitiveObject.transform.SetParent(parentTransform, false);
            primitiveObject.transform.localPosition = localPosition;
            primitiveObject.transform.localScale = localScale;
            primitiveObject.GetComponent<Renderer>().sharedMaterial = material;

            // Mesh colliders from primitives are not needed: the prop carries one trigger collider.
            Object.DestroyImmediate(primitiveObject.GetComponent<Collider>());
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

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
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
