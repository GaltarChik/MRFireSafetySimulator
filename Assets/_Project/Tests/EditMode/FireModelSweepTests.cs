using System.Text;
using MRFireSafety.Fire.Systems;
using NUnit.Framework;
using UnityEngine;

namespace MRFireSafety.Tests.EditMode
{
    /// <summary>
    /// Sweeps the fire-model tuning constants and reports, for each combination, how long the fire
    /// takes to become a real target and whether a trainee applying agent correctly can extinguish
    /// it within the extinguisher capacity. The output is the calibration evidence behind the
    /// shipped defaults; it is explicitly ignored during normal runs because it is a measurement
    /// tool rather than a pass/fail check.
    /// </summary>
    public sealed class FireModelSweepTests
    {
        private const float SimulationStepSeconds = 0.12f;
        private const float ActiveFireThreshold = 0.1f;
        private const float SuppressedFireThreshold = 0.025f;
        private const float MaximumSimulatedSeconds = 240f;
        private const float AgentCapacity = 10f;

        /// <summary>
        /// Reports the session envelope produced by each candidate parameter set.
        /// </summary>
        [Test]
        [Explicit("Measurement sweep: run manually when retuning the fire model.")]
        public void ParameterSweep_ReportsSessionEnvelope()
        {
            (int Width, int Height, float CellSize)[] grids =
            {
                (9, 7, 0.12f),
                (11, 7, 0.10f)
            };
            float[] propagationRates = { 0.045f, 0.035f, 0.03f };
            float[] suppressionRadii = { 0.25f, 0.3f, 0.35f };
            float[] agentRatesPerSecond = { 0.5f, 0.7f };

            StringBuilder report = new StringBuilder();
            report.AppendLine("Fire model parameter sweep");
            report.AppendLine("grid    | cell  | propagation | radius | agent/s | growth s | suppress s | total s | agent used | result");

            foreach ((int width, int height, float cellSize) in grids)
            {
                foreach (float propagationRate in propagationRates)
                {
                    foreach (float suppressionRadius in suppressionRadii)
                    {
                        foreach (float agentRate in agentRatesPerSecond)
                        {
                            MeasureEnvelope(width, height, cellSize, propagationRate, suppressionRadius, agentRate, report);
                        }
                    }
                }
            }

            Debug.Log(report.ToString());
            Assert.Pass();
        }

        private static void MeasureEnvelope(int gridWidth, int gridHeight, float cellSize, float propagationRate, float suppressionRadius, float agentRatePerSecond, StringBuilder report)
        {
            GameObject fireObject = new GameObject("FireSweep");
            FirePropagationSystem firePropagationSystem = fireObject.AddComponent<FirePropagationSystem>();
            firePropagationSystem.ConfigureSimulation(gridWidth, gridHeight, cellSize, propagationRate, 0.025f);
            firePropagationSystem.InitializeFire();

            float growthSeconds = 0f;
            while (firePropagationSystem.AverageIntensity < ActiveFireThreshold && growthSeconds < MaximumSimulatedSeconds)
            {
                firePropagationSystem.AdvanceSimulation(SimulationStepSeconds);
                growthSeconds += SimulationStepSeconds;
            }

            // Approximate correct extinguisher technique: the impact point covers the whole burning
            // face in a lawnmower pattern rather than resting on one spot.
            float faceWidth = gridWidth * cellSize;
            float faceHeight = gridHeight * cellSize;
            float suppressionSeconds = 0f;
            float agentConsumed = 0f;
            float sweepPhase = 0f;
            const float horizontalSecondsPerPass = 1.5f;
            const float verticalSecondsPerPass = 6f;

            while (firePropagationSystem.AverageIntensity > SuppressedFireThreshold
                && suppressionSeconds < MaximumSimulatedSeconds
                && agentConsumed < AgentCapacity)
            {
                sweepPhase += SimulationStepSeconds;
                float horizontalOffset = (Mathf.PingPong(sweepPhase / horizontalSecondsPerPass, 2f) - 1f) * faceWidth * 0.5f;
                float verticalOffset = (Mathf.PingPong(sweepPhase / verticalSecondsPerPass, 2f) - 1f) * faceHeight * 0.5f;
                float agentThisStep = agentRatePerSecond * SimulationStepSeconds;

                firePropagationSystem.ApplySuppression(new Vector3(horizontalOffset, verticalOffset, 0f), suppressionRadius, agentThisStep);
                firePropagationSystem.AdvanceSimulation(SimulationStepSeconds);
                agentConsumed += agentThisStep;
                suppressionSeconds += SimulationStepSeconds;
            }

            bool hasIgnited = growthSeconds < MaximumSimulatedSeconds;
            bool isExtinguished = firePropagationSystem.AverageIntensity <= SuppressedFireThreshold;
            float totalSeconds = growthSeconds + suppressionSeconds;
            string result;
            if (!hasIgnited)
            {
                result = "NEVER IGNITES";
            }
            else if (!isExtinguished)
            {
                result = agentConsumed >= AgentCapacity ? "OUT OF AGENT" : "UNWINNABLE";
            }
            else
            {
                result = totalSeconds >= 40f && totalSeconds <= 90f ? "IN WINDOW" : "extinguished";
            }

            report.AppendLine(
                $"{gridWidth,2}x{gridHeight,-2} | {cellSize,5:0.000} | {propagationRate,11:0.000} | {suppressionRadius,6:0.00} | {agentRatePerSecond,7:0.00} | " +
                $"{growthSeconds,8:0.0} | {suppressionSeconds,10:0.0} | {totalSeconds,7:0.0} | " +
                $"{agentConsumed,10:0.00} | {result}");

            Object.DestroyImmediate(fireObject);
        }
    }
}
