namespace MRFireSafety.Suppression.Controllers
{
    /// <summary>
    /// Selects which extinguisher mechanism is authoritative for reducing fire intensity and for
    /// consuming agent. Enabling both mechanisms at once would suppress each hit twice and would
    /// double-count agent consumption in the session report, so the mode must be chosen explicitly.
    /// </summary>
    public enum SuppressionSourceMode
    {
        /// <summary>
        /// The nozzle raycast reduces the fire and the particle stream is presentation only. This is
        /// the deterministic, lowest-cost option and the recommended default for the 72 FPS target.
        /// </summary>
        RaycastOnly = 0,

        /// <summary>
        /// Particle collisions reduce the fire, which models agent dispersion more faithfully at a
        /// higher runtime cost. The nozzle raycast is disabled.
        /// </summary>
        ParticleOnly = 1,

        /// <summary>
        /// Both mechanisms reduce the fire. Intended for comparative measurements during evaluation
        /// only: suppression and agent consumption are applied by both paths.
        /// </summary>
        Both = 2
    }
}
