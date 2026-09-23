using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MRFireSafety.Tests.EditMode
{
    /// <summary>
    /// Guards the static content budget of the generated training scene. These are the parts of the
    /// performance target that can be verified without a headset: geometry density, shadow-casting
    /// lights, particle caps and renderer count. They do not replace on-device profiling, which
    /// remains the only way to confirm the sustained 72 FPS target, but they catch the content
    /// regressions that make that target unreachable in the first place.
    /// </summary>
    public sealed class TrainingSceneBudgetTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/FireTraining.unity";
        private const int MaximumTriangles = 10000;
        private const int MaximumRenderers = 40;
        private const int MaximumParticles = 400;

        /// <summary>
        /// Measures the generated scene against the mobile XR content budget and reports the totals.
        /// </summary>
        [Test]
        public void GeneratedScene_StaysWithinMobileContentBudget()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Assert.Ignore("The training scene has not been generated yet. Run MR Fire Safety > Build Training Scene.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int triangleCount = 0;
            int rendererCount = 0;
            int shadowCastingLightCount = 0;
            int realtimeLightCount = 0;
            int particleCapacity = 0;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (MeshFilter meshFilter in rootObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh != null)
                    {
                        triangleCount += meshFilter.sharedMesh.triangles.Length / 3;
                    }
                }

                rendererCount += rootObject.GetComponentsInChildren<MeshRenderer>(true).Length;

                foreach (Light light in rootObject.GetComponentsInChildren<Light>(true))
                {
                    realtimeLightCount++;
                    if (light.shadows != LightShadows.None)
                    {
                        shadowCastingLightCount++;
                    }
                }

                foreach (ParticleSystem particleSystem in rootObject.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particleCapacity += particleSystem.main.maxParticles;
                }
            }

            StringBuilder report = new StringBuilder("Training scene content budget\n");
            report.AppendLine($"  triangles: {triangleCount} (budget {MaximumTriangles})");
            report.AppendLine($"  mesh renderers: {rendererCount} (budget {MaximumRenderers})");
            report.AppendLine($"  lights: {realtimeLightCount}, shadow casting: {shadowCastingLightCount}");
            report.AppendLine($"  particle capacity: {particleCapacity} (budget {MaximumParticles})");
            Debug.Log(report.ToString());

            Assert.That(triangleCount, Is.LessThanOrEqualTo(MaximumTriangles),
                "Scene geometry must stay inside the mobile vertex budget.");
            Assert.That(rendererCount, Is.LessThanOrEqualTo(MaximumRenderers),
                "Too many separate renderers defeats batching on a mobile GPU.");
            Assert.That(shadowCastingLightCount, Is.Zero,
                "Real-time shadows are the dominant mobile GPU cost and must stay disabled.");
            Assert.That(particleCapacity, Is.LessThanOrEqualTo(MaximumParticles),
                "Particle overdraw must stay bounded.");
        }
    }
}
