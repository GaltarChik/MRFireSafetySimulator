using MRFireSafety.Fire.Systems;
using NUnit.Framework;
using UnityEngine;

namespace MRFireSafety.Tests.EditMode
{
    /// <summary>
    /// Validates the deterministic initialization and suppression behaviour of the cellular fire grid.
    /// </summary>
    public sealed class FirePropagationSystemTests
    {
        private const int CenterColumn = 3;
        private const int CenterRow = 4;

        /// <summary>
        /// Verifies that applying agent at the ignition point lowers total fire intensity.
        /// </summary>
        [Test]
        public void ApplySuppression_AtIgnitionPoint_ReducesAverageIntensity()
        {
            FirePropagationSystem firePropagationSystem = CreateInitializedSystem(out GameObject fireObject);
            float intensityBeforeSuppression = firePropagationSystem.AverageIntensity;

            firePropagationSystem.ApplySuppression(Vector3.zero, 1f, 1f);

            Assert.That(firePropagationSystem.AverageIntensity, Is.LessThan(intensityBeforeSuppression));
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that negative agent parameters are rejected before they can corrupt the grid.
        /// </summary>
        [Test]
        public void ApplySuppression_WithNegativeRadius_ThrowsArgumentOutOfRangeException()
        {
            FirePropagationSystem firePropagationSystem = CreateInitializedSystem(out GameObject fireObject);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => firePropagationSystem.ApplySuppression(Vector3.zero, -0.1f, 0.1f));
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that an impact beyond the agent radius leaves the burning grid untouched, which
        /// also covers the bounding-box restriction applied to the suppression loop.
        /// </summary>
        [Test]
        public void ApplySuppression_OutsideAgentRadius_LeavesIntensityUnchanged()
        {
            FirePropagationSystem firePropagationSystem = CreateInitializedSystem(out GameObject fireObject);
            float intensityBeforeSuppression = firePropagationSystem.AverageIntensity;

            firePropagationSystem.ApplySuppression(new Vector3(1.5f, 0f, 0f), 0.2f, 1f);

            Assert.That(firePropagationSystem.AverageIntensity, Is.EqualTo(intensityBeforeSuppression));
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that a narrow agent cone reduces the ignited cell without affecting its neighbour.
        /// </summary>
        [Test]
        public void ApplySuppression_WithNarrowRadius_ReducesOnlyTargetCell()
        {
            FirePropagationSystem firePropagationSystem = CreateInitializedSystem(out GameObject fireObject);
            float neighbourIntensityBefore = firePropagationSystem.GetCellIntensity(CenterColumn + 1, CenterRow);

            firePropagationSystem.ApplySuppression(Vector3.zero, 0.05f, 0.5f);

            Assert.That(firePropagationSystem.GetCellIntensity(CenterColumn, CenterRow), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(firePropagationSystem.GetCellIntensity(CenterColumn + 1, CenterRow), Is.EqualTo(neighbourIntensityBefore));
            Object.DestroyImmediate(fireObject);
        }

        /// <summary>
        /// Verifies that reinitializing the grid restores the ignition state of a suppressed fire.
        /// </summary>
        [Test]
        public void InitializeFire_AfterSuppression_RestoresIgnitionState()
        {
            FirePropagationSystem firePropagationSystem = CreateInitializedSystem(out GameObject fireObject);
            float initialAverageIntensity = firePropagationSystem.AverageIntensity;
            firePropagationSystem.ApplySuppression(Vector3.zero, 1f, 1f);

            firePropagationSystem.InitializeFire();

            Assert.That(firePropagationSystem.AverageIntensity, Is.EqualTo(initialAverageIntensity).Within(0.0001f));
            Object.DestroyImmediate(fireObject);
        }

        private static FirePropagationSystem CreateInitializedSystem(out GameObject fireObject)
        {
            fireObject = new GameObject("FirePropagationTest");
            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            firePropagationSystem.InitializeFire();
            return firePropagationSystem;
        }
    }
}
