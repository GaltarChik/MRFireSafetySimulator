using System;
using UnityEngine;

namespace MRFireSafety.Fire.Systems
{
    /// <summary>
    /// Simulates fire intensity on a compact two-dimensional cellular grid attached to a virtual
    /// fire source. The implementation allocates its buffers during initialization only, restricts
    /// suppression work to the affected cells, and publishes at most one intensity notification per
    /// frame, which keeps the simulation suitable for mobile XR frame budgets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirePropagationSystem : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(1)] private int _gridWidth = 7;
        [SerializeField, Min(1)] private int _gridHeight = 9;
        [SerializeField, Min(0.01f)] private float _cellSize = 0.1f;
        [SerializeField] private FireGridPlane _gridPlane = FireGridPlane.LocalXY;
        [SerializeField, Min(0.02f)] private float _simulationInterval = 0.12f;

        [Header("Behaviour")]
        [SerializeField, Range(0f, 1f)] private float _initialIgnitionIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float _propagationRate = 0.03f;
        [SerializeField, Range(0f, 1f)] private float _naturalDecayRate = 0.025f;

        [Header("Lifecycle")]
        [Tooltip("Leave disabled in mixed reality: the training session controller ignites the fire after the prop is anchored on the physical floor.")]
        [SerializeField] private bool _ignitesOnStart;

        private float[] _intensityBuffer;
        private float[] _nextIntensityBuffer;
        private float _simulationElapsedTime;
        private float _averageIntensity;
        private bool _isInitialized;
        private bool _hasPendingIntensityNotification;

        /// <summary>
        /// Raised once each time the simulation grid is initialized.
        /// </summary>
        public event Action FireInitialized;

        /// <summary>
        /// Raised at most once per frame after the aggregate fire intensity changes. Use this event
        /// for presentation concerns such as particles, lighting, and the training interface.
        /// </summary>
        public event Action<float> AverageIntensityChanged;

        /// <summary>
        /// Raised after each fixed simulation step. Use this event, not
        /// <see cref="AverageIntensityChanged"/>, for effects that accumulate over time: it carries
        /// the simulated duration of the step and fires at a constant rate that is independent of
        /// how often the user applies extinguishing agent.
        /// </summary>
        public event Action<FireSimulationStep> SimulationStepped;

        /// <summary>
        /// Gets the current average fire intensity in the inclusive range from zero to one.
        /// </summary>
        public float AverageIntensity => _averageIntensity;

        /// <summary>
        /// Gets whether the fire grid has been initialized and can receive suppression.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the local-space plane occupied by the fire grid.
        /// </summary>
        public FireGridPlane GridPlane => _gridPlane;

        /// <summary>
        /// Sets the tuning constants that define how aggressively the fire spreads, which is what
        /// distinguishes one training scenario difficulty from another. Call before
        /// <see cref="InitializeFire"/>; the grid is rebuilt on the next initialization.
        /// </summary>
        /// <param name="gridWidth">Number of cells across the burning surface.</param>
        /// <param name="gridHeight">Number of cells up the burning surface.</param>
        /// <param name="cellSize">Edge length of one cell, in metres.</param>
        /// <param name="propagationRate">Fraction of neighbouring intensity drawn in per step.</param>
        /// <param name="naturalDecayRate">Intensity lost per second by a cell with no burning neighbours.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension or the cell size is not positive.</exception>
        public void ConfigureSimulation(int gridWidth, int gridHeight, float cellSize, float propagationRate, float naturalDecayRate)
        {
            if (gridWidth < 1 || gridHeight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridWidth), "The fire grid must have at least one cell in each dimension.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            }

            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
            _propagationRate = Mathf.Clamp01(propagationRate);
            _naturalDecayRate = Mathf.Clamp01(naturalDecayRate);
        }

        /// <summary>
        /// Initializes the cellular grid and ignites its central cell. Calling the method again
        /// restarts the simulation and reuses the existing buffers when the grid size is unchanged.
        /// </summary>
        public void InitializeFire()
        {
            int cellCount = _gridWidth * _gridHeight;
            if (_intensityBuffer == null || _intensityBuffer.Length != cellCount)
            {
                _intensityBuffer = new float[cellCount];
                _nextIntensityBuffer = new float[cellCount];
            }
            else
            {
                Array.Clear(_intensityBuffer, 0, cellCount);
                Array.Clear(_nextIntensityBuffer, 0, cellCount);
            }

            int centerColumn = _gridWidth / 2;
            int centerRow = _gridHeight / 2;
            _intensityBuffer[GetCellIndex(centerColumn, centerRow)] = _initialIgnitionIntensity;
            _simulationElapsedTime = 0f;
            _isInitialized = true;
            UpdateAverageIntensity();
            FireInitialized?.Invoke();
            _hasPendingIntensityNotification = true;
        }

        /// <summary>
        /// Applies extinguishing agent to cells within a radius centered on a local-space position.
        /// Only the cells inside the affected bounding box are visited, so the cost scales with the
        /// agent radius rather than with the size of the grid.
        /// </summary>
        /// <param name="localPosition">Agent impact position expressed in the local space of the fire source.</param>
        /// <param name="radius">Radius, in metres, affected by the extinguishing agent.</param>
        /// <param name="reduction">Intensity removed from each fully affected cell.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when radius or reduction is negative.</exception>
        public void ApplySuppression(Vector3 localPosition, float radius, float reduction)
        {
            if (radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Suppression radius cannot be negative.");
            }

            if (reduction < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reduction), "Suppression reduction cannot be negative.");
            }

            if (!_isInitialized || reduction <= 0f)
            {
                return;
            }

            float safeRadius = Mathf.Max(radius, 0.001f);
            Vector2 impactPosition = ProjectOntoGridPlane(localPosition);
            GetAffectedCellRange(impactPosition, safeRadius, out int minimumColumn, out int maximumColumn, out int minimumRow, out int maximumRow);

            bool hasChangedIntensity = false;
            for (int rowIndex = minimumRow; rowIndex <= maximumRow; rowIndex++)
            {
                for (int columnIndex = minimumColumn; columnIndex <= maximumColumn; columnIndex++)
                {
                    Vector2 cellPosition = GetCellCenter(columnIndex, rowIndex);
                    float distance = Vector2.Distance(impactPosition, cellPosition);
                    if (distance > safeRadius)
                    {
                        continue;
                    }

                    float falloff = 1f - (distance / safeRadius);
                    int cellIndex = GetCellIndex(columnIndex, rowIndex);
                    float currentIntensity = _intensityBuffer[cellIndex];
                    float reducedIntensity = Mathf.Clamp01(currentIntensity - (reduction * falloff));
                    hasChangedIntensity |= !Mathf.Approximately(currentIntensity, reducedIntensity);
                    _intensityBuffer[cellIndex] = reducedIntensity;
                }
            }

            if (!hasChangedIntensity)
            {
                return;
            }

            UpdateAverageIntensity();
            _hasPendingIntensityNotification = true;
        }

        /// <summary>
        /// Gets the intensity of one cell in the fire grid.
        /// </summary>
        /// <param name="columnIndex">Zero-based horizontal index of the requested cell.</param>
        /// <param name="rowIndex">Zero-based vertical index of the requested cell.</param>
        /// <returns>The intensity in the inclusive range from zero to one.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid has not been initialized.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested cell is outside the grid.</exception>
        public float GetCellIntensity(int columnIndex, int rowIndex)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Fire grid has not been initialized.");
            }

            if (columnIndex < 0 || columnIndex >= _gridWidth || rowIndex < 0 || rowIndex >= _gridHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex), "Requested cell is outside the fire grid.");
            }

            return _intensityBuffer[GetCellIndex(columnIndex, rowIndex)];
        }

        private void Start()
        {
            if (_ignitesOnStart)
            {
                InitializeFire();
            }
        }

        /// <summary>
        /// Advances the simulation by however many fixed steps fit into the elapsed time. The frame
        /// loop calls this with the frame delta; tests and calibration runs call it directly to step
        /// the model deterministically without waiting in real time.
        /// </summary>
        /// <param name="deltaTimeSeconds">Elapsed time to simulate, in seconds.</param>
        /// <returns>The number of fixed steps that were simulated.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elapsed time is negative.</exception>
        public int AdvanceSimulation(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds), "Elapsed time cannot be negative.");
            }

            if (!_isInitialized)
            {
                return 0;
            }

            _simulationElapsedTime += deltaTimeSeconds;
            int stepCount = 0;
            while (_simulationElapsedTime >= _simulationInterval)
            {
                _simulationElapsedTime -= _simulationInterval;
                SimulateStep();
                stepCount++;
            }

            return stepCount;
        }

        private void Update()
        {
            AdvanceSimulation(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!_hasPendingIntensityNotification)
            {
                return;
            }

            _hasPendingIntensityNotification = false;
            AverageIntensityChanged?.Invoke(_averageIntensity);
        }

        private void SimulateStep()
        {
            for (int rowIndex = 0; rowIndex < _gridHeight; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < _gridWidth; columnIndex++)
                {
                    int cellIndex = GetCellIndex(columnIndex, rowIndex);
                    float currentIntensity = _intensityBuffer[cellIndex];
                    float neighbourAverage = GetNeighbourAverageIntensity(columnIndex, rowIndex);
                    float propagation = neighbourAverage * _propagationRate * (1f - currentIntensity);
                    float decay = _naturalDecayRate * _simulationInterval;
                    _nextIntensityBuffer[cellIndex] = Mathf.Clamp01(currentIntensity + propagation - decay);
                }
            }

            float[] previousBuffer = _intensityBuffer;
            _intensityBuffer = _nextIntensityBuffer;
            _nextIntensityBuffer = previousBuffer;
            UpdateAverageIntensity();
            _hasPendingIntensityNotification = true;
            SimulationStepped?.Invoke(new FireSimulationStep(_averageIntensity, _simulationInterval));
        }

        private float GetNeighbourAverageIntensity(int columnIndex, int rowIndex)
        {
            float neighbourSum = 0f;
            int neighbourCount = 0;

            AddNeighbourIntensity(columnIndex - 1, rowIndex, ref neighbourSum, ref neighbourCount);
            AddNeighbourIntensity(columnIndex + 1, rowIndex, ref neighbourSum, ref neighbourCount);
            AddNeighbourIntensity(columnIndex, rowIndex - 1, ref neighbourSum, ref neighbourCount);
            AddNeighbourIntensity(columnIndex, rowIndex + 1, ref neighbourSum, ref neighbourCount);

            return neighbourCount == 0 ? 0f : neighbourSum / neighbourCount;
        }

        private void AddNeighbourIntensity(int columnIndex, int rowIndex, ref float neighbourSum, ref int neighbourCount)
        {
            if (columnIndex < 0 || columnIndex >= _gridWidth || rowIndex < 0 || rowIndex >= _gridHeight)
            {
                return;
            }

            neighbourSum += _intensityBuffer[GetCellIndex(columnIndex, rowIndex)];
            neighbourCount++;
        }

        private void UpdateAverageIntensity()
        {
            float totalIntensity = 0f;
            for (int cellIndex = 0; cellIndex < _intensityBuffer.Length; cellIndex++)
            {
                totalIntensity += _intensityBuffer[cellIndex];
            }

            _averageIntensity = totalIntensity / _intensityBuffer.Length;
        }

        private void GetAffectedCellRange(Vector2 impactPosition, float radius, out int minimumColumn, out int maximumColumn, out int minimumRow, out int maximumRow)
        {
            float columnOrigin = (_gridWidth - 1) * 0.5f;
            float rowOrigin = (_gridHeight - 1) * 0.5f;
            minimumColumn = Mathf.Max(0, Mathf.FloorToInt(((impactPosition.x - radius) / _cellSize) + columnOrigin));
            maximumColumn = Mathf.Min(_gridWidth - 1, Mathf.CeilToInt(((impactPosition.x + radius) / _cellSize) + columnOrigin));
            minimumRow = Mathf.Max(0, Mathf.FloorToInt(((impactPosition.y - radius) / _cellSize) + rowOrigin));
            maximumRow = Mathf.Min(_gridHeight - 1, Mathf.CeilToInt(((impactPosition.y + radius) / _cellSize) + rowOrigin));
        }

        private Vector2 ProjectOntoGridPlane(Vector3 localPosition)
        {
            return _gridPlane == FireGridPlane.LocalXY
                ? new Vector2(localPosition.x, localPosition.y)
                : new Vector2(localPosition.x, localPosition.z);
        }

        private int GetCellIndex(int columnIndex, int rowIndex)
        {
            return (rowIndex * _gridWidth) + columnIndex;
        }

        private Vector2 GetCellCenter(int columnIndex, int rowIndex)
        {
            float horizontalOffset = (columnIndex - ((_gridWidth - 1) * 0.5f)) * _cellSize;
            float verticalOffset = (rowIndex - ((_gridHeight - 1) * 0.5f)) * _cellSize;
            return new Vector2(horizontalOffset, verticalOffset);
        }
    }
}
